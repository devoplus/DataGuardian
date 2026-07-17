# Changelog

Bu projedeki önemli değişiklikler bu dosyada belgelenir.

## [1.1.0] - Yayınlanmadı

### Düzeltildi — koruma doğruluğu (davranış değişikliği)
- **IBAN tip anahtarı uyumsuzluğu.** Tanıyıcı `IBAN` üretirken `Weights`/`RedactTypes` `IBAN_TR` bekliyordu; IBAN'lar ağırlık 1 alıyor ve hiç redakte edilmiyordu. Tüm tip anahtarları tek kaynakta (`PiiTypes`) birleştirildi ve `IBAN` olarak hizalandı. **`appsettings` içinde `Weights`/`RedactTypes` için `IBAN_TR` kullananlar `IBAN` olarak güncellemelidir.**
- **TCKN dil kapısı.** TCKN artık dilden bağımsız tespit edilir; Türkçe diakritik içermeyen (ör. ASCII JSON) gövdelerde de bulunur.
- **Telefon ulusal formatı.** `PhoneRecognizer` artık `+` öneksiz numaraları `DefaultPhoneRegion`/dil bölgesiyle ayrıştırır (`05xx…` gibi numaralar tespit edilir).
- **IBAN boşluklu gösterim.** 4'lü gruplu (boşluklu) IBAN'lar tespit edilir.
- **Adres yanlış pozitifleri.** Anahtar sözcükler kelime sınırıyla eşleştirilir (`have→ave`, `katalog→kat` artık eşleşmez).
- **DOB doğrulaması.** Geçersiz (ör. `31.02.2024`) ve gelecekteki tarihler DOB sayılmaz.
- **Kredi kartı.** Regex erken kesmesi giderildi; Troy, Discover, JCB, Maestro, Diners ve 13/19 haneli Visa eklendi.

### Düzeltildi — middleware / HTTP
- Yanıt gövdesi swap'ı `try/finally` ile korunur; downstream exception'da orijinal stream geri yüklenir.
- Block modunda 403 gövdesi istemciye ulaşır; `-Detected` header'ları bloklanan yanıtta temizlenir.
- Redaksiyon sonrası `Content-Length` güncellenir (Kestrel uyuşmazlık hatası giderildi).
- Yanıtlar yalnızca analiz edilebilir içerik türleri için ve `MaxBodySizeBytes`'a kadar tamponlanır; büyük/streaming yanıtlar doğrudan akıtılır (OOM ve SSE bozulması giderildi).
- İstek gövdesi de boyut sınırıyla okunur.
- UTF-8/ASCII olmayan charset'li yanıtlar bozulmasın diye redakte edilmez.
- Risk header'ları `InvariantCulture` ile üretilir; metod filtreleri büyük/küçük harf duyarsızdır.

### Düzeltildi — edge
- Snippet, geçersiz/eksik eşik ve risk header'larını güvenli şekilde ele alır; virgüllü ondalıkları tolere eder; `DATAGUARDIAN_FAIL_MODE=closed` ile fail-closed seçeneği sunar.

### Eklendi
- `PiiTypes` kanonik tip sabitleri.
- `DataGuardianOptions.Validate()` ve başlangıçta konfigürasyon doğrulaması.
- `AddDataGuardian(IConfiguration)` / `AddDataGuardian(Action<>)` ve `UseDataGuardian()` / `UseDataGuardian(Action<>)` overload'ları (DI + IOptions benzeri bağlama).
- Yeni seçenekler: `DefaultPhoneRegion`, `BlockOversizeBodies`, `RedactionHashKey`, `BlockMessage`; `AnalyzableContentTypes`'a `text/html`, `text/csv`.
- Skorlamada çakışma (overlap) çözümü ve risk clamp; açık `0` ağırlığı tipi devre dışı bırakır.
- Entity tip filtreleri artık NER hit'lerine de uygulanır.
- Hash redaksiyonu anahtarlı HMAC-SHA256'ya geçti; Partial maske kısa değerleri tamamen maskeler.
- Kapsamlı test paketi (recognizer + engine + middleware TestHost) ve `docs/` belgeleri.

### Güvenlik
- NER'in v1.x'te redaksiyon için olgun olmadığı belgelendi; skor/etiketleme için kullanılmalı ([docs/recognizers.md](docs/recognizers.md#ner-onnx)).
- Header oracle riski ve öneriler belgelendi ([docs/security.md](docs/security.md)).
