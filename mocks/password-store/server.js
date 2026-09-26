// Mock of the Password Store API.
//
// Endpoints used by PasswordActivities:
//   PUT  /credential/v3/{tenant}/tokens[?transactional=true]
//          -> Converts a batch of plaintext passwords into tokens.
//             With transactional=true, tokens are stored with a short
//             expiration and are not permanent until committed.
//   PUT  /credential/v3/{tenant}/tokens/commit
//          -> Commits previously created transactional tokens, making
//             them permanent (clears their expiration).
//   PUT  /credential/v3/{tenant}/tokens/delete
//          -> Deletes exactly the given tokens
//
// Additional inspection and debug endpoints:
//   GET  /_mock/tokens
//          -> Returns all stored tokens (used by Docker healthcheck)
//   GET  /_mock/tokens/{token}
//          -> Inspects a single stored token
//   POST /_mock/reset
//          -> Resets mock back to initial seeded state
//
// State is in-memory only and resets on restart. Uncommitted transactional
// tokens are purged once their expiration passes.

const http = require('node:http');
const crypto = require('node:crypto');

const port = Number(process.env.PORT ?? 8082);
const host = process.env.HOST ?? '0.0.0.0';

const authUsername = process.env.AUTH_USERNAME || '';
const authPassword = process.env.AUTH_PASSWORD || '';

const transactionalTokenTtlMs = Number(process.env.TRANSACTIONAL_TOKEN_TTL_MS ?? 24 * 60 * 60 * 1000);

// token -> token record
const tokens = new Map();

// Mirrors tokens present in sql/04-seed.sql for the seeded demo stack & system instance
const SEEDED_TOKENS = [
    {
        token: '03axxx755ddfab6b8b0dc5e005926a99',
        referenceId: '5c9392216d3e486f956b8e7b079f2c36',
        tenant: 'demo',
        systemType: 100, // SharedWebspaceLinux
        owner: {
            stackInstanceId: '1234567',
            systemInstanceId: '5001234567',
        },
        password: 'seeded-account-password-1',
        createdAt: '2026-07-14T10:06:47.495Z',
        expiresAt: null,
    },
    {
        token: '818xxxfcbbaa449f99dd9dc81ecc55cd',
        referenceId: 'bd075fa6bdb8434d99dcb6b5e7acd570',
        tenant: 'demo',
        systemType: 100, // SharedWebspaceLinux
        owner: {
            stackInstanceId: '1234567',
            systemInstanceId: '5001234567',
        },
        password: 'seeded-account-password-2',
        createdAt: '2026-07-14T10:06:47.495Z',
        expiresAt: null,
    },
    {
        token: 'ca6xxx3feb5842baaad3fae7123428f',
        referenceId: 'mailconfiguration',
        tenant: 'demo',
        systemType: 300, // Smtp
        owner: {
            stackInstanceId: '1234567',
            systemInstanceId: '5001234567',
        },
        password: 'seeded-mail-password',
        createdAt: '2026-07-14T10:06:47.495Z',
        expiresAt: null,
    },
];

function seed() {
    tokens.clear();
    for (const item of SEEDED_TOKENS) {
        tokens.set(item.token, { ...item });
    }
    console.log(`[seed] Seeded ${tokens.size} tokens`);
}

function sendJson(response, statusCode, body) {
    const payload = JSON.stringify(body, null, 2);
    response.writeHead(statusCode, {
        'content-type': 'application/json',
        'content-length': Buffer.byteLength(payload),
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

function purgeExpiredTransactionalTokens() {
    const now = Date.now();
    for (const [token, record] of tokens.entries()) {
        if (record.expiresAt && new Date(record.expiresAt).getTime() <= now) {
            tokens.delete(token);
        }
    }
}

function handleConvertCredentials(response, tenant, body, transactional) {
    const passwordInfos = body?.passwordInfos;
    if (!Array.isArray(passwordInfos)) {
        return sendError(response, 400, 'Missing passwordInfos', ['passwordInfos must be an array']);
    }

    const errors = passwordInfos.flatMap((passwordInfo, index) => {
        const problems = [];
        if (!passwordInfo?.referenceId) {
            problems.push(`passwordInfos[${index}].referenceId is required`);
        }
        if (passwordInfo?.password === undefined || passwordInfo?.password === null) {
            problems.push(`passwordInfos[${index}].password is required`);
        }
        return problems;
    });

    if (errors.length > 0) {
        return sendError(response, 400, 'Invalid passwordInfos', errors);
    }

    const now = new Date();
    const expiresAt = transactional
        ? new Date(now.getTime() + transactionalTokenTtlMs).toISOString()
        : null;

    const created = passwordInfos.map(passwordInfo => {
        const token = generateToken();
        tokens.set(token, {
            token,
            referenceId: String(passwordInfo.referenceId),
            tenant,
            systemType: passwordInfo.systemType ?? null,
            owner: {
                stackInstanceId: passwordInfo.owner?.stackInstanceId !== undefined
                    ? String(passwordInfo.owner.stackInstanceId)
                    : null,
                systemInstanceId: passwordInfo.owner?.systemInstanceId !== undefined
                    ? String(passwordInfo.owner.systemInstanceId)
                    : null,
            },
            password: String(passwordInfo.password),
            createdAt: now.toISOString(),
            expiresAt,
        });

        return { referenceId: String(passwordInfo.referenceId), token, expires: expiresAt };
    });

    console.log(`[convert] Generated ${created.length} tokens for tenant=${tenant} (transactional=${transactional})`);
    return sendJson(response, 200, { tokens: created });
}

function handleCommitTokens(response, tenant, body) {
    const requestedTokens = Array.isArray(body?.tokens) ? body.tokens.map(String) : null;
    if (!requestedTokens) {
        return sendError(response, 400, 'Missing tokens', ['tokens must be an array']);
    }

    const committedTokens = [];
    const notFoundTokens = [];

    for (const token of requestedTokens) {
        const record = tokens.get(token);
        if (record && record.tenant === tenant) {
            record.expiresAt = null;
            committedTokens.push(token);
        } else {
            notFoundTokens.push(token);
        }
    }

    if (notFoundTokens.length > 0) {
        return sendError(response, 404, 'Some tokens were not found', notFoundTokens.map(token => `token '${token}' not found`));
    }

    console.log(`[commit] Committed ${committedTokens.length} tokens for tenant=${tenant}`);
    return sendJson(response, 200, {
        committedCount: committedTokens.length,
        committedTokens,
    });
}

function handleDeleteTokens(response, tenant, body) {
    const requestedTokens = new Set((Array.isArray(body?.tokens) ? body.tokens : []).map(String));
    const deletedTokens = [];

    for (const token of requestedTokens) {
        const record = tokens.get(token);
        if (record && record.tenant === tenant) {
            tokens.delete(token);
            deletedTokens.push(token);
        }
    }

    console.log(`[delete] Deleted ${deletedTokens.length} tokens for tenant=${tenant}`);
    return sendJson(response, 200, {
        deletedCount: deletedTokens.length,
        deletedTokens,
        remainingCount: tokens.size,
    });
}

const server = http.createServer(async (request, response) => {
    const hostHeader = request.headers.host ?? 'localhost';
    const parsedUrl = new URL(request.url, `http://${hostHeader}`);
    const pathname = parsedUrl.pathname;
    const segments = pathname.split('/').filter(Boolean).map(decodeURIComponent);

    // Convenience endpoints for inspecting and resetting mock state, no auth required
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
                result = result.filter(t => String(t.owner?.stackInstanceId) === stackInstanceIdFilter);
            }
            if (systemInstanceIdFilter) {
                result = result.filter(t => String(t.owner?.systemInstanceId) === systemInstanceIdFilter);
            }
            return sendJson(response, 200, result);
        }

        if (request.method === 'GET' && segments.length === 3 && segments[1] === 'tokens') {
            const record = tokens.get(segments[2]);
            if (!record) {
                return sendError(response, 404, `Token '${segments[2]}' not found`);
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

        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    // Check optional Basic Authentication for all non-_mock endpoints
    if (!checkAuth(request)) {
        response.setHeader('WWW-Authenticate', 'Basic realm="PasswordStore"');
        return sendError(response, 401, 'Unauthorized');
    }

    // Route: /credential/v3/{tenant}/tokens[/commit|/delete]
    if (segments[0] !== 'credential' || segments[1] !== 'v3' || segments[3] !== 'tokens' || request.method !== 'PUT') {
        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    purgeExpiredTransactionalTokens();

    let body;
    try {
        body = await readJsonBody(request);
    } catch {
        return sendError(response, 400, 'request body is not valid JSON');
    }

    const tenant = segments[2];

    if (segments.length === 4) {
        const transactional = parsedUrl.searchParams.get('transactional') === 'true';
        return handleConvertCredentials(response, tenant, body, transactional);
    }

    if (segments.length === 5 && segments[4] === 'commit') {
        return handleCommitTokens(response, tenant, body);
    }

    if (segments.length === 5 && segments[4] === 'delete') {
        return handleDeleteTokens(response, tenant, body);
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
