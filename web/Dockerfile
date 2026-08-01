# syntax=docker/dockerfile:1
FROM node:24.18.0-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm config set fetch-timeout 600000 fetch-retries 5 \
	&& npm ci
COPY . .
RUN npm run build

FROM caddy:2-alpine
COPY --from=build /app/dist/streaks/browser /srv
