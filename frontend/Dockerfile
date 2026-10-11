# syntax=docker/dockerfile:1

# ---------- Build stage: compile the React app ----------
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# ---------- Runtime stage: nginx serving static files (non-root) ----------
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final

# Runtime configuration (overridable with `docker run -e` / compose / the hosting dashboard).
#   PORT              port nginx listens on (Render injects its own)
#   BACKEND_UPSTREAM  where /api/ is proxied to (compose service alias)
#   API_BASE_URL      absolute backend URL for production; leave empty to use the /api proxy
ENV PORT=8080 \
    BACKEND_UPSTREAM=http://backend:8080 \
    NGINX_RESOLVER=127.0.0.11 \
    API_BASE_URL=""

USER root
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY docker-entrypoint.d/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
COPY --from=build /app/dist /usr/share/nginx/html
RUN chmod +x /docker-entrypoint.d/40-runtime-config.sh \
    && chown -R nginx:nginx /usr/share/nginx/html
USER nginx

EXPOSE 8080
