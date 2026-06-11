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

# Build-time FE config. DEV default mirrors frontend/.env.example; override at
# `docker build --build-arg VITE_API_BASE_URL=...` (or compose build.args) for a PROD build.
# Step L pruned all VITE_OIDC_* args: the FE holds no OIDC config (login redirects to BE
# /auth/login server-side), so VITE_API_BASE_URL is the sole surviving build var.
ARG VITE_API_BASE_URL=http://localhost:5000

ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

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
