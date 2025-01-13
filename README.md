# Otopark Yönetim Sistemi

Bu proje, otopark giriş-çıkış işlemlerini kolaylaştırmak, yetkili plakaları kontrol etmek ve otopark ücretlendirme sürecini otomatikleştirmek amacıyla geliştirilmiş bir masaüstü uygulamasıdır. Plaka tanıma, şirket yönetimi ve ödeme işlemleri gibi birçok fonksiyon içerir.

---

## **Projenin Özellikleri**
- **Araç Giriş-Çıkış İşlemleri:**
  - Araç plakalarının manuel olarak veya OCR teknolojisi ile otomatik tanınarak giriş-çıkışlarının kaydedilmesi.
  - Giriş-çıkış kayıtlarının şirket bazında saklanması ve raporlanması.
- **Plaka Tanıma (OCR):**
  - OpenCV ve Tesseract OCR kullanılarak araç plakalarının görüntülerden otomatik olarak okunması.
- **Yetkili Plaka Yönetimi:**
  - Şirketlere özel yetkili plakaların tanımlanması ve giriş sırasında otomatik kontrol edilmesi.
- **Otopark Ücretlendirme:**
  - Araçların otoparkta kaldığı süreye göre saatlik tarifeye dayalı otomatik ücret hesaplanması.
- **Grafiksel Görselleştirme:**
  - Otopark doluluk oranlarının grafiklerle gösterilmesi (pasta grafiği).
- **Veritabanı Yönetimi:**
  - MSSQL Server kullanılarak şirketler, araçlar, giriş-çıkış kayıtları, yetkili plakalar ve ödemelerin yönetimi.

---

## **Kullanılan Teknolojiler**
- **C# / WPF (Windows Presentation Foundation):**
  - Masaüstü uygulamasını geliştirmek için kullanıldı.
- **Microsoft SQL Server (MSSQL):**
  - Veritabanı yönetim sistemi olarak kullanıldı.
- **OpenCV ve Tesseract:**
  - Plaka tanıma işlemleri için kullanılan görüntü işleme ve OCR kütüphaneleri.
- **Python:**
  - Plaka tanıma işlemleri için opsiyonel olarak kullanılan Python scripti.
- **ADO.NET ve PyODBC:**
  - C# ve Python ile MSSQL bağlantıları kuruldu.

---

## **Kurulum**
1. **Veritabanı Ayarları:**
   - Microsoft SQL Server'da `OtoparkSistemi` adlı bir veritabanı oluşturun.
   - SQL tablolarını tanımlayın:
     - `Sirketler`, `Araclar`, `AracGirisCikis`, `YetkiliPlakalar`, `Odemeler`

2. **Python Gereksinimleri:**
   - Python kütüphanelerini yüklemek için:
     ```bash
     pip install -r requirements.txt
     ```

3. **Tesseract OCR Kurulumu:**
   - Tesseract OCR'yi sisteminize yükleyin:
     - [Windows için Tesseract Kurulumu](https://github.com/UB-Mannheim/tesseract/wiki)
   - Tesseract'in yolunu Python'da tanımlayın:
     ```python
     pytesseract.pytesseract.tesseract_cmd = r"C:\Program Files\Tesseract-OCR\tesseract.exe"
     ```

4. **C# Projesini Ayarlayın:**
   - Visual Studio'da projeyi açın.
   - `App.config` dosyasında MSSQL bağlantı bilgilerini düzenleyin.
   - Uygulamayı çalıştırın.

---

## **Kullanım**
- **Oturum Açma:**
  - Şirket yöneticileri giriş yaparak kendi verilerine erişir.
- **Araç Giriş ve Çıkış:**
  - Araç giriş-çıkış işlemleri kayıt altına alınır.
  - OCR ile otomatik plaka tanıma yapılabilir.
- **Ücret Hesaplama:**
  - Çıkış yapan araçlar için otoparkta kalma süresine göre ücret hesaplanır.
- **Yetkili Plaka Yönetimi:**
  - Şirket bazında yetkili plakalar tanımlanabilir ve kontrol edilebilir.
- **Grafik ve Raporlama:**
  - Doluluk oranları grafiksel olarak gösterilir.

---

## **Notlar**
- Proje, küçük ve orta ölçekli otopark yönetimi için uygundur.
- Sistemi geliştirmek veya farklı özellikler eklemek için açık kaynak kod yapısı kullanılmıştır.

---

## **Lisans**
Bu proje [MIT Lisansı](https://opensource.org/licenses/MIT) altında lisanslanmıştır.
