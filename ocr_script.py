import cv2
import pytesseract

# Tesseract OCR'ın yolunu ayarlayın (Windows için)
pytesseract.pytesseract.tesseract_cmd = r"C:/Program Files/Tesseract-OCR/tesseract.exe"

def detect_plate_once():
    cap = cv2.VideoCapture(0)  # Kamerayı başlat
    if not cap.isOpened():
        return {"error": "Kamera açılamadı"}

    detected_plate = None
    while True:
        ret, frame = cap.read()
        if not ret:
            return {"error": "Kameradan görüntü alınamadı"}

        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        blurred = cv2.GaussianBlur(gray, (5, 5), 0)
        edged = cv2.Canny(blurred, 100, 200)

        contours, _ = cv2.findContours(edged, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)

            if w > h and w > 100 and h > 30:
                plate_region = frame[y:y + h, x:x + w]
                plate_text = pytesseract.image_to_string(plate_region, config="--psm 7").strip()
                if plate_text:
                    detected_plate = plate_text
                    cv2.rectangle(frame, (x, y), (x+w, y+h), (0,255,0), 2)
                    cv2.putText(frame, plate_text, (x, y - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0,255,0), 2)
                    break

        cv2.imshow("Kamera - Plaka Algılama", frame)

        if detected_plate:
            break

        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()
    return {"plate": detected_plate} if detected_plate else {"error": "Plaka algılanamadı"}

if __name__ == "__main__":
    result = detect_plate_once()
    print(result)
