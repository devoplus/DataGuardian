# Katkı Rehberi

## Geliştirme ortamı

- .NET SDK 8.0
- Depoyu klonlayın ve:

```bash
dotnet restore
dotnet build
dotnet test
```

## Proje yapısı

```
src/Devoplus.DataGuardian/        # Kütüphane
  Recognizers/                    # Kural tabanlı tanıyıcılar
  Ner/                            # Opsiyonel ONNX NER
  Internal/                       # Dahili yardımcılar (ör. ResponseCaptureStream)
  PiiTypes.cs                     # Kanonik tip anahtarları
tests/Devoplus.DataGuardian.Tests # xUnit + TestHost testleri
samples/                          # Örnek minimal API
edge/                             # Cloudflare snippet
docs/                             # Belgeler
```

## Yeni tanıyıcı ekleme

1. `PiiTypes`'a bir sabit ekleyin (ör. `public const string Vkn = "VKN";`).
2. `IPiiRecognizer` uygulayan bir sınıf yazın; regex'i `static readonly` tutun ve bir `MatchTimeout` verin.
3. `DataGuardianEngine` kurucusundaki tanıyıcı listesine ekleyin.
4. `DataGuardianOptions.Weights`'e aynı anahtarla bir ağırlık ekleyin; redakte edilebilir olacaksa `RedactTypes`'a da ekleyin.
5. Pozitif ve negatif testler yazın.

> Tip anahtarları `PiiTypes` üzerinden tek kaynaktan gelmelidir. `TypeConsistencyTests`, `RedactTypes` ile `Weights` uyumunu doğrular.

## Test kuralları

- Her tanıyıcı için en az bir pozitif ve bir negatif vaka.
- Middleware davranışları `Microsoft.AspNetCore.TestHost` ile uçtan uca test edilir (Tag/Block/Redact/filtreler).
- Regresyonu önlemek için düzeltilen her hata için bir test bırakın.

## Kod stili

- `Nullable` etkin; kütüphane projesinde nullable uyarıları hata sayılır.
- Kullanıcıya görünen metinlerde resmî dil kullanın.
- Genel API'lere XML doc yorumu ekleyin.

## CI

`.github/workflows/dotnet.yml`, push ve PR'larda restore/build/test çalıştırır, test sonuçlarını (`trx`) ve kod kapsamını artifact olarak yükler.
