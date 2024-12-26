import cv2
import pytesseract
import matplotlib.pyplot as plt
import mysql.connector

# Tesseract OCR'ın yolunu ayarlayın (Windows için)
pytesseract.pytesseract.tesseract_cmd = r"C:/Program Files/Tesseract-OCR/tesseract.exe"

# Veritabanı bağlantısı
def save_plate_to_database(plate):
    connection = None
    try:
        connection = mysql.connector.connect(
            host="localhost",
            user="root",  # MySQL kullanıcı adınız
            password="1426ma",  # MySQL şifreniz
            database="OtoparkSistemi"  # Veritabanı adı
        )
        cursor = connection.cursor()
        query = "INSERT INTO AracGirisCikis (Plaka, GirisTarihi, Durum) VALUES (%s, NOW(), 'GirisYapildi')"
        cursor.execute(query, (plate,))
        connection.commit()
        print(f"Plaka '{plate}' başarıyla veritabanına kaydedildi.")
    except mysql.connector.Error as err:
        print(f"Veritabanı hatası: {err}")
    finally:
        if connection and connection.is_connected():
            cursor.close()
            connection.close()

# Plaka algılama
def detect_plate_from_camera():
    cap = cv2.VideoCapture(0)  # Kamerayı başlat

    if not cap.isOpened():
        print("Kamera açılamadı.")
        return

    while True:
        ret, frame = cap.read()
        if not ret:
            print("Kameradan görüntü alınamadı.")
            break

        # Görüntüyü gri tonlamaya çevir
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

        # Kenar tespiti için bulanıklık uygula
        blurred = cv2.GaussianBlur(gray, (5, 5), 0)

        # Kenar tespiti yap
        edged = cv2.Canny(blurred, 100, 200)

        # Kontur bulma
        contours, _ = cv2.findContours(edged, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)

            # Plaka benzeri dikdörtgenleri filtrele
            if w > h and w > 100 and h > 30:  # Plaka boyut kriterleri
                plate_region = frame[y:y + h, x:x + w]

                # OCR işlemi ile plaka okuma
                plate_text = pytesseract.image_to_string(plate_region, config="--psm 7")
                plate_text = plate_text.strip()  # Fazladan boşlukları temizle

                if plate_text:
                    print(f"Algılanan Plaka: {plate_text}")

                    # Veritabanına kaydet
                    save_plate_to_database(plate_text)

                    # Plakayı görselleştir
                    cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 255, 0), 2)
                    cv2.putText(frame, plate_text, (x, y - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 0), 2)

        # Görüntüyü matplotlib ile göster (cv2.imshow yerine)
        plt.imshow(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        plt.title("Kamera - Plaka Algılama")
        plt.axis("off")
        plt.show()

        # Çıkış için bir görüntü gösterdikten sonra döngüyü kır
        break

    cap.release()

if __name__ == "__main__":
    detect_plate_from_camera()
