async function getJson(path) {
  const res = await fetch(path);
  if (!res.ok) throw new Error(`${path} failed: ${res.status}`);
  return res.json();
}

function normalizeOutcome(outcome, extendCount) {
  let raw = (outcome || "").toLowerCase();
  const hasExtend = Number(extendCount || 0) > 0;
  if (raw === "still_matched") {
    raw = "ignored";
  }
  if (raw === "resolved" && hasExtend) {
    raw = "extended";
  } else if (raw === "ignored" && hasExtend) {
    raw = "deferred";
  }
  return raw;
}

function buildOutcomeTotalsFromSessions(rows) {
  const totals = { resolved: 0, ignored: 0, deferred: 0 };
  for (const s of rows || []) {
    const normalized = normalizeOutcome(s.outcome, s.extendCount);
    if (normalized === "resolved") totals.resolved += 1;
    else if (normalized === "ignored") totals.ignored += 1;
    else if (normalized === "deferred") totals.deferred += 1;
  }
  return totals;
}

function renderOutcomeClassifier(totals) {
  const resolved = totals.resolved ?? 0;
  const ignored = totals.ignored ?? 0;
  const deferred = totals.deferred ?? 0;

  document.getElementById("count-resolved").textContent = resolved;
  document.getElementById("count-ignored").textContent = ignored;
  document.getElementById("count-deferred").textContent = deferred;
}

function renderOverviewByRule(byRule) {
  const tbody = document.querySelector("#overview-table tbody");
  tbody.innerHTML = "";

  const rows = Object.entries(byRule || {}).sort((a, b) => a[0].localeCompare(b[0]));
  for (const [key, v] of rows) {
    const ruleId = v.ruleId || key;
    const desc = v.ruleDescription || "";
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td>${ruleId}</td>
      <td>${desc}</td>
      <td>${v.started ?? 0}</td>
      <td>${v.extended ?? 0}</td>
      <td>${v.finished ?? 0}</td>
      <td>${v.resolved ?? 0}</td>
      <td>${v.deferred ?? 0}</td>
      <td>${(v.ignored ?? 0) + (v.stillMatched ?? 0)}</td>
    `;
    tbody.appendChild(tr);
  }
}

function typeBadge(type) {
  const raw = type || "";
  const text = raw.replace("watch_", "");
  let cls = "type-other";
  if (raw === "watch_started") cls = "type-started";
  if (raw === "watch_extended") cls = "type-extended";
  if (raw === "watch_finished") cls = "type-finished";
  return `<span class="badge ${cls}">${text || "-"}</span>`;
}

function outcomeBadge(outcome, extendCount) {
  const raw = normalizeOutcome(outcome, extendCount);
  if (!raw) return "";
  const cls =
    raw === "resolved"
      ? "outcome-complied"
      : raw === "extended"
      ? "outcome-extended"
      : raw === "deferred"
      ? "outcome-deferred"
      : raw === "ignored"
      ? "outcome-ignored"
      : "type-other";
  return `<span class="badge ${cls}">${raw}</span>`;
}

function renderSessions(rows) {
  const tbody = document.querySelector("#sessions-table tbody");
  tbody.innerHTML = "";

  const grouped = new Map();
  for (const s of rows || []) {
    const ruleId = s.ruleId || "unknown_rule";
    if (!grouped.has(ruleId)) grouped.set(ruleId, []);
    grouped.get(ruleId).push(s);
  }

  const sortedRuleIds = Array.from(grouped.keys()).sort((a, b) => a.localeCompare(b));
  for (const ruleId of sortedRuleIds) {
    const header = document.createElement("tr");
    header.className = "group-row";
    header.innerHTML = `<td colspan="4">Rule: ${ruleId} (${grouped.get(ruleId).length} sessions)</td>`;
    tbody.appendChild(header);

    const sessions = grouped.get(ruleId).sort((a, b) => {
      const at = new Date(a.timestampUtc || 0).getTime();
      const bt = new Date(b.timestampUtc || 0).getTime();
      return bt - at;
    });

    for (const s of sessions) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td>${s.timestampUtc || ""}</td>
        <td>${typeBadge(s.type)}</td>
        <td>${s.conditionSummary || ""}</td>
        <td>${outcomeBadge(s.outcome, s.extendCount)}</td>
      `;
      tbody.appendChild(tr);
    }
  }
}

async function refresh() {
  try {
    const [overview, sessions] = await Promise.all([
      getJson("/api/overview"),
      getJson("/api/sessions?limit=200"),
    ]);

    const sessionTotals = buildOutcomeTotalsFromSessions(sessions.rows || []);
    renderOutcomeClassifier(sessionTotals);
    renderOverviewByRule(overview.byRule || {});
    renderSessions(sessions.rows || []);

    const stamp = new Date().toISOString();
    document.getElementById("last-refresh").textContent = `Last refresh: ${stamp}`;
  } catch (err) {
    document.getElementById("last-refresh").textContent = `Error: ${err.message}`;
  }
}

refresh();
setInterval(refresh, 5000);
