# Edge Dağıtımı (Cloudflare Snippet / Worker)

`edge/dataguardian-snippet.js`, origin'den dönen DataGuardian risk header'larını okuyup eşik aşıldığında yanıtı 403 ile bloklar. Origin'de `Action = Tag` (veya `Block`) ile header üretin, edge'de merkezi politikayı uygulayın.

## Ortam değişkenleri

| Değişken | Örnek | Açıklama |
|---|---|---|
| `DATAGUARDIAN_THRESHOLD` | `8.0` | Sayısal eşik. Geçersiz/eksikse `8.0` varsayılır. |
| `DATAGUARDIAN_HEADER_PREFIX` | `X-DataGuardian` | Header öneki (origin'deki `HeaderPrefix` ile aynı olmalı). |
| `DATAGUARDIAN_EXCLUDED_PATHS` | `/health,/metrics,/public` | Bypass edilecek path önekleri. |
| `DATAGUARDIAN_FAIL_MODE` | `open` / `closed` | Risk header'ları yok/geçersizse davranış. Varsayılan `open`. |

## Fail-open vs fail-closed

Risk header'ları hiç yoksa (middleware devre dışı, `EmitHeaders=false`, önbellekten dönen yanıt, origin hata sayfası vb.):

- `open` (varsayılan): yanıt geçirilir. Gözlem/uyarı senaryoları için.
- `closed`: yanıt 403 ile bloklanır. **Regülasyon-kritik uçlar için önerilir.**

## Kültür / ondalık ayıracı

Origin risk değerlerini `InvariantCulture` ile (`8.90` gibi, nokta ondalık) yazar. Snippet yine de savunmacı olarak virgüllü değerleri (`8,90`) noktaya çevirip ayrıştırır; böylece farklı locale'lerde eşik yanlış okunmaz.

## Snippet

Güncel kaynak: [`edge/dataguardian-snippet.js`](../edge/dataguardian-snippet.js).

```js
export default {
  async fetch(request, env, ctx) {
    const parsedThreshold = parseNumber(env.DATAGUARDIAN_THRESHOLD);
    const THRESHOLD = parsedThreshold === null ? 8.0 : parsedThreshold;
    const HEADER_PREFIX = env.DATAGUARDIAN_HEADER_PREFIX || "X-DataGuardian";
    const EXCLUDED = (env.DATAGUARDIAN_EXCLUDED_PATHS || "/health,/metrics")
      .split(",").map(s => s.trim()).filter(Boolean);
    const FAIL_CLOSED = (env.DATAGUARDIAN_FAIL_MODE || "open").toLowerCase() === "closed";

    const url = new URL(request.url);
    if (EXCLUDED.some(p => url.pathname.startsWith(p))) return fetch(request);

    const originResp = await fetch(request);
    const reqRisk = parseNumber(originResp.headers.get(`${HEADER_PREFIX}-Request-Risk`));
    const resRisk = parseNumber(originResp.headers.get(`${HEADER_PREFIX}-Response-Risk`));

    if (reqRisk === null && resRisk === null) {
      return FAIL_CLOSED
        ? new Response("Blocked by DataGuardian policy (edge, missing risk headers).", { status: 403 })
        : originResp;
    }

    const risk = Math.max(reqRisk ?? -1, resRisk ?? -1);
    return risk >= THRESHOLD
      ? new Response("Blocked by DataGuardian policy (edge).", { status: 403 })
      : originResp;
  }
};
```

## Origin mi, edge mi?

| Kriter | Origin (Middleware) | Edge (Cloudflare) |
|---|---|---|
| Bloklama noktası | Uygulama katmanı (erken) | Kullanıcıya en yakın nokta |
| Uygulama maliyeti | Düşer (erken durur) | Origin'e yine gider (header okumak için) |
| Çoklu origin / ortak politika | Zor (her servis ayrı) | Kolay (tek yerde politika) |
| Streaming / SSE | İçerik değişmeden önce karar | Çoğu zaman içerik geldikten sonra |

**Kısaca:**
- Sadece gözlem → Origin `Tag`.
- Maliyet/risk kritik → Origin `Block`.
- Merkezi politika → Edge blok.
- En sıkı → Origin `Block` + Edge fail-closed ikinci bariyer.

> `X-DataGuardian-*-Detected` header'larını dış istemcilere sızdırmayın; edge'de güven sınırından önce temizlemeyi düşünün (bkz. [security.md](security.md)).
