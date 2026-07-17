# Aksiyon Modları ve Redaksiyon

`Action` seçeneği, risk skoruna göre ne yapılacağını belirler.

| Mod | Davranış |
|---|---|
| `None` | Hiçbir şey yapmaz (isteğe bağlı header hariç). |
| `Tag` | Yalnızca `X-DataGuardian-*` header'larını yazar. |
| `Redact` | Yanıt gövdesinde `RedactTypes` içindeki eşleşmeleri maskeler (risk ≥ `RedactAt`). |
| `Block` | Risk ≥ `BlockAt` ise 403 döndürür (istek veya yanıt aşamasında). |

## Block

- İstek aşamasında bloklanırsa, downstream endpoint hiç çalışmaz (erken kesme, maliyet düşer).
- Bloklanan yanıt **gövdeli** bir 403'tür (`BlockMessage`); `-Detected` header'ları oracle oluşturmamak için temizlenir.
- `BlockAt` negatifse bloklama devre dışıdır (varsayılan `-1`).

```csharp
o.Action = ActionMode.Block;
o.BlockAt = 8.0;
```

## Redact

Risk `RedactAt`'i aşarsa ve redakte edilebilir hit varsa, yanıt gövdesi yeniden yazılır ve `Content-Length` güncellenir. Redakte edilebilir bir tip yoksa yanıt değiştirilmez ve `-Response-Skip-Reason: NoRedactableTypes` yazılır.

### Redaksiyon stilleri

| Stil | Örnek (`secret@example.com`) | Notlar |
|---|---|---|
| `MaskAll` | `******************` | Tüm karakterleri `*` yapar (uzunluk korunur). |
| `Partial` | `se************om` | İlk/son 2 karakteri gösterir; **uzunluk ≤ 4 ise tamamen maskeler**. |
| `Hash` | `a1b2c3d4e5f6a7b8` | Anahtarlı HMAC-SHA256 önekiyle değiştirir (uzunluk değişir). |

### Hash hakkında güvenlik notu

`Hash`, değeri **gizli anahtarlı** HMAC-SHA256'nın 16 hex karakterine indirger. Anahtar (`RedactionHashKey`) gizli kaldığı sürece geri döndürme (özellikle düşük entropili TCKN/kart için) pratik değildir.

- `RedactionHashKey` boşsa süreç başına rastgele bir anahtar kullanılır (yeniden başlatmada değişir).
- Aynı değeri kayıtlar arasında ilişkilendirmeniz gerekiyorsa sabit bir gizli anahtar verin — ancak **anahtarı asla açığa çıkarmayın**. Aksi halde hash geri döndürülebilir.
- Yalnızca gizleme istiyorsanız `MaskAll` daha basit ve güvenlidir.

## İçerik türü ve boyut

- Analiz yalnızca `AnalyzableContentTypes` içindeki (veya eksik/boş) içerik türlerine uygulanır. Diğer türler (ör. `application/octet-stream`, `text/event-stream`) tamponlanmadan doğrudan akıtılır (OOM ve streaming bozulması önlenir).
- `MaxBodySizeBytes`'ı aşan gövdeler tamponlanmaz; varsayılan olarak analiz edilmeden geçer ve `BodyTooLarge` skip nedeni yazılır. Güvenlik-kritik uçlar için `BlockOversizeBodies = true` ile fail-closed davranışı seçilebilir.
- UTF-8/ASCII olmayan charset'li yanıtlar bozulmasın diye redakte edilmez (`UnsupportedCharset`).
