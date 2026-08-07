# syntax=docker/dockerfile:1
# Build context is this directory — the mock has no dependencies outside it.
#   docker build -f mocks/webspace-middleware/Dockerfile mocks/webspace-middleware

ARG NODE_VERSION=22

FROM node:${NODE_VERSION}-alpine AS final
WORKDIR /app

# No package.json and no install step: the mock imports only node:http.
COPY server.js ./

# The base image ships a non-root "node" user; nothing here needs to write.
USER node

ENV PORT=8081 \
    HOST=0.0.0.0
EXPOSE 8081

# Exec form, so the process gets PID 1 and receives SIGTERM directly.
ENTRYPOINT ["node", "server.js"]
