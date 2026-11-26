# Shift Hazırlayıcı

Haftalık shift programı oluşturucu. Mağaza, ofis veya ekip için **Pazartesi–Pazar** arası otomatik, adil ve özelleştirilebilir shift planı hazırlar.

<p align="left">
  <a href="https://github.com/c4brky/SHIFT-APP/releases/latest/download/shift_hazirlayici.exe">
    <img src="assets/download.svg" alt="PROGRAMI İNDİR" width="260" />
  </a>
</p>

> [!WARNING]
> Windows SmartScreen **"Bilinmeyen uygulama"** uyarısı verebilir. Uygulama dijital olarak imzalanmadığı için bu uyarı normaldir; bir virüs değildir ve kaynak kodu tamamen açıktır.

> [!TIP]
> **Nasıl Açılır?** Uyarı penceresinde **"Ek bilgi"** yazısına tıklayıp **"Yine de çalıştır"** butonuna basarak programı güvenle başlatabilirsiniz. *(İsterseniz indirilen `.exe` dosyasına Sağ Tık → Özellikler → **Engeli kaldır** diyerek uyarıyı kalıcı olarak kapatabilirsiniz.)*

---

## Ekran Görüntüleri

| Uygulama Arayüzü | Shift Sonucu |
|---|---|
| ![Uygulama](assets/in-app-view.png) | ![Shift Sonucu](assets/output-result.png) |

---

## Özellikler

- **3–10 kişi** arası ekip desteği
- **Sabahçı / Ara Shift / Akşamcı / Full / İzin** shift tipleri
- Renkli, paylaşılabilir **PNG / JPG** görsel çıktı
- Haftalık izin ataması (rastgele / belirli gün / toplu Pazar)
- **Adalet algoritması**: Sabah + Ara shift eşit dağıtılır
- **Önceki 2 hafta** kontrol edilerek tekrar önleme
- **Full kalma**: İsteğe bağlı, gün gün kişi seçimi
- **Mesai saatleri**: Tamamen özelleştirilebilir (07:00 – 22:00, 15 dk aralık)
- **Otomatik kayıt**: Her kapanışta ayarlar ve son durum saklanır
- **Geçmiş kayıtları**: Oluşturulan eski shiftlere tek tıkla erişim
- **Modern tasarım**: Antrasit koyu tema, Windows 10 / 11 uyumlu arayüz

---

## Gereksinimler & Kurulum

> [!NOTE]
> **Hazır Exe (Önerilen):** Sağdaki **Releases** bölümünden veya yukarıdaki **Programı İndir** butonundan `shift_hazirlayici.exe` dosyasını indirip doğrudan çift tıklayarak çalıştırabilirsiniz. **.NET Runtime gömülüdür**, ek kurulum veya internet bağlantısı gerektirmez.

---

## Proje Yapısı

```text
├── assets/                       # README görselleri ve indirme butonu
├── src/ShiftHazirlayici/         # Ana uygulama (WPF)
│   ├── Models/                   # Veri modelleri
│   ├── Services/                 # Shift üretici, kayıt, görsel dışa aktarma
│   ├── Views/                    # Özelleştirme penceresi
│   ├── Helpers/                  # Windows tema yardımcıları
│   ├── Themes/                   # XAML tema ve stil dosyaları
│   └── Assets/                   # Uygulama logosu ve ikonu
└── ShiftHazirlayici.sln          # Solution dosyası
```

---

## Lisans

MIT

---

*created by c4brky*
