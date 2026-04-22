const http = require("http");
const fs = require("fs");
const path = require("path");
const url = require("url");

const PORT = Number(process.env.PORT || 3030);
const DATA_DIR =
  process.env.WATCH_DATA_DIR ||
  path.join(process.env.APPDATA || "", "AgileInspect");
const SESSIONS_PATH = path.join(DATA_DIR, "behavior_watch_sessions.jsonl");
const OVERVIEW_PATH = path.join(DATA_DIR, "behavior_watch_overview.json");
const PUBLIC_DIR = path.join(__dirname, "public");

function sendJson(res, status, payload) {
  res.writeHead(status, { "Content-Type": "application/json; charset=utf-8" });
  res.end(JSON.stringify(payload));
}

function readOverview() {
  if (!fs.existsSync(OVERVIEW_PATH)) {
    return {
      generatedAtUtc: null,
      totals: { started: 0, extended: 0, finished: 0, resolved: 0, stillMatched: 0 },
      byRule: {},
      _meta: { fileExists: false, path: OVERVIEW_PATH },
    };
  }

  try {
    const raw = fs.readFileSync(OVERVIEW_PATH, "utf8");
    const parsed = JSON.parse(raw);
    parsed._meta = { fileExists: true, path: OVERVIEW_PATH };
    return parsed;
  } catch (err) {
    return {
      error: `Failed to parse overview: ${err.message}`,
      _meta: { fileExists: true, path: OVERVIEW_PATH },
    };
  }
}

function readSessions(limit) {
  if (!fs.existsSync(SESSIONS_PATH)) {
    return {
      rows: [],
      _meta: { fileExists: false, path: SESSIONS_PATH, totalRows: 0 },
    };
  }

  try {
    const raw = fs.readFileSync(SESSIONS_PATH, "utf8");
    const lines = raw
      .split(/\r?\n/)
      .map((x) => x.trim())
      .filter(Boolean);

    const parsed = [];
    for (const line of lines) {
      try {
        parsed.push(JSON.parse(line));
      } catch {
        // Skip malformed lines to keep dashboard resilient.
      }
    }

    const rows = parsed.slice(-limit).reverse();
    return {
      rows,
      _meta: {
        fileExists: true,
        path: SESSIONS_PATH,
        totalRows: parsed.length,
        returnedRows: rows.length,
      },
    };
  } catch (err) {
    return {
      rows: [],
      error: `Failed to read sessions: ${err.message}`,
      _meta: { fileExists: true, path: SESSIONS_PATH, totalRows: 0 },
    };
  }
}

function serveStatic(req, res) {
  const parsed = url.parse(req.url);
  let filePath = parsed.pathname === "/" ? "/index.html" : parsed.pathname;
  filePath = path.normalize(filePath).replace(/^(\.\.[/\\])+/, "");
  const absPath = path.join(PUBLIC_DIR, filePath);

  if (!absPath.startsWith(PUBLIC_DIR)) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }

  if (!fs.existsSync(absPath) || !fs.statSync(absPath).isFile()) {
    res.writeHead(404);
    res.end("Not Found");
    return;
  }

  const ext = path.extname(absPath).toLowerCase();
  const contentType =
    ext === ".html"
      ? "text/html; charset=utf-8"
      : ext === ".css"
      ? "text/css; charset=utf-8"
      : ext === ".js"
      ? "application/javascript; charset=utf-8"
      : "application/octet-stream";

  res.writeHead(200, { "Content-Type": contentType });
  fs.createReadStream(absPath).pipe(res);
}

const server = http.createServer((req, res) => {
  const parsed = url.parse(req.url, true);

  if (parsed.pathname === "/api/overview") {
    sendJson(res, 200, readOverview());
    return;
  }

  if (parsed.pathname === "/api/sessions") {
    const limit = Math.max(10, Math.min(500, Number(parsed.query.limit || 200)));
    sendJson(res, 200, readSessions(limit));
    return;
  }

  serveStatic(req, res);
});

server.listen(PORT, () => {
  console.log(`[watch-admin-web] running on http://localhost:${PORT}`);
  console.log(`[watch-admin-web] data dir: ${DATA_DIR}`);
});
