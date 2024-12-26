import cv2
import pytesseract

# Tesseract OCR'ın yolunu ayarlayın (Windows için)
pytesseract.pytesseract.tesseract_cmd = r"C:/Program Files/Tesseract-OCR/tesseract.exe"

def detect_plate_live():
    # Kamerayı başlat
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("Kamera açılamadı.")
        return

    while True:
        # Kameradan görüntü al
        ret, frame = cap.read()
        if not ret:
            print("Kameradan görüntü alınamadı.")
            break

        # Görüntüyü gri tonlamaya çevir
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        blurred = cv2.GaussianBlur(gray, (5, 5), 0)
        edged = cv2.Canny(blurred, 100, 200)

        # Kontur tespiti
        contours, _ = cv2.findContours(edged, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)

            # Plaka benzeri dikdörtgenleri filtrele
            if w > h and w > 100 and h > 30:  # Plaka boyut kriterleri
                plate_region = frame[y:y + h, x:x + w]
                plate_text = pytesseract.image_to_string(plate_region, config="--psm 7")
                plate_text = plate_text.strip()

                if plate_text:
                    # Plaka bilgisini görüntü üzerinde göster
                    cv2.rectangle(frame, (x, y), (x+w, y+h), (0, 255, 0), 2)
                    cv2.putText(frame, plate_text, (x, y - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 0), 2)
                    print(f"Algılanan Plaka: {plate_text}")

        # Kameradan alınan görüntüyü ekranda göster
        cv2.imshow("Kamera - Anlık Plaka Okuma", frame)

        # 'q' tuşu ile çıkış
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    # Kaynakları serbest bırak
    cap.release()
    cv2.destroyAllWindows()

if __name__ == "__main__":
    detect_plate_live()
