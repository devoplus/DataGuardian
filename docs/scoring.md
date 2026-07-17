# Risk Skorlaması

DataGuardian, tespit edilen PII'ları 0–10 arası tek bir risk skoruna indirger.

## Formül

```
sum  = Σ_type ( min(count_type, MaxCountPerType) × weight_type )
risk = 10 × (1 − e^(−K × sum))
risk = clamp(risk, 0, 10)
```

- `count_type`: o tipten kaç hit bulunduğu (çakışma çözümü sonrası).
- `weight_type`: `Weights` sözlüğündeki ağırlık. Anahtar yoksa **1**; açık **0** tipi devre dışı bırakır; negatif değer 0 sayılır.
- `MaxCountPerType`: tek tipin katkısını sınırlar (varsayılan 5).
- `K`: eğrinin dikliği (varsayılan 0.15). Büyük `K` daha az veriyle eşiğe ulaşır.
- Sonuç her zaman `[0, 10]` aralığına clamp edilir.

## Sezgi

Fonksiyon monoton artan ve doyumludur: ilk tespitler skoru hızla yükseltir, çok sayıda tespit skoru 10'a asimptotik yaklaştırır. Bu, "bir TCKN de on TCKN de yüksek risktir" davranışını verir.

### Örnek

Varsayılan ağırlıklarla tek bir TCKN (ağırlık 10):

```
sum  = min(1,5) × 10 = 10
risk = 10 × (1 − e^(−0.15 × 10)) = 10 × (1 − e^(−1.5)) ≈ 7.77
```

Tek bir e-posta (ağırlık 4):

```
sum  = 4
risk = 10 × (1 − e^(−0.6)) ≈ 4.51
```

## Ağırlıkla ayar

- Bir tipi tamamen skordan çıkarmak için `Weights[type] = 0` verin.
- Bir tipin etkisini artırmak/azaltmak için ağırlığı değiştirin.
- Eşikleri (`BlockAt`, `RedactAt`) bu skala üzerinden seçin; örnek politikalar için [actions.md](actions.md).

## Doğrulama

`K > 0` ve `MaxCountPerType >= 1` başlangıçta zorunludur (aksi halde `ArgumentException`). Bu, negatif/sonsuz risk gibi sessiz konfigürasyon hatalarını engeller.
