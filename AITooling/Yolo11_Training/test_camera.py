import cv2
from ultralytics import YOLO
import os

MODEL_PATH = 'weights/best.pt' 
CONFIDENCE_THRESHOLD = 0.1  

VIOLATION_CLASSES_TO_DETECT = [
    "NO-Gloves", "NO-Goggles", "NO-Hardhat",
    "NO-Mask", "NO-Safety Vest", "Fall-Detected"
]

def main():
    # Lấy đường dẫn tuyệt đối của thư mục chứa script
    script_dir = os.path.dirname(os.path.abspath(__file__))
    model_full_path = os.path.abspath(os.path.join(script_dir, MODEL_PATH))

    if not os.path.exists(model_full_path):
        print(f"Lỗi: Không tìm thấy file model tại '{model_full_path}'")
        print("Vui lòng đảm bảo file 'best.pt' nằm đúng vị trí trong thư mục weights.")
        return

    print(f"Đang tải mô hình từ '{model_full_path}'...")
    try:
        model = YOLO(model_full_path)
    except Exception as e:
        print(f"Lỗi khi tải mô hình: {e}")
        return

    # Lọc các class cần thiết
    all_class_names = model.names
    print(f"Các lớp model có thể nhận diện: {list(all_class_names.values())}")
    
    # So khớp không phân biệt hoa thường và khoảng trắng
    violation_class_indices = [
        k for k, v in all_class_names.items() 
        if any(target.lower().strip() == v.lower().strip() for target in VIOLATION_CLASSES_TO_DETECT)
    ]

    if not violation_class_indices:
        print("\nCảnh báo: Không tìm thấy tên lớp nào khớp trong model. Sẽ hiển thị TẤT CẢ các lớp phát hiện được.")
        classes_to_predict = None 
    else:
        detected_names = [all_class_names[i] for i in violation_class_indices]
        print(f"\nĐang lọc và hiển thị các lỗi: {detected_names}")
        classes_to_predict = violation_class_indices

    # Mở camera (ID 0 thường là webcam mặc định của laptop)
    cap = cv2.VideoCapture(0)
    
    if not cap.isOpened():
        print("Lỗi: Không thể truy cập vào camera. Hãy kiểm tra xem camera có đang bị ứng dụng khác sử dụng không.")
        return

    print("\nĐang mở Camera... Nhấn phím 'q' trên cửa sổ video để dừng và thoát.")

    while True:
        success, frame = cap.read()
        if not success:
            print("Lỗi: Không thể đọc hình ảnh từ camera.")
            break

        # Dự đoán trực tiếp trên frame từ camera
        results = model.predict(frame, conf=CONFIDENCE_THRESHOLD, classes=classes_to_predict, verbose=False)
        
        # Vẽ các hộp nhận diện và nhãn lên frame
        annotated_frame = results[0].plot()

        # Hiển thị cửa sổ live preview
        cv2.imshow("PPE Detection - Live Camera Test", annotated_frame)

        # Nhấn phím 'q' để thoát
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()
    print("\nĐã dừng camera và đóng ứng dụng thành công.")

if __name__ == '__main__':
    main()