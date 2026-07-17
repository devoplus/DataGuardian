# Devoplus DataGuardian - Privacy Middleware

[![NuGet Version](https://img.shields.io/nuget/v/Devoplus.DataGuardian)](https://www.nuget.org/packages/Devoplus.DataGuardian) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/devoplus/DataGuardian/dotnet.yml) ![GitHub License](https://img.shields.io/github/license/devoplus/DataGuardian) ![Devoplus Open Source](https://img.shields.io/badge/Open_Source-DP?label=Devoplus&labelColor=%23B60017&color=%2319191a)

DataGuardian, ASP.NET Core için **request ve response** gövdelerinde PII/hassas veri tespiti yapar, **0–10 risk skoru** üretir, isteğe bağlı olarak **response header yazar, redaksiyon yapar veya bloklar**. Türkçe ve İngilizce dillerini destekler.

- ✅ Kural tabanlı dedektörler: TCKN (checksum), IBAN (mod-97), Kredi Kartı (Luhn + şema), E-posta, Telefon, Tarih, Adres anahtar sözcükleri
- ✅ TR/EN dil tahmini veya `LanguageOverride`
- ✅ Konfigürasyon: ağırlıklar, eşikler, path/metot filtreleri, entity include/exclude, header öneki
- ✅ Aksiyon modları: **Tag**, **Redact** (MaskAll/Partial/Hash), **Block**
- ✅ Opsiyonel **BERT NER (ONNX)**: serbest metinde `PERSON/ADDRESS/...`

> Ayrıntılı belgeler [`docs/`](docs/) klasöründedir. Değişiklik geçmişi için [`CHANGELOG.md`](CHANGELOG.md).

---

## Hızlı Başlangıç

```bash
dotnet build
dotnet test

cd samples/Devoplus.DataGuardian.SampleApi
dotnet run
# POST JSON to /echo and check the response headers:
#   X-DataGuardian-Request-Risk, X-DataGuardian-Response-Risk
```

### Kullanım (DI + konfigürasyon)

Önerilen yol, seçenekleri `appsettings.json`'daki `DataGuardian` bölümünden bağlamaktır:

```csharp
using Devoplus.DataGuardian;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataGuardian(builder.Configuration.GetSection("DataGuardian"));

var app = builder.Build();
app.UseDataGuardian();   // AddDataGuardian ile kaydedilen seçenekleri çözer

app.MapPost("/echo", async (HttpContext ctx) =>
{
    using var sr = new StreamReader(ctx.Request.Body);
    var text = await sr.ReadToEndAsync();
    return Results.Text(text, "application/json");
});

app.Run();
```

### Kullanım (kod içinde inline)

```csharp
app.UseDataGuardian(o =>
{
    o.Action = ActionMode.Tag;    // None | Tag | Redact | Block
    o.BlockAt = 8.0;              // Block modunda eşik
    o.RedactAt = 4.0;             // Redact modunda eşik
    o.Redaction = RedactionStyle.MaskAll;
    o.ExcludePaths = new() { "/health", "/metrics" };
    o.EnableNer = false;          // ONNX model ekleyince true
    // o.LanguageOverride = "tr";
});
```

> Geçersiz bir konfigürasyon (`K <= 0`, `MaxCountPerType < 1`, eşiklerin aralık dışı olması vb.) başlangıçta `ArgumentException` ile **erken** hata verir; sessizce yok sayılmaz.

### Header’lar
- `X-DataGuardian-Request-Risk: 0..10` (nokta ondalık, kültürden bağımsız)
- `X-DataGuardian-Request-Detected: EMAIL=2;PHONE=1;...`
- `X-DataGuardian-Response-Risk: 0..10`
- `X-DataGuardian-Response-Detected: ...`
- `X-DataGuardian-Response-Skip-Reason: ...` (ör. `ContentTypeNotAnalyzable`, `BodyTooLarge`, `UnsupportedCharset`, `NoRedactableTypes`)

> **Güvenlik notu:** `-Detected` header’ları hangi PII tiplerinin bulunduğunu açığa vurur. Bunları yalnızca güvenilir iç tüketicilere (ör. edge) verin; dış istemcilere dönen yanıtlarda `EmitHeaders = false` yapın veya güven sınırında temizleyin. Ayrıntı: [`docs/security.md`](docs/security.md).

### Postman ile test örneği
Projede yer alan **Devoplus.DataGuardian.SampleApi** projesini başlatarak Postman üzerinden veri göndererek header'ları test edebilirsiniz.
![Postman ekran görüntüsü](https://github.com/devoplus/DataGuardian/raw/main/assets/postman_ss.png "Postman")

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
    "ExcludePaths": ["/health", "/metrics"],
    "IncludeMethods": ["POST", "PUT"],
    "ExcludeMethods": ["GET"],
    "IncludeEntityTypes": [],
    "ExcludeEntityTypes": ["ADDRESS"],
    "Weights": { "TCKN": 10, "CREDIT_CARD": 9, "IBAN": 8, "DOB": 7, "ADDRESS": 6, "PHONE": 5, "EMAIL": 4, "PERSON": 3 },
    "MaxCountPerType": 5,
    "K": 0.15,
    "MaxBodySizeBytes": 524288,
    "BlockOversizeBodies": false,
    "DefaultPhoneRegion": "TR",
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

Tüm seçeneklerin ayrıntısı: [`docs/configuration.md`](docs/configuration.md).

> **Not:** Entity tip anahtarları tek bir kaynakta (`PiiTypes`) tanımlıdır. `Weights`, `RedactTypes` ve tanıyıcı çıktıları aynı anahtarları kullanır (ör. IBAN için `IBAN`). Bir birim testi bu tutarlılığı sabitler.

---

## Belgeler

| Belge | İçerik |
|---|---|
| [docs/configuration.md](docs/configuration.md) | Tüm seçeneklerin referansı |
| [docs/recognizers.md](docs/recognizers.md) | Tanıyıcılar, ne tespit eder, doğrulama |
| [docs/scoring.md](docs/scoring.md) | Risk skoru formülü |
| [docs/actions.md](docs/actions.md) | Tag / Redact / Block ve redaksiyon stilleri |
| [docs/security.md](docs/security.md) | Güvenlik modeli, sınırlar ve öneriler |
| [docs/edge.md](docs/edge.md) | Cloudflare Snippet/Worker ile edge dağıtımı |
| [docs/contributing.md](docs/contributing.md) | Geliştirme, test, yeni tanıyıcı ekleme |

---

## ONNX NER (Opsiyonel)
`models/` altına `kvkk-ner.onnx`, `tokenizer.json` ve `labels.txt` ekleyip `EnableNer = true` yaparak etkinleştirin. Çıktı etiketleriniz `PERSON`, `ADDRESS`, `EMAIL`, `PHONE`, `DATE` vb. olabilir; DataGuardian bunları ağırlıklandırıp risk skoruna dahil eder. NER entegrasyonunun bilinen sınırları ve olgunluk durumu için [`docs/recognizers.md`](docs/recognizers.md#ner-onnx) bölümüne bakın.

---

## Roadmap

DataGuardian için planlanan geliştirmeler ve hedefler:

### v1.x (Mevcut)
- [x] Kural tabanlı PII tespitleri (TCKN, IBAN, kredi kartı, e-posta, telefon, tarih, adres)
- [x] Risk skorlaması (0–10) ve header üretimi (`X-DataGuardian-*`)
- [x] Aksiyon modları: **Tag**, **Redact**, **Block**
- [x] Konfigürasyon: path/method filtreleri, entity include/exclude, redaksiyon stili
- [x] DI / `AddDataGuardian` + konfigürasyon bağlama ve başlangıçta doğrulama
- [x] Opsiyonel NER entegrasyonu (ONNX, deneysel)

### v2.0 (Kısa vadeli hedeflenen)
- [ ] .NET Standard 2.0 desteği
- [ ] Edge (Cloudflare) snippet için logging ve metric forwarding
- [ ] JSON-aware redaksiyon (sadece değerleri maskeleme, key’lere dokunmama)
- [ ] Farklı risk içeren veriler için kurallar (VKN, SGK sicil numarası, plaka, pasaport numarası, IP adresi, MAC adresi, konum verileri vb.)
- [ ] NER için token→karakter ofset eşlemesi ve pencereleme (uzun gövdeler)
- [ ] CLI aracı ile dosya/batch analizi (`dataguardian analyze file.json`)

### v3.0 (Uzun vadeli hedeflenen)
- [ ] Plug-in mimarisi (kendi PII dedektörlerini ekleme)
- [ ] Yönetim arayüzü (policy editor + dashboard)
- [ ] OpenAPI/Swagger plugin: request/response şemalarına göre risk tahmini

---

## Lisans
MIT © Devoplus
