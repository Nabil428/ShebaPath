# ShebaPath — Bangladesh Services Portal

Government-service guide website for Bangladesh (driving licence, passport,
NID, birth certificate, trade licence guides + a small blog + account system).

## Structure

- `frontend/` — Angular 20 app (standalone components, signals)
- `backend/`  — ASP.NET Core Web API (C#), Npgsql direct (no ORM), cookie-based
  auth (`bd_session`), BCrypt password hashing

## Backend

- Runs on port 8000 by default
- Connects directly to PostgreSQL via Npgsql (no EF Core)
- Tables used: `bd_users`, `bd_guides`, `bd_blog_posts` (all `bd_`-prefixed to
  avoid collisions if sharing a database with other projects)

## Frontend

- Angular CLI app (`ng serve` / `ng build`)
- Dev proxy: `/bd-services/api/*` → `http://localhost:8000` (see `proxy.conf.json`)
- Base href is hardcoded as `/bd-services/` in `src/index.html` — change this
  if you're not deploying under that sub-path
- Run dev server with: `ng serve --proxy-config proxy.conf.json`

## Known limitation (carried over from original project)

Production deployment isn't fully wired — the frontend build has no
server-side proxy to the backend out of the box. You'll need a reverse proxy
(Nginx, IIS URL Rewrite, etc.) or to merge the API behind the same host in
production.

## Setup

1. Create the PostgreSQL tables (`bd_users`, `bd_guides`, `bd_blog_posts`) —
   check `backend/` for SQL/seed scripts if present.
2. Update the connection string / config in `backend/` (appsettings or env vars).
3. `cd backend && dotnet run`
4. `cd frontend && npm install && ng serve --proxy-config proxy.conf.json`
