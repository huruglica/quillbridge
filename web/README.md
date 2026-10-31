# Quillbridge web

React + Vite frontend for the Quillbridge API (`../api`).

## Run

```bash
# 1. the API (from the repo root)
dotnet run --project api/quillbridge.api --launch-profile http   # http://localhost:5255

# 2. the web app
cd web
npm install
npm run dev                                                       # http://localhost:4200
```

The dev server runs on port 4200 because that is the origin the API's CORS policy allows by default,
and it proxies `/api` to `http://localhost:5255`. Copy `.env.example` to `.env` to change either target.

Seeded SDR accounts use the password `Test1234`, e.g. `maya.brennan@quillbridge.com`.
In development the login page offers them as one-click fills.

## Structure

```
src/
  lib/         api client (JWT session, error handling), auth context, formatting, theme, paging
  components/  app shell, shared UI, and the Mates login characters
  pages/       Login, CallList, Guide, DoNotCall, Import, Insights
```

Stack: React 19, React Router, TanStack Query, Motion, lucide-react, sonner. Styling is plain CSS with
design tokens in `src/styles/global.css` (light and dark).
