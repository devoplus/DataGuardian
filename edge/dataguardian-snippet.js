/**
 * DataGuardian edge policy (Cloudflare Snippet / Worker).
 *
 * Reads the DataGuardian risk headers produced by the origin and blocks the response with 403 when the
 * risk meets the threshold.
 *
 * Environment variables:
 *   DATAGUARDIAN_THRESHOLD       Numeric threshold, e.g. "8.0" (default 8.0 when unset/invalid).
 *   DATAGUARDIAN_HEADER_PREFIX   Header prefix (default "X-DataGuardian").
 *   DATAGUARDIAN_EXCLUDED_PATHS  Comma-separated path prefixes to bypass (default "/health,/metrics").
 *   DATAGUARDIAN_FAIL_MODE       "open" (default) forwards when risk headers are missing/unparseable;
 *                                "closed" blocks in that case (recommended for regulated endpoints).
 */
export default {
  async fetch(request, env, ctx) {
    const parsedThreshold = parseNumber(env.DATAGUARDIAN_THRESHOLD);
    const THRESHOLD = parsedThreshold === null ? 8.0 : parsedThreshold;
    const HEADER_PREFIX = env.DATAGUARDIAN_HEADER_PREFIX || "X-DataGuardian";
    const EXCLUDED = (env.DATAGUARDIAN_EXCLUDED_PATHS || "/health,/metrics")
      .split(",").map(s => s.trim()).filter(Boolean);
    const FAIL_CLOSED = (env.DATAGUARDIAN_FAIL_MODE || "open").toLowerCase() === "closed";

    const url = new URL(request.url);
    if (EXCLUDED.some(p => url.pathname.startsWith(p))) {
      return fetch(request);
    }

    const originResp = await fetch(request);

    const reqRisk = parseNumber(originResp.headers.get(`${HEADER_PREFIX}-Request-Risk`));
    const resRisk = parseNumber(originResp.headers.get(`${HEADER_PREFIX}-Response-Risk`));

    // No usable risk signal at all: honor the configured fail mode instead of silently allowing.
    if (reqRisk === null && resRisk === null) {
      if (FAIL_CLOSED) {
        return new Response("Blocked by DataGuardian policy (edge, missing risk headers).", { status: 403 });
      }
      return originResp;
    }

    const risk = Math.max(reqRisk ?? -1, resRisk ?? -1);
    if (risk >= THRESHOLD) {
      return new Response("Blocked by DataGuardian policy (edge).", { status: 403 });
    }
    return originResp;
  }
};

/**
 * Parses a header/env value into a finite number, tolerating decimal commas (e.g. tr-TR "8,90").
 * Returns null when the value is missing or not numeric.
 */
function parseNumber(value) {
  if (value === null || value === undefined) return null;
  const normalized = String(value).trim().replace(",", ".");
  if (normalized === "") return null;
  const n = Number(normalized);
  return Number.isFinite(n) ? n : null;
}
