# Modeller (NER) — `models/` klasörü

Bu klasör **isteğe bağlı** olan NER (Named Entity Recognition) bileşeni içindir. Sadece kural tabanlı tespitleri kullanacaksanız burayı boş bırakabilirsiniz. NER’i etkinleştirmek için aşağıdaki dosyaları ekleyin:

- `kvkk-ner.onnx` — ONNX biçiminde token-sınıflandırma (NER) modeli
- `tokenizer.json` — modelin tokenizer tanımı
- `labels.txt` — her satırda bir etiket (örn. `O`, `B-PER`, `I-PER`, `B-ADDR`, …)

## Hızlı üretim (Exporter ile)

Depoda `tools/export_ner_onnx.py` betiği vardır. Tek komutla bu üç dosyayı üretir.

```bash
# Sanal ortam (opsiyonel)
python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate

# Bağımlılıklar
pip install -r tools/requirements.txt

# Türkçe örnek
python tools/export_ner_onnx.py --model-id dbmdz/bert-base-turkish-cased --out-dir models

# İngilizce örnek
python tools/export_ner_onnx.py --model-id dslim/bert-base-NER --out-dir models
```

Betik, HuggingFace modelindeki `id2label` bilgisini kullanarak `labels.txt` dosyasını oluşturur ve `tokenizer.json` ile ONNX modeli `models/` altına kaydeder.

> Not: NER’i açmak için uygulama tarafında `DataGuardianOptions.EnableNer = true` olmalıdır.
