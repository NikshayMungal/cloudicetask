# Waybill: Logistics Shipment Tracker

> CLDV6212 group project: a containerised full-stack web app for regional freight dispatchers.

**Live app:** `https://REPLACE-ME.onrender.com` *(free tier: the first request after a quiet period can take up to a minute)*
**API docs (Swagger):** `https://REPLACE-ME-api.onrender.com/swagger`
**Pipeline status:** `![CI/CD](https://github.com/OWNER/REPO/actions/workflows/ci-cd.yml/badge.svg)`

---

## 1. The problem

Regional freight and delivery companies often run dispatch from spreadsheets, WhatsApp groups and phone calls. That creates three recurring failures:

| Failure | What it looks like on the ground |
|---|---|
| **Lost or stale records** | A status lives in one coordinator's spreadsheet or chat history; nobody else can tell where a load is. |
| **Unallocated loads** | A booked shipment has no driver or vehicle until someone notices, and the delivery slips. |
| **Late discovery of delays** | Overdue deliveries are found when the customer phones, not when the estimated date passes. |

> **TODO (group):** Replace the table above with your own research. Add 3-5 cited sources (industry reports, news articles, interviews with a real dispatcher) and one or two figures on the cost of late or lost deliveries. Do not submit unsourced statistics.

### Target users

- **Dispatch managers**, who need a live picture of every open shipment and what needs attention today.
- **Logistics coordinators**, who create shipments, allocate drivers and vehicles, and update statuses as loads move.

### How Waybill helps

| Need | Feature |
|---|---|
| One source of truth | Every shipment is stored in a hosted PostgreSQL database with a unique tracking number. |
| See problems early | The dashboard counts **overdue** shipments and loads that **need a driver**; each is one click from the filtered list. |
| Faster coordination | Change status from a dropdown in the table, or allocate a driver and vehicle from the edit panel. |
| Find anything | Search by tracking number, customer, town or driver, and filter by status. |
| Trustworthy data | Validation runs in the browser *and* on the API; records survive restarts and redeploys. |

---

## 2. Architecture

```mermaid
flowchart LR
    U[Dispatcher's browser] -- HTTPS --> FE["Frontend container<br/>React + Vite build served by nginx"]
    U -- "HTTPS (JSON, CORS-restricted)" --> BE["Backend container<br/>ASP.NET Core 8 Web API + Swagger"]
    BE -- "TLS, EF Core / Npgsql" --> DB[("Hosted PostgreSQL<br/>Neon or Supabase free tier")]
    GH[GitHub Actions] -- "build, test, scan, push" --> REG[(GitHub Container Registry)]
    REG -- "image pulled on deploy hook" --> FE
    REG --> BE
```

**Local (`docker compose`)**: three containers on one Docker network. The browser talks only to nginx on port 3000. nginx serves the React app and forwards `/api/*` to the backend using the network alias `backend`. The backend reaches PostgreSQL using the alias `db`. Data lives in a named volume.

**Production (Render + Neon)**: the frontend and backend are two separate containerised web services. The frontend's `API_BASE_URL` points at the backend's HTTPS URL, and the backend allows only that origin through CORS. The database is a hosted free-tier PostgreSQL instance, so data survives container restarts and redeploys.

### Key design decisions

- **ASP.NET Core Web API** (not Azure Functions): straightforward to containerise and deploy on any Docker host.
- **PostgreSQL + EF Core**: relational data suits shipments; the same code runs against the local container and Neon/Supabase.
- **Runtime frontend config**: `/config.js` is written when the container starts, so one image can be pointed at any backend without rebuilding.
- **No secrets in the repo**: connection strings and deploy hooks come from environment variables and GitHub Secrets.

---

## 3. Repository layout

```
.
├── backend/
│   ├── Dockerfile
│   ├── LogisticsTracker.sln
│   ├── LogisticsTracker.Api/          # Program.cs, Controllers, Models, Dtos, Data
│   └── LogisticsTracker.Tests/        # xUnit tests (EF Core in-memory)
├── frontend/
│   ├── Dockerfile
│   ├── nginx.conf.template
│   ├── docker-entrypoint.d/           # writes /config.js at container start
│   └── src/                           # App, components/, api/shipmentsApi.js
├── .github/
│   ├── workflows/ci-cd.yml
│   └── dependabot.yml
├── docker-compose.yml
├── .env.example
└── .gitignore
```

---

## 4. Run it locally

**Prerequisites:** Docker Desktop (or Docker Engine with the Compose plugin) and Git. Nothing else is needed; .NET and Node run inside the containers.

```bash
git clone https://github.com/OWNER/REPO.git
cd REPO
cp .env.example .env          # Windows PowerShell: Copy-Item .env.example .env
docker compose up --build
```

| What | URL |
|---|---|
| Web app | http://localhost:3000 |
| Swagger UI | http://localhost:5000/swagger |
| Health check | http://localhost:5000/health/db |

Demo shipments are inserted automatically when the table is empty (`SEED_DEMO_DATA=true`).
Stop with `Ctrl+C`; remove containers with `docker compose down`, or also wipe the database with `docker compose down -v`.

> Locally the app runs on plain HTTP. In production, Render terminates HTTPS and the browser only ever talks to `https://` URLs.

### Without Docker (optional)

```bash
# terminal 1: needs a PostgreSQL instance and the .NET 8 SDK
cd backend/LogisticsTracker.Api
export ConnectionStrings__Default="Host=localhost;Database=logistics;Username=logistics;Password=change-me-locally"
export SeedDemoData=true
dotnet run --urls http://localhost:5000

# terminal 2: needs Node 20
cd frontend && npm install && npm run dev      # http://localhost:5173 (proxies /api to :5000)
```

### Run the tests

```bash
dotnet test backend/LogisticsTracker.sln
```

---

## 5. Environment variables

`.env.example` lists every variable with dummy values. Copy it to `.env`, which is git-ignored.

| Variable | Used by | Purpose | Local default |
|---|---|---|---|
| `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` | db, backend | Local database credentials | `logistics` / `change-me-locally` / `logistics` |
| `ASPNETCORE_ENVIRONMENT` | backend | `Development` or `Production` | `Development` |
| `CORS_ALLOWED_ORIGINS` | backend | Comma-separated origins allowed to call the API (`Cors__AllowedOrigins`) | `http://localhost:3000` |
| `SEED_DEMO_DATA` | backend | Insert sample shipments when empty | `true` |
| `DATABASE_URL` | backend (**production**) | Hosted DB URI: `postgres://user:pass@host/db?sslmode=require` | *(unset)* |
| `API_BASE_URL` | frontend | Backend's public URL. **Empty locally** (nginx proxies `/api`). | *(empty)* |
| `BACKEND_UPSTREAM` | frontend | Where nginx forwards `/api/*` | `http://backend:8080` |
| `PORT` | both | Listen port (hosting platforms set this) | `8080` |

---

## 6. API reference

Interactive docs are served at `/swagger` on the backend.

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/shipments?search=&status=&unassigned=` | List and filter shipments |
| GET | `/api/shipments/summary` | Dashboard counts (per status, unassigned, overdue) |
| GET | `/api/shipments/{id}` | One shipment |
| POST | `/api/shipments` | Create (tracking number generated if omitted) |
| PUT | `/api/shipments/{id}` | Update details |
| PATCH | `/api/shipments/{id}/status` | Change status only |
| PATCH | `/api/shipments/{id}/driver` | Allocate driver and vehicle |
| DELETE | `/api/shipments/{id}` | Delete |
| GET | `/health`, `/health/db` | Liveness and database reachability |

Validation errors return `400` with an `errors` object per field; a duplicate tracking number returns `409`.

---

## 7. CI/CD pipeline

`.github/workflows/ci-cd.yml` runs on every pull request to `main` and every push to `main`.

| Job | What it does |
|---|---|
| **backend** | Restore, build, run xUnit tests, fail on vulnerable NuGet packages |
| **frontend** | `npm ci`, production build, `npm audit` (high severity) |
| **codeql** | CodeQL static analysis for C# and JavaScript |
| **docker** | Builds each image, scans it with **Trivy** (fails on fixable HIGH/CRITICAL), pushes to **GHCR** on `main` |
| **compose** | `docker compose build`, starts the stack, checks the API and frontend respond |
| **deploy** | `main` only: triggers Render deploy hooks with the new image tag, then smoke-tests the live site |

Pull requests run everything except pushing images and deploying.

---

## 8. Deploying on free tiers (no credit card)

> Free-tier terms change. Check each provider's pricing page and confirm that none asks for payment details before you sign up.

### Step 1: Database (Neon or Supabase)
1. Create a free project and copy the **PostgreSQL connection URI**.
2. Keep it secret. It becomes `DATABASE_URL` on the backend service.

Avoid Render's free Postgres for the final deployment: it expires after a fixed period. Also check whether your provider pauses idle databases, and wake it before presenting.

### Step 2: Publish the images
Merge your first PR to `main`. The `docker` job pushes `ghcr.io/<owner>/<repo>-backend` and `-frontend`.
On GitHub, open **Your profile → Packages**, select each package, then **Package settings → Change visibility → Public** so Render can pull it without credentials.

### Step 3: Create two Render web services
For each, choose **New → Web Service → Existing image** and select the **Free** instance type.

| Service | Image | Environment variables |
|---|---|---|
| Backend | `ghcr.io/<owner>/<repo>-backend:latest` | `DATABASE_URL`, `Cors__AllowedOrigins=https://<frontend>.onrender.com`, `ASPNETCORE_ENVIRONMENT=Production`, `SeedDemoData=true` (first deploy only, so the demo has data) |
| Frontend | `ghcr.io/<owner>/<repo>-frontend:latest` | `API_BASE_URL=https://<backend>.onrender.com` |

Set each service's health check path (backend: `/health`).
Then copy each service's **Deploy Hook URL** from its Settings page.

### Step 4: GitHub secrets and variables
In **Settings → Secrets and variables → Actions**:

| Type | Name | Value |
|---|---|---|
| Secret | `RENDER_BACKEND_DEPLOY_HOOK` | Backend deploy hook URL |
| Secret | `RENDER_FRONTEND_DEPLOY_HOOK` | Frontend deploy hook URL |
| Variable (optional) | `BACKEND_URL`, `FRONTEND_URL` | Live URLs for the smoke test |

Also create an Environment named **production** (Settings → Environments) used by the deploy job.
`GITHUB_TOKEN` is provided automatically; no registry password is needed.

### Before you present
Open both live URLs and `/health/db` a few minutes early so the sleeping services and database are awake.

---

## 9. Git workflow

- `main` is protected: **Settings → Branches → Add rule** for `main` with *Require a pull request before merging*, *Require approvals (1)*, and *Require status checks to pass* (select the CI jobs).
- Branch naming: `feature/<short-description>`, `fix/<short-description>`, `docs/<short-description>`.
- Every member commits **under their own GitHub account**, opens their own PRs, and reviews at least one teammate's PR.
- Turn on **Dependabot alerts** and **Secret scanning** under **Settings → Code security**.

---

## 10. Group members

| Member | Student no. | GitHub | Main contributions |
|---|---|---|---|
| *Name* | *ST...* | [@username](https://github.com/username) | *e.g. backend API and unit tests* |
| *Name* | *ST...* | [@username](https://github.com/username) | *e.g. React UI and validation* |
| *Name* | *ST...* | [@username](https://github.com/username) | *e.g. Docker and CI/CD pipeline* |
| *Name* | *ST...* | [@username](https://github.com/username) | *e.g. research, database and deployment* |

---

## 11. Known limitations and next steps

- **No login yet.** Anyone with the URL can read and edit shipments. A natural next step is authentication (e.g. Supabase Auth or ASP.NET Identity) with dispatcher and read-only roles.
- **Schema is created with `EnsureCreated`.** For schema changes after launch, switch to EF Core migrations (`dotnet ef migrations add Initial`, then `Database.MigrateAsync()`).
- Possible features: driver management screen, customer-facing tracking page, delay notifications by email or SMS (free-tier providers), map view of routes.
