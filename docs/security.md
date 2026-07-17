# Güvenlik Modeli, Sınırlar ve Öneriler

DataGuardian bir **derinlemesine savunma** katmanıdır; tek başına bir veri sızıntısı garantisi değildir. Bu belge tehdit modelini, bilinen sınırları ve sıkılaştırma önerilerini özetler.

## Ne sağlar

- Request/response gövdelerinde kural tabanlı PII tespiti ve 0–10 risk skoru.
- İsteğe bağlı redaksiyon (MaskAll/Partial/Hash) ve bloklama.
- Edge (Cloudflare) ile ikinci bir bariyer.

## Bilinen sınırlar

- **Kapsam gövdeyledir.** Query string, path, header, cookie ve dosya adları analiz edilmez.
- **İçerik türü/boyut.** `AnalyzableContentTypes` dışındaki türler ve `MaxBodySizeBytes` üstü gövdeler varsayılan olarak analiz edilmeden geçer. Sıkı senaryolarda `BlockOversizeBodies = true` kullanın ve tür listesini gözden geçirin.
- **Kural tabanlı tespit eksiksiz değildir.** Yeni/nadir formatlar kaçabilir; adres tespiti anahtar sözcük temellidir. NER opsiyoneldir ve v1.x'te redaksiyon için olgun değildir (bkz. [recognizers.md](recognizers.md#ner-onnx)).
- **Charset.** UTF-8/ASCII olmayan gövdeler bozulmasın diye redakte edilmez.

## Header'lar bir oracle olabilir

`EmitHeaders = true` (varsayılan) iken `X-DataGuardian-*-Detected` header'ları hangi PII tiplerinin ve kaç adedinin bulunduğunu açığa vurur. Bu bilgi bir saldırgan için değerlidir.

**Öneri:**
- Bu header'ları yalnızca güvenilir iç tüketicilere (ör. edge) verin.
- Dış istemcilere dönen yanıtlarda `EmitHeaders = false` yapın veya güven sınırında (reverse proxy/edge) `X-DataGuardian-*` header'larını **temizleyin**.
- Bloklanan yanıtlarda `-Detected` header'ları zaten temizlenir.

## Redaksiyon geri döndürülebilirliği

- `Hash` stili gizli anahtarlı HMAC kullanır; anahtar gizli kalmalıdır. Zayıf/paylaşılan anahtar veya `MaskAll`'a kıyasla yanlış beklenti geri döndürme riskini artırır. Ayrıntı: [actions.md](actions.md).
- Düşük entropili tanımlayıcılar (TCKN, kart) için en güvenli seçenek `MaskAll`'dır.

## Denial of Service

- İstek ve yanıt gövdeleri `MaxBodySizeBytes` ile sınırlanır; büyük gövdeler tamponlanmadan akıtılır.
- Regex tanıyıcılarında `MatchTimeout` vardır ve e-posta deseni ReDoS'a karşı yazılmıştır.
- Yine de üst katmanda (Kestrel `MaxRequestBodySize`, reverse proxy limitleri, rate limiting) sınırlar koymanız önerilir.

## Edge (Cloudflare) sıkılaştırma

- Risk header'ları kültürden bağımsız (`InvariantCulture`) üretilir; edge snippet virgüllü ondalıkları da tolere eder.
- Snippet, risk header'ları eksik/geçersizse `DATAGUARDIAN_FAIL_MODE=closed` ile **fail-closed** yapılandırılabilir. Regülasyon-kritik uçlarda bunu tercih edin.
- Ayrıntı: [edge.md](edge.md).

## Önerilen üretim yapılandırması (özet)

- `Action = Block` + uygun `BlockAt` (regülasyon-kritik uçlarda), gerekiyorsa edge'de ikinci bariyer.
- Dış yüzeyde `EmitHeaders = false`.
- `BlockOversizeBodies = true` (kritik uçlarda).
- `Hash` kullanılacaksa gizli ve sabit `RedactionHashKey`; aksi halde `MaskAll`.
- NER'e redaksiyon için güvenmeyin; skor/etiketleme için kullanın.

## Zafiyet bildirimi

Güvenlik açığı bildirimlerini herkese açık bir issue yerine deponun güvenlik/iletişim kanalları üzerinden iletin.
