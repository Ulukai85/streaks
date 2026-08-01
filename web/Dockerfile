# syntax=docker/dockerfile:1
FROM node:24.18.0-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm config set fetch-timeout 600000 fetch-retries 5 \
	&& npm ci
COPY . .
RUN npm run build

FROM caddy:2.11.4-alpine
# The official image has no built-in non-root user (unlike the .NET runtime images), so create
# one - /data and /config are where Caddy writes at runtime (TLS storage, autosaved config);
# /srv comes pre-owned via COPY --chown below. Binding :80 as non-root needs
# CAP_NET_BIND_SERVICE, added in infrastructure/docker-compose.yml.
RUN adduser -D -u 1000 caddy && mkdir -p /data /config && chown -R caddy /data /config
COPY --from=build --chown=caddy /app/dist/streaks/browser /srv
USER caddy
