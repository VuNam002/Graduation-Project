import cv2
from ultralytics import YOLO

model = YOLO("../yolov8_Training/weights/best.pt")

cap = cv2.VideoCapture(0)

while True:
    ret, frame = cap.read()
    if not ret:
        break
    results = model(frame)
    results = model(frame)
    annot_frame = results[0].plot()
    cv2.imshow("YOLOv8 Inference", annot_frame)
    if cv2.waitKey(1) & 0xFF == ord("q"):
        break
cap.release()
cv2.destroyAllWindows()