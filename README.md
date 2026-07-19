# MusiQL

MusiQL is a web app where a playlist is a saved query rather than a hand-curated list. You describe what you want — say, grunge and alternative rock from 1990 to 2004, minus Nirvana — and MusiQL stores that definition and re-runs it against a music catalog every time the playlist is opened, so the result stays live as the catalog grows. There are two ways to write a query: a visual builder for casual use, and an advanced mode where fluent users write MQL, a small purpose-built query language. Users never write raw SQL; MQL is parsed to an AST on the server, validated against a whitelisted schema, and compiled to parameterized SQL.

The catalog is built from MusicBrainz PostgreSQL dumps, trimmed by an ETL pipeline into an app-owned schema holding just what queries need: artists, releases, recordings, genres, and dates. The backend is an ASP.NET Core (.NET 10) API over PostgreSQL; the frontend is React (Vite + TypeScript). Later phases add authentication, playlist management, and export to Spotify. This repository currently contains the Phase 1 scaffold: the solution layout, a Dockerized Postgres for development, the React app, and a health-check endpoint wired end to end.

## Running locally

Prerequisites: .NET 10 SDK, Node 20+, Docker.

```sh
cp .env.example .env          # dev database credentials
docker compose up -d          # Postgres 17 on localhost:5442
dotnet test                   # backend tests
dotnet run --project src/MusiQL.Api   # API on http://localhost:5272
```

In a second terminal:

```sh
cd frontend
npm install
npm run dev                   # app on http://localhost:5173
```

Open http://localhost:5173 — the page reports API and database health. `npm test` runs the frontend test suite.

## Layout

| Path | Purpose |
| --- | --- |
| `src/MusiQL.Api` | ASP.NET Core Web API (minimal hosting) |
| `src/MusiQL.Core` | Domain model and MQL query engine |
| `src/MusiQL.Data` | EF Core / Npgsql data access |
| `src/MusiQL.Etl` | MusicBrainz catalog ETL (console) |
| `tests/MusiQL.Tests` | xUnit backend tests |
| `frontend` | React + Vite + TypeScript client |
