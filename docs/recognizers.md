# Tanıyıcılar (Recognizers)

Her tanıyıcı `IPiiRecognizer` arayüzünü uygular ve metin içinde bir tip için `PiiHit` (tip, başlangıç, uzunluk) listesi döndürür. Tip adları `PiiTypes` sabitlerinden gelir.

| Tip | Tanıyıcı | Doğrulama | Notlar |
|---|---|---|---|
| `TCKN` | `TcknRecognizer` | Resmî T.C. Kimlik No checksum'ı (10. ve 11. hane) | **Dilden bağımsız** çalışır; checksum yanlış pozitifleri eler. |
| `CREDIT_CARD` | `CreditCardRecognizer` | Luhn + bilinen şema öneki | Visa, Mastercard, Amex, **Troy**, Discover, JCB, Maestro, Diners. |
| `IBAN` | `IbanRecognizer` | Ülke uzunluğu + mod-97 | Kompakt ve 4'lü gruplu (boşluklu) gösterimi tanır. |
| `EMAIL` | `EmailRecognizer` | Desen | Geri-izleme güvenli desen (ReDoS önlemli). |
| `PHONE` | `PhoneRecognizer` | libphonenumber `IsValidNumber` | Ulusal format için `DefaultPhoneRegion`/dil bölgesi kullanılır. |
| `DOB` | `DobRecognizer` | Takvim geçerliliği + gelecek reddi | `yyyy-mm-dd` ve `dd-mm-yyyy` (`/`, `.`, `-`). |
| `ADDRESS` | `AddressRecognizer` | Kelime sınırlı anahtar sözcük | Yalnızca anahtar sözcüğü kapsar; tag/skor amaçlıdır. |

## Önemli davranışlar

- **Dil ve tanıyıcı seçimi.** TCKN, IBAN, kredi kartı ve e-posta yapısal desenlerdir ve **her dilde** çalışır. Yalnızca adres anahtar sözcükleri (TR/EN listesi) ve telefon varsayılan bölgesi dile bağlıdır. Dil, `LanguageOverride` yoksa `SimpleLanguage.Guess` ile (Türkçe diakritik veya yaygın Türkçe kelimeler) tahmin edilir.
- **ReDoS koruması.** Tüm regex tabanlı tanıyıcılar `Compiled` + 200 ms `MatchTimeout` ile kurulur; e-posta deseni katastrofik geri izleme yapmayacak biçimde yazılmıştır.
- **Çakışma çözümü.** Aynı metin aralığını birden fazla desen tanıyıcı yakalarsa, motor en yüksek ağırlıklı (eşitse en uzun) hit'i tutar; böylece çifte sayım olmaz.
- **ADDRESS ve redaksiyon.** ADDRESS hit'i yalnızca anahtar sözcüğü (`mah.`, `no:` vb.) kapsadığından varsayılan `RedactTypes` içinde **değildir**; anahtar sözcüğü maskelemek adresi gizlemez. ADDRESS'i skor/etiketleme için kullanın.

## Yeni tanıyıcı ekleme

`IPiiRecognizer` uygulayan bir sınıf yazın ve `PiiTypes`'a yeni bir sabit ekleyip `Weights` (ve gerekiyorsa `RedactTypes`) içine aynı anahtarı koyun. Ayrıntı: [contributing.md](contributing.md).

<a id="ner-onnx"></a>
## NER (ONNX) — deneysel

Opsiyonel BERT-NER, serbest metinde `PERSON/ADDRESS/...` gibi varlıkları bulur. `EnableNer = true` ve `models/` altında `kvkk-ner.onnx`, `tokenizer.json`, `labels.txt` gerektirir.

**Bilinen sınırlar (v1.x):**

- NER varlık ofsetleri **token indeksi** olarak üretilir; redaksiyon karakter ofseti beklediğinden, NER tabanlı redaksiyon yanlış konumu maskeleyebilir. Bu nedenle NER'i şu an **yalnızca skor/etiketleme** amacıyla kullanın; hassas redaksiyon için NER'e güvenmeyin.
- Girdi tensör isimleri (`input_ids`, `attention_mask`, `token_type_ids`) sabittir; `token_type_ids` beklemeyen modellerde (DistilBERT/RoBERTa) çalışmayabilir.
- `NerMaxSequenceLength`'ten uzun metin kırpılır (pencereleme yoktur).
- Model/tokenizer dosyaları yüklenemezse NER sessizce devre dışı kalır. Üretimde NER'e güveniyorsanız dosyaların dağıtıma dahil olduğunu ve yüklendiğini doğrulayın.

Bu maddeler yol haritasında ele alınmaktadır (README → Roadmap v2.0).
