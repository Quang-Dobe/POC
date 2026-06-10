# Frontend image — multi-stage (infra-cicd §Images).
# Build stage: node:22-alpine runs `npm ci && npm run build`. Vite bakes VITE_* at BUILD
# time, so per-env FE config is build-per-env (infra-cicd): each VITE_* is a build ARG with a
# DEV default matching frontend/.env.example, promoted to ENV before `npm run build`.
# Runtime stage: nginx:alpine serves the static dist/ with the SPA-fallback default.conf.

# ---- build stage ----
FROM node:22-alpine AS build
WORKDIR /app

# Install deps from the lockfile first (cache-friendly: only re-runs when lock/manifest change).
# npm ci needs devDependencies (TypeScript/types) because `build` = `tsc -b && vite build`.
COPY package.json package-lock.json ./
RUN npm ci

# Build-time FE config. DEV defaults mirror frontend/.env.example concrete values; override at
# `docker build --build-arg VITE_*=...` (or compose build.args) for a PROD build. NOT secrets —
# the SPA is a public PKCE client (secrets-dual-provider).
ARG VITE_OIDC_AUTHORITY=http://localhost:8080/realms/poc
ARG VITE_OIDC_CLIENT_ID=poc-spa
ARG VITE_OIDC_SCOPE="openid profile poc-api"
ARG VITE_API_BASE_URL=http://localhost:5000
ARG VITE_OIDC_REDIRECT_URI=http://localhost:5173/callback

ENV VITE_OIDC_AUTHORITY=$VITE_OIDC_AUTHORITY \
    VITE_OIDC_CLIENT_ID=$VITE_OIDC_CLIENT_ID \
    VITE_OIDC_SCOPE=$VITE_OIDC_SCOPE \
    VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_OIDC_REDIRECT_URI=$VITE_OIDC_REDIRECT_URI

COPY . .
RUN npm run build

# ---- runtime stage ----
FROM nginx:alpine AS runtime

# SPA-fallback config (try_files $uri /index.html) is supplied at RUNTIME, not baked here:
# infra/dev/nginx/default.conf lives outside this image's build context (../frontend) and is
# mounted by docker-compose into /etc/nginx/conf.d/default.conf. This keeps the conf the single
# reproducible source under infra/dev/ without widening the build context to the repo root.

# Static build output. nginx:alpine already runs the worker as the non-privileged `nginx` user.
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
# nginx:alpine ships a default CMD that runs the server in the foreground.
