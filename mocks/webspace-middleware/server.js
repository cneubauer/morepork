// Mock of the Webspace Middleware API (https://qa-webspacemw.server.lan/apispec_1.json).
//
// Only the endpoints used by SpaceMiddlewareService.Publish are implemented:
//   POST   /{tenant}/webspaces                -> create
//   PUT    /{tenant}/webspaces/{resource_id}  -> update
//   DELETE /{tenant}/webspaces/{resource_id}  -> delete
//
// State is in-memory only and resets on restart.

const http = require('node:http');

const port = Number(process.env.PORT ?? 8081);
const host = process.env.HOST ?? '0.0.0.0';

// resource_id -> webspace document
const webspaces = new Map();
// ext_reference -> resource_id, backing the 303 pre-existing-resource behaviour
const externalReferences = new Map();
// resource_id -> host/addresses, kept across updates so a publish is idempotent
const placements = new Map();
// Deleted ids are remembered so they can answer 410 Gone instead of 404.
const tombstoned = new Set();

let nextWebspaceId = 1000;

// Mirrors sql/04-seed.sql, whose desired state already carries webspaceid
// 43210001. That webspace is therefore an existing resource: the worker's first
// publish is a PUT, not a POST, and against an empty mock it would 410. The
// host and addresses are the seed's own, so a publish round-trip returns what
// the desired state already holds instead of reassigning it.
const SEEDED_WEBSPACE = {
    tenant: 'demo',
    id: 43210001,
    // ToBackendExtensions builds this as {stackInstanceId}-{systemInstanceId}-{namespace}-{zone}.
    ext_reference: '1234567-5001234567-3-1',
    placement: {
        host: 'some-infong.schlund.de',
        webspace_ipv4: '123.123.123.123',
        webspace_ipv6: 'aa42:bb42:cc42:42:123:123:123:123',
    },
    document: {
        ext_reference: '1234567-5001234567-3-1',
        ext_correlation: '0',
        region: 'europe',
        state: 'enabled',
        biofilter_enabled: true,
        placement_tags: ['shl:standard'],
        limits: { diskquota: '5000000000b', resource_level: 'M' },
        owner: { uid: 654321, gid: 600, username: 'ws654321', groupname: 'ftpusers' },
        mailconfig: {
            host: 'some-mail-host.de',
            port: 25,
            username: 'some-mail-user',
            default_sender: 'some-mail@domain.de',
            default_envelope_from_policy: 'default_sender',
        },
        domains: [
            { ext_reference: 'foo.de', domain_id: 1230001, ext_correlation: '0', domain_name: 'foo.de', connect_type: 'docroot', state: 'enabled', docroot: { path: '/', type: 'user' } },
            { ext_reference: 'www.foo.de', domain_id: 1230002, ext_correlation: '0', domain_name: 'www.foo.de', connect_type: 'docroot', state: 'enabled', docroot: { path: '/', type: 'user' } },
            { domain_id: 67890001, ext_correlation: 'http-access-domain-correlation-id', domain_name: 'home-5004265496.some-product-domain.de', connect_type: 'docroot', state: 'enabled', docroot: { path: '/', type: 'user' } },
        ],
        accounts: [
            { ext_reference: '5c9392216d3e486f956b8e7b079f2c36', account_id: 5432101, ext_correlation: '0', username: 'a5432101', state: 'enabled', access_type: ['sftp'], account_type: 'standard', homedir_pubkeys: true, target: { path: '/', type: 'user' } },
            { ext_reference: 'bd075fa6bdb8434d99dcb6b5e7acd570', account_id: 5432102, ext_correlation: '1', username: 'a5432102', state: 'enabled', access_type: ['sftp', 'ssh'], account_type: 'standard', homedir_pubkeys: true, target: { path: '/', type: 'user' } },
        ],
    },
};

function seed() {
    const { tenant, id, ext_reference, placement, document } = SEEDED_WEBSPACE;

    placements.set(id, placement);
    webspaces.set(id, assignReadOnlyFields({ ...document }, tenant, id, placement));
    externalReferences.set(ext_reference, id);

    console.log(`[seed] ${tenant}/webspaces/${id} ext_reference=${ext_reference}`);
}

// Server-assigned fields. The spec marks these readOnly, so a client-supplied
// value is ignored and replaced with what the mock generates.
//
// `placement` carries the host and addresses an already-provisioned webspace was
// given. Generated for new resources, but for a seeded one it is the seed's own
// values, so a publish round-trip does not rewrite them.
function assignReadOnlyFields(webspace, tenant, id, placement) {
    const assigned = placement ?? generatePlacement(id, webspace.region);

    webspace.webspace_id = id;
    webspace.tenant = tenant;
    webspace.host = assigned.host;
    webspace.webspace_ipv4 = assigned.webspace_ipv4;
    webspace.webspace_ipv6 = assigned.webspace_ipv6;
    webspace.tech_webspace_id = id + 500000;
    webspace.slot_id = id + 900000;
    webspace.tech_mode = 'shared';
    webspace.state ??= 'enabled';
    return webspace;
}

function generatePlacement(id, region) {
    return {
        host: `webspace-${id}.${region ?? 'europe'}.mock.lan`,
        webspace_ipv4: `192.0.2.${id % 254 + 1}`,
        webspace_ipv6: `2001:db8::${id.toString(16)}`,
    };
}

// The real backend deploys asynchronously: the desired state is echoed back and
// _actual mirrors it as if convergence already happened.
function withActualState(webspace) {
    return { ...webspace, _actual: { ...webspace }, _errors: [] };
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

function sendError(response, statusCode, message) {
    sendJson(response, statusCode, { message, code: statusCode });
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

function createWebspace(response, tenant, body) {
    const externalReference = body.ext_reference;

    // Spec: if a resource with this ext_reference already exists, redirect to it.
    if (externalReference && externalReferences.has(externalReference)) {
        const existingId = externalReferences.get(externalReference);
        return sendJson(
            response,
            303,
            withActualState(webspaces.get(existingId)),
            { location: `/${tenant}/webspaces/${existingId}` },
        );
    }

    const id = nextWebspaceId++;
    const placement = generatePlacement(id, body.region);
    const webspace = assignReadOnlyFields({ ...body }, tenant, id, placement);

    placements.set(id, placement);
    webspaces.set(id, webspace);
    if (externalReference) {
        externalReferences.set(externalReference, id);
    }

    console.log(`[create] ${tenant}/webspaces/${id} ext_reference=${externalReference ?? '-'}`);
    return sendJson(response, 202, withActualState(webspace));
}

function updateWebspace(response, tenant, id, body) {
    if (tombstoned.has(id)) {
        return sendError(response, 410, `webspace ${id} is gone`);
    }
    if (!webspaces.has(id)) {
        return sendError(response, 410, `webspace ${id} does not exist`);
    }

    // readOnly fields survive the update; everything else is replaced.
    const webspace = assignReadOnlyFields({ ...body }, tenant, id, placements.get(id));
    webspaces.set(id, webspace);

    console.log(`[update] ${tenant}/webspaces/${id}`);
    return sendJson(response, 202, withActualState(webspace));
}

function deleteWebspace(response, tenant, id) {
    if (tombstoned.has(id)) {
        return sendError(response, 410, `webspace ${id} is already gone`);
    }
    if (!webspaces.has(id)) {
        return sendError(response, 410, `webspace ${id} does not exist`);
    }

    const webspace = webspaces.get(id);
    webspaces.delete(id);
    placements.delete(id);
    tombstoned.add(id);
    if (webspace.ext_reference) {
        externalReferences.delete(webspace.ext_reference);
    }

    console.log(`[delete] ${tenant}/webspaces/${id}`);
    return sendJson(response, 202, withActualState({ ...webspace, state: 'deleted' }));
}

const server = http.createServer(async (request, response) => {
    const { pathname } = new URL(request.url, `http://${request.headers.host ?? 'localhost'}`);
    const segments = pathname.split('/').filter(Boolean).map(decodeURIComponent);

    // Convenience endpoints for inspecting and restoring mock state while debugging.
    if (request.method === 'GET' && pathname === '/_mock/webspaces') {
        return sendJson(response, 200, [...webspaces.values()]);
    }
    if (request.method === 'POST' && pathname === '/_mock/reset') {
        webspaces.clear();
        externalReferences.clear();
        placements.clear();
        tombstoned.clear();
        nextWebspaceId = 1000;
        seed();
        return sendJson(response, 200, [...webspaces.values()]);
    }

    // Expected shapes: /{tenant}/webspaces and /{tenant}/webspaces/{resource_id}
    const isWebspaceRoute = segments.length >= 2 && segments[1] === 'webspaces';
    if (!isWebspaceRoute || segments.length > 3) {
        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    const tenant = segments[0];
    const resourceIdSegment = segments[2];

    let body = {};
    if (request.method === 'POST' || request.method === 'PUT') {
        try {
            body = await readJsonBody(request);
        } catch {
            return sendError(response, 400, 'request body is not valid JSON');
        }
    }

    if (resourceIdSegment === undefined) {
        if (request.method === 'POST') {
            return createWebspace(response, tenant, body);
        }
        return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }

    const id = Number(resourceIdSegment);
    if (!Number.isInteger(id) || id <= 0) {
        return sendError(response, 400, `invalid resource_id '${resourceIdSegment}'`);
    }

    switch (request.method) {
        case 'PUT':
            return updateWebspace(response, tenant, id, body);
        case 'DELETE':
            return deleteWebspace(response, tenant, id);
        default:
            return sendError(response, 404, `no mock for ${request.method} ${pathname}`);
    }
});

seed();

server.listen(port, host, () => {
    console.log(`webspace-middleware mock listening on http://${host}:${port}`);
});

// Without this the process ignores SIGTERM and every `docker compose down`
// waits out the 10s grace period before the container is killed.
for (const signal of ['SIGTERM', 'SIGINT']) {
    process.on(signal, () => {
        console.log(`${signal} received, shutting down`);
        server.close(() => process.exit(0));
        server.closeIdleConnections();
    });
}
