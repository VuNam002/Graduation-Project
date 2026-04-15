import cv2
from ultralytics import YOLO
import os

MODEL_PATH = 'weights/best.pt' 
INPUT_VIDEO = '../test/test3.mp4' 

OUTPUT_DIRECTORY = 'test_results'
OUTPUT_VIDEO_NAME = 'output_video3.mp4'

CONFIDENCE_THRESHOLD = 0.02


VIOLATION_CLASSES_TO_DETECT = [
    "NO-Gloves", "NO-Goggles", "NO-Hardhat",
    "NO-Mask", "NO-Safety Vest", "Fall-Detected"
]


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))

    model_full_path = os.path.abspath(os.path.join(script_dir, MODEL_PATH))

    if not os.path.exists(model_full_path):
        print(f"Lỗi: Không tìm thấy file model tại '{model_full_path}'")
        print("Vui lòng đảm bảo bạn đã huấn luyện model và file 'best.pt' nằm đúng vị trí.")
        return

    is_url = str(INPUT_VIDEO).startswith(('http://', 'https://', 'rtmp://', 'rtsp://'))
    
    if is_url:
        video_source = INPUT_VIDEO
    else:
        video_source = os.path.abspath(os.path.join(script_dir, INPUT_VIDEO))
        if not os.path.exists(video_source):
            print(f"Lỗi: Không tìm thấy video đầu vào tại '{video_source}'")
            print("Vui lòng kiểm tra lại đường dẫn file hoặc link video.")
            return

    output_dir_full_path = os.path.join(script_dir, OUTPUT_DIRECTORY)
    os.makedirs(output_dir_full_path, exist_ok=True)
    output_path = os.path.join(output_dir_full_path, OUTPUT_VIDEO_NAME)
    print(f"Đang tải mô hình từ '{model_full_path}'...")
    try:
        model = YOLO(model_full_path)
    except Exception as e:
        print(f"Lỗi khi tải mô hình: {e}")
        return

    all_class_names = model.names
    violation_class_indices = [k for k, v in all_class_names.items() if v in VIOLATION_CLASSES_TO_DETECT]

    if not violation_class_indices:
        print("\nCảnh báo: Không tìm thấy lớp vi phạm nào được định nghĩa trong model.")
        print("Script sẽ tiếp tục và hiển thị TẤT CẢ các lớp phát hiện được.")
        classes_to_predict = None 
    else:
        print(f"\nOK! Sẽ chỉ tập trung phát hiện các lỗi sau: {VIOLATION_CLASSES_TO_DETECT}")
        classes_to_predict = violation_class_indices

    cap = cv2.VideoCapture(video_source)
    if not cap.isOpened():
        print(f"Lỗi: Không thể mở nguồn video '{video_source}'")
        return

    frame_width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    frame_height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    fps = cap.get(cv2.CAP_PROP_FPS)
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    out = cv2.VideoWriter(output_path, fourcc, fps, (frame_width, frame_height))

    print("\nBắt đầu xử lý video...")
    while cap.isOpened():
        success, frame = cap.read()
        if not success:
            break
        results = model.predict(frame, conf=CONFIDENCE_THRESHOLD, classes=classes_to_predict, verbose=False)
        annotated_frame = results[0].plot()

        out.write(annotated_frame)
    cap.release()
    out.release()
    print(f"\nHoàn tất! Video kết quả đã được lưu tại: '{os.path.abspath(output_path)}'")

if __name__ == '__main__':
    main()