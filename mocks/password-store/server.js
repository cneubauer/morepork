// Mock of the Password Store API (credential v3), limited to the endpoints used by PasswordService.
//
// Reference: https://ac1winmidnlb.server.lan/PasswordStore/swagger/v1/swagger.json
//
// Endpoints:
//   POST /credential/v3/{tenant}/tokens
//          -> Converts a batch of plaintext passwords into tokens.
//   GET  /credential/v3/{tenant}/stack-instances/{stackInstanceId}/system-instances/{systemInstanceId}/tokens[?limit=&offset=]
//          -> Lists the tokens of a system instance, oldest first (paged).
//   PUT  /credential/v3/{tenant}/tokens/delete
//          -> Deletes the given tokens. Unknown tokens are skipped.
//
// Additional inspection and debug endpoints:
//   GET  /_mock/tokens
//          -> Returns all stored tokens (used by Docker healthcheck)
//   GET  /_mock/tokens/{token}
//          -> Inspects a single stored token
//   POST /_mock/reset
//          -> Resets mock back to initial seeded state
//
// State is in-memory only and resets on restart.

const http = require('node:http');
const crypto = require('node:crypto');

const port = Number(process.env.PORT ?? 8082);
const host = process.env.HOST ?? '0.0.0.0';

const authUsername = process.env.AUTH_USERNAME || '';
const authPassword = process.env.AUTH_PASSWORD || '';

const MAX_ITEMS = 1000;

// Numeric values of the WaaS `PasswordType` enum mapped to the API's SystemType names
const SYSTEM_TYPES_BY_NUMBER = {
    100: 'sharedWebspaceLinux',
    110: 'stretchSpace',
    150: 'sharedWebspaceWindows',
    200: 'databaseMySql',
    220: 'databaseMariaDB',
    250: 'databaseMSSql',
    300: 'smtp',
    350: 'webAnalytics',
};
const SYSTEM_TYPES = new Set([...Object.values(SYSTEM_TYPES_BY_NUMBER), 'databaseMySqlMariaDb']);

// token -> token record
const tokens = new Map();

// Mirrors tokens present in sql/04-seed.sql for the seeded demo stack & system instance
const SEEDED_TOKENS = [
    {
        token: '03axxx755ddfab6b8b0dc5e005926a99',
        referenceId: '5c9392216d3e486f956b8e7b079f2c36',
        tenant: 'demo',
        systemType: 'sharedWebspaceLinux',
        owner: { stackInstanceId: 1234567, systemInstanceId: 5001234567 },
        password: 'seeded-account-password-1',
        created: '2026-07-14T10:06:47.495Z',
    },
    {
        token: '818xxxfcbbaa449f99dd9dc81ecc55cd',
        referenceId: 'bd075fa6bdb8434d99dcb6b5e7acd570',
        tenant: 'demo',
        systemType: 'sharedWebspaceLinux',
        owner: { stackInstanceId: 1234567, systemInstanceId: 5001234567 },
        password: 'seeded-account-password-2',
        created: '2026-07-14T10:06:47.495Z',
    },
    {
        token: 'ca6xxx3feb5842baaad3fae7123428f',
        referenceId: 'mailconfiguration',
        tenant: 'demo',
        systemType: 'smtp',
        owner: { stackInstanceId: 1234567, systemInstanceId: 5001234567 },
        password: 'seeded-mail-password',
        created: '2026-07-14T10:06:47.495Z',
    },
];

function seed() {
    tokens.clear();
    for (const item of SEEDED_TOKENS) {
        tokens.set(item.token, { ...item, owner: { ...item.owner } });
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

// Accepts the API's string names as well as the numeric `PasswordType` values sent by PasswordService
function normalizeSystemType(value) {
    if (typeof value === 'number') {
        return SYSTEM_TYPES_BY_NUMBER[value] ?? null;
    }
    if (typeof value === 'string') {
        if (SYSTEM_TYPES.has(value)) return value;
        return SYSTEM_TYPES_BY_NUMBER[value] ?? null;
    }
    return null;
}

function parsePaging(searchParams) {
    const errors = [];
    const limit = searchParams.has('limit') ? Number(searchParams.get('limit')) : 100;
    const offset = searchParams.has('offset') ? Number(searchParams.get('offset')) : 0;
    if (!Number.isInteger(limit) || limit < 1 || limit > MAX_ITEMS) {
        errors.push(`limit must be an integer between 1 and ${MAX_ITEMS}`);
    }
    if (!Number.isInteger(offset) || offset < 0) {
        errors.push('offset must be a non-negative integer');
    }
    return { limit, offset, errors };
}

function toSummary(record) {
    return {
        token: record.token,
        systemType: record.systemType,
        status: 'active',
        created: record.created,
        ownerData: record.owner,
    };
}

function handleCreateTokens(response, tenant, body) {
    const passwordInfos = body?.passwordInfos;
    if (!Array.isArray(passwordInfos) || passwordInfos.length < 1 || passwordInfos.length > MAX_ITEMS) {
        return sendError(response, 400, 'Invalid passwordInfos', [`passwordInfos must be an array of 1 to ${MAX_ITEMS} items`]);
    }

    const seenReferenceIds = new Set();
    const errors = passwordInfos.flatMap((passwordInfo, index) => {
        const problems = [];
        const referenceId = passwordInfo?.referenceId;
        if (typeof referenceId !== 'string' || referenceId.length < 1 || referenceId.length > 100) {
            problems.push(`passwordInfos[${index}].referenceId is required (1-100 characters)`);
        } else if (seenReferenceIds.has(referenceId)) {
            problems.push(`passwordInfos[${index}].referenceId '${referenceId}' is duplicated`);
        } else {
            seenReferenceIds.add(referenceId);
        }

        const password = passwordInfo?.password;
        if (typeof password !== 'string' || password.length < 1 || password.length > 250) {
            problems.push(`passwordInfos[${index}].password is required (1-250 characters)`);
        }

        if (normalizeSystemType(passwordInfo?.systemType) === null) {
            problems.push(`passwordInfos[${index}].systemType is missing or unsupported`);
        }
        return problems;
    });

    if (errors.length > 0) {
        return sendError(response, 400, 'Invalid passwordInfos', errors);
    }

    const now = new Date().toISOString();

    const created = passwordInfos.map(passwordInfo => {
        const token = generateToken();

        // The real API requires `owner`. It is optional here so that clients that do not send it yet still work,
        // but such tokens do not show up in the system instance token list.
        const owner = passwordInfo.owner
            ? {
                stackInstanceId: Number(passwordInfo.owner.stackInstanceId),
                systemInstanceId: Number(passwordInfo.owner.systemInstanceId),
            }
            : null;
        if (!owner) {
            console.warn(`[create] referenceId=${passwordInfo.referenceId} has no owner (required by the real API)`);
        }

        tokens.set(token, {
            token,
            referenceId: passwordInfo.referenceId,
            tenant,
            systemType: normalizeSystemType(passwordInfo.systemType),
            owner,
            password: passwordInfo.password,
            created: now,
        });

        return { referenceId: passwordInfo.referenceId, token };
    });

    console.log(`[create] Generated ${created.length} tokens for tenant=${tenant}`);
    return sendJson(response, 200, { tokens: created });
}

function handleListSystemInstanceTokens(response, tenant, stackInstanceId, systemInstanceId, searchParams) {
    const { limit, offset, errors } = parsePaging(searchParams);
    if (!/^\d+$/.test(stackInstanceId) || !/^\d+$/.test(systemInstanceId)) {
        errors.push('stackInstanceId and systemInstanceId must be integers');
    }
    if (errors.length > 0) {
        return sendError(response, 400, 'Invalid query', errors);
    }

    const owned = [...tokens.values()]
        .filter(t => t.tenant === tenant
            && String(t.owner?.stackInstanceId) === stackInstanceId
            && String(t.owner?.systemInstanceId) === systemInstanceId)
        .sort((a, b) => a.created.localeCompare(b.created));

    return sendJson(response, 200, {
        items: owned.slice(offset, offset + limit).map(toSummary),
        total: owned.length,
        limit,
        offset,
    });
}

function handleDeleteTokens(response, tenant, body) {
    const requested = body?.tokens;
    if (!Array.isArray(requested) || requested.length < 1 || requested.length > MAX_ITEMS
        || requested.some(token => typeof token !== 'string' || token === '')) {
        return sendError(response, 400, 'Invalid tokens', [`tokens must be an array of 1 to ${MAX_ITEMS} non-empty strings`]);
    }

    // Unknown tokens and tokens of other tenants are skipped, only the deleted ones are returned
    const deleted = [];
    for (const token of new Set(requested)) {
        const record = tokens.get(token);
        if (record && record.tenant === tenant) {
            tokens.delete(token);
            deleted.push({ token, systemType: record.systemType, ownerData: record.owner });
        }
    }

    console.log(`[delete] Deleted ${deleted.length} tokens for tenant=${tenant}`);
    return sendJson(response, 200, deleted);
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

    if (segments[0] !== 'credential' || segments[1] !== 'v3' || segments.length < 4) {
        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    const tenant = segments[2];
    const method = request.method;

    // GET /credential/v3/{tenant}/stack-instances/{id}/system-instances/{id}/tokens
    if (method === 'GET'
        && segments.length === 8
        && segments[3] === 'stack-instances'
        && segments[5] === 'system-instances'
        && segments[7] === 'tokens') {
        return handleListSystemInstanceTokens(response, tenant, segments[4], segments[6], parsedUrl.searchParams);
    }

    // POST /credential/v3/{tenant}/tokens and PUT /credential/v3/{tenant}/tokens/delete
    const isCreate = method === 'POST' && segments.length === 4 && segments[3] === 'tokens';
    const isDelete = method === 'PUT' && segments.length === 5 && segments[3] === 'tokens' && segments[4] === 'delete';
    if (!isCreate && !isDelete) {
        return sendError(response, 404, `no mock for ${method} ${pathname}`);
    }

    let body;
    try {
        body = await readJsonBody(request);
    } catch {
        return sendError(response, 400, 'request body is not valid JSON');
    }

    return isCreate
        ? handleCreateTokens(response, tenant, body)
        : handleDeleteTokens(response, tenant, body);
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
