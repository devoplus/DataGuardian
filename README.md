# Devoplus DataGuardian — Privacy Middleware

DataGuardian, ASP.NET Core için **request ve response** gövdelerinde PII/Sensitive Data tespiti yapar, **0–10 risk skoru** üretir, isteğe bağlı olarak **response header yazar, redaksiyon yapar veya bloklar**. Türkçe ve İngilizce dillerini destekler.

- ✅ Kural tabanlı dedektörler: TCKN (checksum), IBAN, Kredi Kartı (Luhn), E-posta, Telefon, Tarih, Adres anahtar sözcükleri
- ✅ TR/EN dil tahmini veya `LanguageOverride`
- ✅ Konfigürasyon: ağırlıklar, eşikler, path/metot filtreleri, entity include/exclude, header öneki
- ✅ Aksiyon modları: **Tag**, **Redact** (MaskAll/Partial/Hash), **Block**
- ✅ Opsiyonel **BERT NER (ONNX)**: serbest metinde `PERSON/ADDRESS/...`

---

## Hızlı Başlangıç

```bash
dotnet build
dotnet test

cd samples/Devoplus.DataGuardian.SampleApi
dotnet run
# POST JSON to /echo and check headers:
#   X-DataGuardian-Request-Risk, X-DataGuardian-Response-Risk
```

### Program.cs kullanım örneği

```csharp
using Devoplus.DataGuardian;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var opt = new DataGuardianOptions
{
    AnalyzeRequests = true,
    AnalyzeResponses = true,
    HeaderPrefix = "X-DataGuardian",
    Action = ActionMode.Tag,    // None | Tag | Redact | Block
    BlockAt = -1,               // Block modunda eşik
    RedactAt = 4.0,             // Redact modunda eşik
    Redaction = RedactionStyle.MaskAll,
    IncludePaths = new() { "/api/" },
    ExcludePaths = new() { "/health", "/metrics" },
    IncludeMethods = new() { "POST", "PUT" },
    ExcludeMethods = new() { "GET" },
    EnableNer = false,          // ONNX model ekleyince true
    // LanguageOverride = "tr"
};

app.UseDataGuardian(opt);

app.MapPost("/echo", async (HttpContext ctx) =>
{
    using var sr = new StreamReader(ctx.Request.Body);
    var text = await sr.ReadToEndAsync();
    return Results.Text(text, "application/json");
});

app.Run();
```

### Header’lar
- `X-DataGuardian-Request-Risk: 0..10`
- `X-DataGuardian-Request-Detected: EMAIL=2;PHONE=1;...`
- `X-DataGuardian-Response-Risk: 0..10`
- `X-DataGuardian-Response-Detected: ...`

---

## Yapılandırma (appsettings örneği)

```jsonc
{
  "DataGuardian": {
    "AnalyzeRequests": true,
    "AnalyzeResponses": true,
    "HeaderPrefix": "X-DataGuardian",
    "EmitHeaders": true,
    "Action": "Tag",              // None | Tag | Redact | Block
    "BlockAt": 8.0,
    "RedactAt": 4.0,
    "Redaction": "MaskAll",       // MaskAll | Partial | Hash
    "IncludePaths": ["/api/"],
    "ExcludePaths": ["/health","/metrics"],
    "IncludeMethods": ["POST","PUT"],
    "ExcludeMethods": ["GET"],
    "IncludeEntityTypes": [],
    "ExcludeEntityTypes": ["ADDRESS"],
    "Weights": { "TCKN":10, "CREDIT_CARD":9, "IBAN_TR":8, "DOB":7, "ADDRESS":6, "PHONE":5, "EMAIL":4, "PERSON":3 },
    "MaxCountPerType": 5,
    "K": 0.15,
    "MaxBodySizeBytes": 524288,
    "EnableNer": false,
    "NerModelPath": "models/kvkk-ner.onnx",
    "NerTokenizerPath": "models/tokenizer.json",
    "NerLabelsPath": "models/labels.txt",
    "NerMaxSequenceLength": 256,
    "MinNerConfidence": 0.6,
    "LanguageOverride": null
  }
}
```
> `IOptions<DataGuardianOptions>` ile bağlayabilirsiniz.

---

## Cloudflare Snippet / Worker ile Edge’de Uygulama

Cloudflare üzerinde aşağıdaki snippet, **origin’den dönen** DataGuardian header’larını kontrol eder. **Hariç tutulan path’ler** (exclude) dışındaysa ve risk **eşiği aşmışsa**, **403** döndürür.

**Environment Değişkenleri:**

- `DATAGUARDIAN_THRESHOLD` - örn. `8.0`
- `DATAGUARDIAN_HEADER_PREFIX` - varsayılan `X-DataGuardian`
- `DATAGUARDIAN_EXCLUDED_PATHS` - `/health,/metrics,/public`

> Not: Header’lar origin’de üretildiğinden istek bir kez origin’e ulaşır. Request'i uygulama katmanında daha erken durdurmak isterseniz `Action = Block` + `BlockAt` ayarını kullanmanız gerekir.

```js
// edge/dataguardian-snippet.js
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
```


### Hangi yaklaşımı kullanmalıyım?

**Kısa cevap:**  
- **Sadece gözlem/uyarı** istiyorsanız → **Origin’de _Tag_ (sadece response header yazar)**  
- **Maliyet ve risk kritik** (erken kes) → **Origin’de _Block_**  
- **Merkezi politika + çok-çekirdek/origin** → **Edge’de (Cloudflare) blok**  
- **En sıkı** senaryo → **Origin’de _Block_ + Edge’de ikinci bariyer**

---

**Karşılaştırma**

| Kriter | Origin (Middleware) | Edge (Cloudflare Snippet/Worker) |
|---|---|---|
| Bloklama noktası | Uygulama katmanı (erken) | Kullanıcıya en yakın nokta |
| Uygulama maliyeti | Düşer (erken durur) | Origin’e yine gider (header okumak için) |
| Çoklu origin / ortak politika | Zor (her service ayrı ayar) | Kolay (tek yerde politika) |
| Gözlemlenebilirlik | Uygulama logları | Edge logları ile merkezi + Cloudflare Logpush desteği |
| Hata modları | Uygulama hatası etkiler | Origin hatası olsa da Edge karar verebilir |
| Streaming / SSE | İçerik değişmeden önce durdurma/redaksiyon | Çoğu zaman içerik geldikten sonra karar |
| Cache entegrasyonu | App tarafında | Cloudflare Cache ile kolay |
| Rollout / kademeli geçiş | Feature flag ile | Route/hostname bazlı çok kolay |

---

**Ne zaman hangisini seçelim?**

- **Regülasyon-kritik uçlar** (KYC, ödeme, veri ihracı):  
  → *Öncelik Origin Block.* `Action=Block`, `BlockAt=…` ile **erken kes**.  
  Gerekirse **Edge**’de de aynı eşiği uygulayıp ikinci bariyer kur.

- **Tek merkezden yönetim** (çok mikroservis, çok dil/yığın):  
  → *Öncelik Edge.* 
  Snippet/Worker ile **tek yerde politika**. Origin’de **Tag** kullanılır (response header üretir), edge üzerinde bloklama yapılabilir.
---

**Pratik ipuçları**

- **Gözlemleme:** “blok nedenleri” için Cloudflare Worker’a D1 kullanarak *log* eklenebilir.  
- **Hata toleransı:** Bir yapılandırma hatası nedeniyle origin header üretmezse Edge "risk yok" varsayabilir **veya** default-deny modunda kullanılabilir.


---

## ONNX NER (Opsiyonel)
`models/` altına:
- `kvkk-ner.onnx`
- `tokenizer.json`
- `labels.txt` (etiket listesi)

`EnableNer = true` yaparak etkinleştirin. Çıktı etiketleriniz `PERSON`, `ADDRESS`, `EMAIL`, `PHONE`, `DATE` vb. olabilir; DataGuardian bunları ağırlıklandırıp risk skoruna dahil eder.

---

## Lisans
MIT © Devoplus
