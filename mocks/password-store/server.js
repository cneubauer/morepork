// Mock of the Password Store API.
//
// Endpoints used by PasswordService:
//   PUT    /credential/v2/{tenant}/systemtype/{systemType}/token
//            -> Converts plaintext password into a token
//   DELETE /credential/v2/{tenant}/systemtype/{systemType}/token/{token}
//            -> Revokes/deletes a token
//   PUT    /credential/v2/{tenant}/stack-instance/{stackInstanceId}/system-instance/{systemInstanceId}/cleanup
//            -> Deletes all tokens for the stack/system instance except the excluded ones
//
// Additional inspection and debug endpoints:
//   GET    /credential/v2/{tenant}/systemtype/{systemType}/token/{token}
//            -> Reads token details (including password)
//   GET    /_mock/tokens
//            -> Returns all stored tokens (used by Docker healthcheck)
//   GET    /_mock/tokens/{token}
//            -> Inspects a single stored token
//   POST   /_mock/reset
//            -> Resets mock back to initial seeded state
//   GET    /_mock/config
//            -> Returns current mock config (failStatus, latencyMs)
//   POST   /_mock/config
//            -> Updates failStatus and latencyMs at runtime
//
// State is in-memory only and resets on restart.

const http = require('node:http');
const crypto = require('node:crypto');

const port = Number(process.env.PORT ?? 8082);
const host = process.env.HOST ?? '0.0.0.0';

const authUsername = process.env.AUTH_USERNAME || '';
const authPassword = process.env.AUTH_PASSWORD || '';

let currentFailStatus = process.env.FAIL_STATUS ? Number(process.env.FAIL_STATUS) : null;
let currentLatencyMs = Number(process.env.LATENCY_MS ?? 0);

// token -> token record
const tokens = new Map();

// Mirrors tokens present in sql/04-seed.sql for the seeded demo stack & system instance
const SEEDED_TOKENS = [
    {
        token: '03axxx755ddfab6b8b0dc5e005926a99',
        tenant: 'demo',
        systemType: 'SharedWebspaceLinux',
        ownerData: {
            stackInstanceId: '1234567',
            systemInstanceId: '5001234567',
        },
        passwordInfo: {
            password: 'seeded-account-password-1',
        },
        createdAt: '2026-07-14T10:06:47.495Z',
    },
    {
        token: '818xxxfcbbaa449f99dd9dc81ecc55cd',
        tenant: 'demo',
        systemType: 'SharedWebspaceLinux',
        ownerData: {
            stackInstanceId: '1234567',
            systemInstanceId: '5001234567',
        },
        passwordInfo: {
            password: 'seeded-account-password-2',
        },
        createdAt: '2026-07-14T10:06:47.495Z',
    },
    {
        token: 'ca6xxx3feb5842baaad3fae7123428f',
        tenant: 'demo',
        systemType: 'Smtp',
        ownerData: {
            stackInstanceId: '1234567',
            systemInstanceId: '5001234567',
        },
        passwordInfo: {
            password: 'seeded-mail-password',
        },
        createdAt: '2026-07-14T10:06:47.495Z',
    },
];

function seed() {
    tokens.clear();
    for (const item of SEEDED_TOKENS) {
        tokens.set(item.token, { ...item });
    }
    console.log(`[seed] Seeded ${tokens.size} tokens`);
}

function sendJson(response, statusCode, body, headers = {}) {
    const payload = JSON.stringify(body, null, 2);
    response.writeHead(statusCode, {
        'content-type': 'application/json',
        'content-length': Buffer.byteLength(payload),
        ...headers,
    });
    response.end(payload);
}

function sendError(response, statusCode, message, errors = []) {
    const body = { message, code: statusCode };
    if (errors.length > 0) {
        body.errors = errors;
    }
    sendJson(response, statusCode, body);
}

function readJsonBody(request) {
    return new Promise((resolve, reject) => {
        const chunks = [];
        request.on('data', chunk => chunks.push(chunk));
        request.on('error', reject);
        request.on('end', () => {
            const raw = Buffer.concat(chunks).toString('utf8');
            if (raw.trim() === '') {
                resolve({});
                return;
            }
            try {
                resolve(JSON.parse(raw));
            } catch (error) {
                reject(error);
            }
        });
    });
}

function checkAuth(request) {
    // If no credentials configured on the mock, allow all requests
    if (!authUsername && !authPassword) {
        return true;
    }

    const authHeader = request.headers['authorization'];
    if (!authHeader || !authHeader.startsWith('Basic ')) {
        return false;
    }

    try {
        const credentials = Buffer.from(authHeader.slice(6), 'base64').toString('utf8');
        const colonIndex = credentials.indexOf(':');
        if (colonIndex === -1) return false;
        const user = credentials.substring(0, colonIndex);
        const pass = credentials.substring(colonIndex + 1);
        return user === authUsername && pass === authPassword;
    } catch {
        return false;
    }
}

function generateToken() {
    return crypto.randomUUID().replace(/-/g, '');
}

function handleConvertCredential(response, tenant, systemType, body) {
    const password = body?.passwordInfo?.password;
    if (password === undefined || password === null) {
        return sendError(response, 400, 'Missing password in passwordInfo', ['password is required']);
    }

    const token = generateToken();
    const ownerData = {
        stackInstanceId: body?.ownerData?.stackInstanceId !== undefined ? String(body.ownerData.stackInstanceId) : null,
        systemInstanceId: body?.ownerData?.systemInstanceId !== undefined ? String(body.ownerData.systemInstanceId) : null,
    };

    const record = {
        token,
        tenant,
        systemType,
        ownerData,
        passwordInfo: {
            password: String(password),
        },
        createdAt: new Date().toISOString(),
    };

    tokens.set(token, record);
    console.log(`[convert] Generated token=${token} for tenant=${tenant} systemType=${systemType} stackInstanceId=${ownerData.stackInstanceId ?? '-'}`);

    return sendJson(response, 200, { token });
}

function handleDeleteToken(response, tenant, systemType, token) {
    const record = tokens.get(token);
    if (!record) {
        return sendError(response, 404, `Token '${token}' not found`);
    }

    tokens.delete(token);
    console.log(`[delete] Deleted token=${token} for tenant=${tenant} systemType=${systemType}`);
    return sendJson(response, 200, { token, deleted: true });
}

function handleCleanupTokens(response, tenant, stackInstanceId, systemInstanceId, body) {
    const excludeList = Array.isArray(body?.exclude) ? body.exclude : [];
    const excludeSet = new Set(excludeList.map(String));
    const deletedTokens = [];

    for (const [token, record] of tokens.entries()) {
        const matchesTenant = record.tenant === tenant;
        const matchesStack = String(record.ownerData?.stackInstanceId) === String(stackInstanceId);
        const matchesSystem = String(record.ownerData?.systemInstanceId) === String(systemInstanceId);

        if (matchesTenant && matchesStack && matchesSystem && !excludeSet.has(token)) {
            tokens.delete(token);
            deletedTokens.push(token);
        }
    }

    console.log(`[cleanup] Cleaned up ${deletedTokens.length} tokens for tenant=${tenant} stackInstanceId=${stackInstanceId} systemInstanceId=${systemInstanceId}`);
    return sendJson(response, 200, {
        deletedCount: deletedTokens.length,
        deletedTokens,
        remainingCount: tokens.size,
    });
}

function handleGetToken(response, tenant, systemType, token) {
    const record = tokens.get(token);
    if (!record) {
        return sendError(response, 404, `Token '${token}' not found`);
    }

    return sendJson(response, 200, record);
}

const server = http.createServer(async (request, response) => {
    const hostHeader = request.headers.host ?? 'localhost';
    const parsedUrl = new URL(request.url, `http://${hostHeader}`);
    const pathname = parsedUrl.pathname;
    const segments = pathname.split('/').filter(Boolean).map(decodeURIComponent);

    // Convenience endpoints for inspecting and controlling mock state
    if (segments[0] === '_mock') {
        if (request.method === 'GET' && segments.length === 2 && segments[1] === 'tokens') {
            const tenantFilter = parsedUrl.searchParams.get('tenant');
            const stackInstanceIdFilter = parsedUrl.searchParams.get('stackInstanceId');
            const systemInstanceIdFilter = parsedUrl.searchParams.get('systemInstanceId');

            let result = [...tokens.values()];
            if (tenantFilter) {
                result = result.filter(t => t.tenant === tenantFilter);
            }
            if (stackInstanceIdFilter) {
                result = result.filter(t => String(t.ownerData?.stackInstanceId) === stackInstanceIdFilter);
            }
            if (systemInstanceIdFilter) {
                result = result.filter(t => String(t.ownerData?.systemInstanceId) === systemInstanceIdFilter);
            }
            return sendJson(response, 200, result);
        }

        if (request.method === 'GET' && segments.length === 3 && segments[1] === 'tokens') {
            const token = segments[2];
            const record = tokens.get(token);
            if (!record) {
                return sendError(response, 404, `Token '${token}' not found`);
            }
            return sendJson(response, 200, record);
        }

        if (request.method === 'POST' && segments.length === 2 && segments[1] === 'reset') {
            seed();
            return sendJson(response, 200, {
                reset: true,
                tokensCount: tokens.size,
                tokens: [...tokens.values()],
            });
        }

        if (segments.length === 2 && segments[1] === 'config') {
            if (request.method === 'GET') {
                return sendJson(response, 200, {
                    failStatus: currentFailStatus,
                    latencyMs: currentLatencyMs,
                    authConfigured: Boolean(authUsername || authPassword),
                });
            }
            if (request.method === 'POST') {
                try {
                    const body = await readJsonBody(request);
                    if ('failStatus' in body) {
                        currentFailStatus = body.failStatus ? Number(body.failStatus) : null;
                    }
                    if ('latencyMs' in body) {
                        currentLatencyMs = Number(body.latencyMs ?? 0);
                    }
                    console.log(`[config] Updated mock config: failStatus=${currentFailStatus} latencyMs=${currentLatencyMs}`);
                    return sendJson(response, 200, {
                        failStatus: currentFailStatus,
                        latencyMs: currentLatencyMs,
                        authConfigured: Boolean(authUsername || authPassword),
                    });
                } catch {
                    return sendError(response, 400, 'Invalid JSON body');
                }
            }
        }

        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    // Check optional Basic Authentication for all non-_mock endpoints
    if (!checkAuth(request)) {
        response.setHeader('WWW-Authenticate', 'Basic realm="PasswordStore"');
        return sendError(response, 401, 'Unauthorized');
    }

    // Apply artificial latency if configured
    if (currentLatencyMs > 0) {
        await new Promise(resolve => setTimeout(resolve, currentLatencyMs));
    }

    // Apply simulated failure status if configured
    if (currentFailStatus) {
        const status = Number(currentFailStatus);
        return sendJson(response, status, {
            message: `Simulated mock failure with status ${status}`,
            errors: [`Mock failure: ${status}`],
        });
    }

    // Read body if POST / PUT
    let body = {};
    if (request.method === 'POST' || request.method === 'PUT') {
        try {
            body = await readJsonBody(request);
        } catch {
            return sendError(response, 400, 'request body is not valid JSON');
        }
    }

    // Route: /credential/v2/...
    if (segments[0] !== 'credential' || segments[1] !== 'v2') {
        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    const tenant = segments[2];

    // Shape: /credential/v2/{tenant}/systemtype/{systemType}/token (length 6)
    if (segments.length === 6 && segments[3] === 'systemtype' && segments[5] === 'token') {
        const systemType = segments[4];
        if (request.method === 'PUT') {
            return handleConvertCredential(response, tenant, systemType, body);
        }
        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    // Shape: /credential/v2/{tenant}/systemtype/{systemType}/token/{token} (length 7)
    if (segments.length === 7 && segments[3] === 'systemtype' && segments[5] === 'token') {
        const systemType = segments[4];
        const token = segments[6];
        if (request.method === 'DELETE') {
            return handleDeleteToken(response, tenant, systemType, token);
        }
        if (request.method === 'GET') {
            return handleGetToken(response, tenant, systemType, token);
        }
        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    // Shape: /credential/v2/{tenant}/stack-instance/{stackInstanceId}/system-instance/{systemInstanceId}/cleanup (length 8)
    // 0: 'credential', 1: 'v2', 2: tenant, 3: 'stack-instance', 4: stackInstanceId, 5: 'system-instance', 6: systemInstanceId, 7: 'cleanup'
    if (segments.length === 8 &&
        segments[3] === 'stack-instance' &&
        segments[5] === 'system-instance' &&
        segments[7] === 'cleanup') {
        const stackInstanceId = segments[4];
        const systemInstanceId = segments[6];
        if (request.method === 'PUT') {
            return handleCleanupTokens(response, tenant, stackInstanceId, systemInstanceId, body);
        }
        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
});

seed();

server.listen(port, host, () => {
    console.log(`password-store mock listening on http://${host}:${port}`);
});

for (const signal of ['SIGTERM', 'SIGINT']) {
    process.on(signal, () => {
        console.log(`${signal} received, shutting down`);
        server.close(() => process.exit(0));
        server.closeIdleConnections();
    });
}
