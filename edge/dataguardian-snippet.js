export default {
  async fetch(request, env, ctx) {
    const THRESHOLD = parseFloat(env.DATAGUARDIAN_THRESHOLD ?? "8.0");
    const HEADER_PREFIX = env.DATAGUARDIAN_HEADER_PREFIX || "X-DataGuardian";
    const EXCLUDED = (env.DATAGUARDIAN_EXCLUDED_PATHS || "/health,/metrics")
      .split(",").map(s => s.trim()).filter(Boolean);

    const url = new URL(request.url);
    if (EXCLUDED.some(p => url.pathname.startsWith(p))) {
      return fetch(request);
    }

    const originResp = await fetch(request);
    const reqRisk = originResp.headers.get(`${HEADER_PREFIX}-Request-Risk`);
    const resRisk = originResp.headers.get(`${HEADER_PREFIX}-Response-Risk`);
    const risk = Math.max(parseFloat(reqRisk ?? "-1"), parseFloat(resRisk ?? "-1"));

    if (!Number.isNaN(risk) && risk >= THRESHOLD) {
      return new Response("Blocked by DataGuardian policy (edge).", { status: 403 });
    }
    return originResp;
  }
};
