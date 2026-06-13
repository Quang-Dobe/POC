# POC.Agent

A small Python (FastAPI) HTTP service — the "Agent" — that answers natural-language
questions about NYC taxi data. It runs an LLM tool-calling loop against an Azure AI
Foundry model and fetches the underlying data, read-only, from Data API Builder (DAB).

The Agent sits behind the BFF (`POC.BFF`): the BFF mints a short-lived internal JWT,
forwards it as the `Authorization: Bearer` header, and streams the Agent's response back
to the front end. The Agent never mints or exchanges tokens — it validates the inbound
bearer and forwards that same token verbatim to DAB.

## What it does

1. Receives a question on `/ask` (single response) or `/ask/stream` (Server-Sent Events).
2. Validates the inbound Bearer token against the BFF's JWKS endpoint (RS256, issuer +
   audience enforced). External / non-BE tokens are rejected with a loud, grep-able
   `EXTERNAL_TOKEN_REJECTED no-fallback` log marker.
3. Runs a tool-calling loop with the Foundry model. When the model calls the `query_dab`
   tool, the Agent issues an OData query to DAB, passing the same bearer token and an
   `X-MS-API-ROLE` header derived from the token's `roles` claim.
4. Returns the model's answer (with a `session_id`) or streams the answer chunk by chunk.

## Modules (`src/`)

| File | Responsibility |
|------|----------------|
| `main.py` | FastAPI app and entrypoint. Defines `/health`, `/ask`, `/ask/stream`. Authenticates each request (`_authenticate`), wires config + clients at import time, and runs the app via uvicorn under `__main__`. |
| `auth.py` | JWT/JWKS verification. `validate_token` fetches signing keys via `PyJWKClient` (cached per JWKS URL) and validates RS256 signature, issuer, and audience. Distinguishes `ExternalTokenError` (unknown kid / wrong issuer / wrong audience / bad signature — a non-BE token) from `MalformedTokenError` (structurally broken or expired BE token). |
| `agent.py` | Azure Foundry client factories (`make_project_client` via `ClientSecretCredential`, `make_openai_client`) plus the `query_dab` tool definition and the system instructions describing the NYC-taxi entities (Trip, Date, Geography, Weather). |
| `runner.py` | The tool-calling loop. `run_agent` (non-streaming) and `run_agent_stream` (SSE) share one tool-execution path (`_append_tool_round`). Streaming reassembles tool-call deltas across chunks. Both cap at `_MAX_TOOL_ROUNDS = 10`. |
| `dab_client.py` | `query_dab`: builds the OData query string into the URL (with `$` left unencoded, because DAB rejects `%24`), calls DAB with the bearer token + `X-MS-API-ROLE`, and returns the JSON body as a string. `first` is clamped to 1..50. |
| `config.py` | `Config` dataclass + `load_config`. Reads environment variables, fails fast if any required one is missing, and exposes `dab_base_url` (computed from host + port). |

## Relationship to the BFF

```
Front end ──▶ POC.BFF ──▶ POC.Agent ──▶ POC.DAB ──▶ database
                 │            │
          mints internal   validates bearer (JWKS from the BFF),
          JWT, forwards     forwards the SAME bearer + X-MS-API-ROLE to DAB
          as Bearer
```

- **Auth:** The BFF is the issuer. The Agent trusts only tokens signed by the BFF's JWKS
  key (`AUTH_JWKS_URL`), with the expected issuer (`AUTH_AUTHORITY`) and audience
  (`AUTH_API_AUDIENCE`). No fallback to external IDPs.
- **No token exchange:** The validated raw bearer is forwarded to DAB unchanged.
- **DAB access:** Read-only OData queries; the token's first `roles` claim (default
  `reader`) becomes the `X-MS-API-ROLE` header DAB uses for row/role authorization.

## Configuration (environment variables)

Read by `config.py`. The service raises `ValueError` at startup if any **required** var is
missing.

| Variable | Required | Default | Purpose |
|----------|----------|---------|---------|
| `ENVIRONMENT` | no | `Development` | `Development` disables TLS verification on the JWKS fetch. |
| `PORT` | no | `8080` | Port the app binds when run as `__main__`. |
| `AUTH_AUTHORITY` | yes | — | Expected token issuer (`iss`). |
| `AUTH_API_AUDIENCE` | yes | — | Expected token audience (`aud`). |
| `AUTH_JWKS_URL` | yes | — | JWKS endpoint used to fetch signing keys. |
| `AZ_FOUNDRY_PROJECT_ENDPOINT` | yes | — | Azure AI Foundry project endpoint. |
| `AZ_FOUNDRY_MODEL_DEPLOYMENT_NAME` | yes | — | Model deployment name passed to chat completions. |
| `API_VERSION` | no | `2025-05-01` | Foundry API version. |
| `DAB_HOST` | yes | — | DAB host; combined with `DAB_PORT` into `dab_base_url`. |
| `DAB_PORT` | yes | — | DAB port. |
| `AZ_FOUNDRY_TENANT_ID` | yes | — | Service-principal tenant for `ClientSecretCredential`. |
| `AZ_FOUNDRY_SP_CLIENT_ID` | yes | — | Service-principal client id. |
| `AZ_FOUNDRY_SP_CLIENT_SECRET` | yes | — | Service-principal client secret. |

## Running

### Locally

```bash
pip install -r requirements.txt
python -m uvicorn src.main:app --host 0.0.0.0 --port 8080
```

(`src/main.py` also runs directly via `python -m src.main`, which calls
`uvicorn.run("src.main:app", ...)` on the configured `PORT`.)

### Docker

```bash
docker build -t poc-agent .
docker run -p 8080:8080 --env-file .env poc-agent
```

The `Dockerfile` is based on `python:3.12-slim`, installs `requirements.txt`, copies
`src/`, exposes `8080`, and runs uvicorn.

### docker-compose

`docker-compose.yml` defines the `agent-gateway` service. It maps `AGENT_PORT` (default
`8082`) to the container's `8080`, points `AUTH_JWKS_URL` at the BFF on the host
(`host.docker.internal:${BE_PORT}`), and wires `DAB_HOST`/`DAB_PORT` plus the Foundry
service-principal and project settings from the surrounding environment.

```bash
docker compose up --build
```

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health` | none | Liveness check — returns `{"status": "ok"}`. |
| POST | `/ask` | Bearer | `{ "question", "session_id?" }` → `{ "answer", "session_id" }`. |
| POST | `/ask/stream` | Bearer | Same body; streams the answer as `text/event-stream` SSE chunks. |

Auth failures return `401`. `/ask` returns `504` on agent timeout and `500` on other
agent errors.

## Tests

```bash
pip install -r requirements-dev.txt
pytest
```

Tests cover config loading, JWT/JWKS validation (with a runtime-generated RSA keypair),
the DAB client URL/header construction, the FastAPI endpoints, and the tool-calling loop
for both the streaming and non-streaming paths.
