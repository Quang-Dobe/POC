# POC.FE

Frontend single-page app for the POC's Backend-for-Frontend (BFF). It signs the
user in through the backend, then renders a protected message and a streaming
"ask the agent" view. The browser never sees a token: authentication rides on an
opaque httpOnly session cookie issued by the backend.

## Stack

- React 18 + TypeScript
- Vite 5 (dev server, build, env loading)
- Tailwind CSS 3 + shadcn/ui (new-york style; Radix Slot, `class-variance-authority`, `lucide-react`)
- react-router-dom 6 (routing + route guard)
- Vitest 2 + Testing Library + jsdom (unit/component tests)

## Project structure

```
src/
  main.tsx            App bootstrap: AuthProvider > BrowserRouter > App
  App.tsx             Routes: "/" (landing), "/message" (guarded)
  config.ts           Reads VITE_API_BASE_URL from import.meta.env (throws if missing)
  vite-env.d.ts       ImportMetaEnv typing for VITE_API_BASE_URL
  api/
    client.ts         fetchMessage (GET /api/message), askAgent (POST /api/ask)
    stream.ts         openAskStream (POST /api/ask/stream, SSE-style reader), postExtendSession
  auth/
    AuthContext.tsx   AuthProvider + useAuth: login/logout/recheck, /auth/me probe
  components/
    RequireAuth.tsx   Route guard: loading -> placeholder, anon -> redirect to "/"
    ui/               shadcn primitives (button, card)
  pages/
    LandingPage.tsx   Sign-in card; redirects to /message when already authed
    MessagePage.tsx   Shows the protected message + streamed ask flow + sign out
  lib/utils.ts        cn() classname helper (clsx + tailwind-merge)
  index.css           Tailwind layers + theme CSS variables
  test/setup.ts       Testing Library jest-dom matchers
```

Root: `Dockerfile`, `vite.config.ts`, `tailwind.config.js`, `postcss.config.js`,
`components.json`, `tsconfig*.json`, `index.html`, `.env.example`.

## How auth works (frontend side)

The frontend holds no client secret and no OIDC config. The login redirect and
the session cookie are owned entirely by the backend.

1. On load, `AuthProvider` calls `GET {VITE_API_BASE_URL}/auth/me` with
   `credentials: 'include'`. A 200 with `{ displayName, roles }` means there is a
   live session (`isAuthenticated === true`); a 401 or network failure means
   anonymous. `isLoading` is true until this probe resolves.
2. `RequireAuth` wraps `/message`: it shows a loading placeholder while the probe
   is pending, redirects anonymous users to `/`, and renders the page when authed.
3. `login()` does a full-page redirect to `{VITE_API_BASE_URL}/auth/login`. The
   backend runs the OIDC flow and sets the httpOnly session cookie, then returns
   the user to the SPA. `LandingPage` reads `?error=access_denied` to show a
   denied message.
4. Every API call (`/api/message`, `/api/ask`, `/api/ask/stream`, `/auth/*`) is
   sent with `credentials: 'include'` and no `Authorization` header — the cookie
   is the only credential. A 401 throws `UnauthorizedError`, which triggers
   `recheck()` so the UI flips back to anonymous.
5. `logout()` POSTs `{VITE_API_BASE_URL}/auth/logout` and clears local auth state.
6. During a long stream the backend can emit a `control: extend-session` event;
   the page responds by POSTing `{VITE_API_BASE_URL}/auth/extend-session` to keep
   the session alive.

The streaming endpoint (`/api/ask/stream`) is read as a chunked body and parsed
into SSE-style frames (`event:` / `data:` lines split on blank lines), so
answers render incrementally.

## Running locally

Requires Node.js (the Docker build uses Node 22) and npm.

```
npm install
npm run dev      # Vite dev server
npm run build    # tsc -b && vite build  -> dist/
npm run preview  # serve the production build
npm run test     # vitest run (one-shot)
npm run test:watch
```

## Environment variables

Vite bakes `VITE_*` values at build time, so there is one build per environment.
See `.env.example` for the committed template. Create an untracked local file
(`.env.development` / `.env.local` for dev, `.env.production` for prod).

| Variable | Required | Description |
| --- | --- | --- |
| `VITE_API_BASE_URL` | yes | Origin of the backend/BFF (e.g. `https://localhost:5000`). `config.ts` throws at startup if it is missing or empty. |

## Docker

The `Dockerfile` is a multi-stage build:

- Build stage (`node:22-alpine`): `npm ci` then `npm run build`. `VITE_API_BASE_URL`
  is a build `ARG` (default `http://localhost:5000`) promoted to `ENV` before the
  build — override with `--build-arg VITE_API_BASE_URL=...` for other environments.
- Runtime stage (`nginx:alpine`): serves the static `dist/` on port 80. The SPA
  fallback nginx config is mounted at runtime, not baked into the image.
