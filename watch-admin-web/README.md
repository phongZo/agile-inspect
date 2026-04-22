# Watch Admin Web

Simple Node.js dashboard to help admins understand behavior watch data.

## What it shows

- Watch overview summary from:
  - `%APPDATA%/AgileInspect/behavior_watch_overview.json`
- Recent watch sessions from:
  - `%APPDATA%/AgileInspect/behavior_watch_sessions.jsonl`

## Run

From this folder:

```bash
npm start
```

Open:

`http://localhost:3030`

## Optional environment variables

- `PORT` (default `3030`)
- `WATCH_DATA_DIR` (default `%APPDATA%/AgileInspect`)
