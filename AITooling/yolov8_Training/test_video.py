import cv2
from ultralytics import YOLO
import os

# Đường dẫn tới mô hình đã được huấn luyện
model_path = os.path.join(os.path.dirname(__file__), "weights", "best.pt")
model = YOLO(model_path)

# Đường dẫn video đầu vào và đầu ra
input_video_path = "../ppe_detection_v1/video/test.mp4"  
output_video_path = "output.mp4"

# Đọc video đầu vào
cap = cv2.VideoCapture(input_video_path)

if not cap.isOpened():
    print(f"Không thể mở video đầu vào tại: {input_video_path}")
    print("Vui lòng kiểm tra lại đường dẫn input_video_path.")
    exit()

# Lấy các thông số cấu hình của video gốc
width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
fps = cap.get(cv2.CAP_PROP_FPS)

# Khởi tạo VideoWriter để lưu lại kết quả
# Mã hóa 'mp4v' phổ biến để lưu video định dạng .mp4
fourcc = cv2.VideoWriter_fourcc(*'mp4v')
out = cv2.VideoWriter(output_video_path, fourcc, fps, (width, height))

print(f"Đang xử lý video... Kết quả sẽ được lưu tại: {output_video_path}")

while True:
    ret, frame = cap.read()
    if not ret:
        break # Hết video
    
    # Chạy mô hình để nhận diện trên khung hình hiện tại
    results = model(frame, verbose=False)
    
    # Lấy khung hình đã được vẽ bounding box và nhãn
    annotated_frame = results[0].plot()
    
    # Ghi khung hình đã xử lý vào file video đầu ra
    out.write(annotated_frame)
    
    # Tùy chọn: Hiển thị cửa sổ xem trực tiếp quá trình xử lý (Bấm 'q' để thoát sớm)
    cv2.imshow("YOLOv8 Video Inference", annotated_frame)
    if cv2.waitKey(1) & 0xFF == ord("q"):
        print("Đã dừng xử lý sớm do người dùng.")
        break

# Giải phóng bộ nhớ và các luồng video
cap.release()
out.release()
cv2.destroyAllWindows()

print("Quá trình xử lý đã hoàn tất!")
