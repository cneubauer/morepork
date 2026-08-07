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
// Deleted ids are remembered so they can answer 410 Gone instead of 404.
const tombstoned = new Set();

let nextWebspaceId = 1000;

// Server-assigned fields. The spec marks these readOnly, so a client-supplied
// value is ignored and replaced with what the mock generates.
function assignReadOnlyFields(webspace, tenant, id) {
    webspace.webspace_id = id;
    webspace.tenant = tenant;
    webspace.host = `webspace-${id}.${webspace.region ?? 'europe'}.mock.lan`;
    webspace.webspace_ipv4 = `192.0.2.${id % 254 + 1}`;
    webspace.webspace_ipv6 = `2001:db8::${id.toString(16)}`;
    webspace.tech_webspace_id = id + 500000;
    webspace.slot_id = id + 900000;
    webspace.tech_mode = 'shared';
    webspace.state ??= 'enabled';
    return webspace;
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
    const webspace = assignReadOnlyFields({ ...body }, tenant, id);

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
    const webspace = assignReadOnlyFields({ ...body }, tenant, id);
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

    // Convenience endpoint for inspecting mock state while debugging.
    if (request.method === 'GET' && pathname === '/_mock/webspaces') {
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

server.listen(port, host, () => {
    console.log(`webspace-middleware mock listening on http://${host}:${port}`);
});
