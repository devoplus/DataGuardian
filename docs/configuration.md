# Yapılandırma Referansı

DataGuardian, `DataGuardianOptions` ile yapılandırılır. Seçenekler üç yolla verilebilir:

```csharp
// 1) Konfigürasyondan bağlama
builder.Services.AddDataGuardian(builder.Configuration.GetSection("DataGuardian"));
app.UseDataGuardian();

// 2) Kod içinde inline
app.UseDataGuardian(o => { o.Action = ActionMode.Block; o.BlockAt = 8; });

// 3) Hazır bir nesne ile
var opt = new DataGuardianOptions { Action = ActionMode.Tag };
app.UseDataGuardian(opt);
```

Her üç yol da başlangıçta `DataGuardianOptions.Validate()` çağırır; geçersiz değerler `ArgumentException` fırlatır.

## Seçenekler

| Seçenek | Tip | Varsayılan | Açıklama |
|---|---|---|---|
| `AnalyzeRequests` | `bool` | `true` | İstek gövdelerini analiz et. |
| `AnalyzeResponses` | `bool` | `true` | Yanıt gövdelerini analiz et. |
| `Action` | `ActionMode` | `Tag` | `None` / `Tag` / `Redact` / `Block`. |
| `BlockAt` | `double` | `-1` | Block eşiği (0–10). Negatif değer bloklamayı kapatır. |
| `RedactAt` | `double` | `0` | Redact eşiği (0–10). Risk ≥ bu değerse redakte eder. |
| `Redaction` | `RedactionStyle` | `MaskAll` | `MaskAll` / `Partial` / `Hash`. |
| `RedactTypes` | `HashSet<string>` | EMAIL, PHONE, TCKN, CREDIT_CARD, IBAN, DOB | Redakte edilecek tipler. |
| `RedactionHashKey` | `string?` | `null` | `Hash` stili için gizli anahtar. Boşsa süreç başına rastgele anahtar. |
| `Weights` | `Dictionary<string,double>` | bkz. aşağı | Tip başına ağırlık. Anahtar bulunmazsa 1; açık `0` tipi devre dışı bırakır. |
| `MaxCountPerType` | `int` | `5` | Skora katkıda tip başına üst sınır. `>= 1` olmalı. |
| `K` | `double` | `0.15` | Risk eğrisinin dikliği. `> 0` olmalı. |
| `MaxBodySizeBytes` | `int` | `524288` | Tamponlanıp analiz edilen azami gövde boyutu. |
| `BlockOversizeBodies` | `bool` | `false` | Bu sınırı aşan gövdeler Block modunda bloklansın (fail-closed) mı? |
| `HeaderPrefix` | `string` | `X-DataGuardian` | Emit edilen header öneki. |
| `EmitHeaders` | `bool` | `true` | `X-DataGuardian-*` header’larını yaz. Dış istemcilere kapatmayı düşünün. |
| `IncludePaths` / `ExcludePaths` | `List<string>` | boş | Path öneki filtreleri (ordinal, büyük/küçük harf duyarsız). |
| `IncludeMethods` / `ExcludeMethods` | `List<string>` | boş | HTTP metod filtreleri (büyük/küçük harf duyarsız). |
| `IncludeEntityTypes` / `ExcludeEntityTypes` | `HashSet<string>` | boş | Entity tip filtreleri (NER dahil tüm hit’lere uygulanır). |
| `DefaultPhoneRegion` | `string` | `TR` | Ulusal formatlı telefonları ayrıştırmak için varsayılan bölge. |
| `AnalyzableContentTypes` | `HashSet<string>` | JSON, text/plain, text/html, text/csv, XML, form-urlencoded | Analiz edilecek içerik tipleri. Eksik/boş tip de analiz edilir. |
| `LanguageOverride` | `string?` | `null` | `null` = otomatik, `"tr"` veya `"en"`. |
| `EnableNer` | `bool` | `false` | Opsiyonel ONNX NER. |
| `NerModelPath` / `NerTokenizerPath` / `NerLabelsPath` | `string` | `models/…` | NER dosya yolları. |
| `NerMaxSequenceLength` | `int` | `256` | Modele verilen azami token uzunluğu. |
| `MinNerConfidence` | `double` | `0.6` | NER entity için asgari güven (0–1). |
| `BlockMessage` | `string` | `Blocked by DataGuardian policy.` | Bloklanan yanıtın gövdesi. |

### Varsayılan ağırlıklar

```
TCKN=10, CREDIT_CARD=9, IBAN=8, DOB=7, ADDRESS=6, PHONE=5, EMAIL=4, PERSON=3
```

Anahtarlar `PiiTypes` sabitleriyle aynıdır. `Weights` / `RedactTypes` / tanıyıcı çıktıları arasındaki tutarlılık bir testle (`TypeConsistencyTests`) güvenceye alınır.

## Doğrulama kuralları

`Validate()` şunları zorunlu kılar: `K > 0`, `MaxCountPerType >= 1`, `MaxBodySizeBytes >= 1`, `BlockAt <= 10`, `0 <= RedactAt <= 10`, `NerMaxSequenceLength >= 1`, `0 <= MinNerConfidence <= 1`, `HeaderPrefix` boş değil.
