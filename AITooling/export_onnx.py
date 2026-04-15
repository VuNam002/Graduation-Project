from ultralytics import YOLO

model = YOLO(r"D:\Graduation-Project\PPE_Detection_App\AITooling\Yolo11_Training\weights\best.pt")

model.export(
    format='onnx',
    imgsz=640,
    simplify=True,
    opset=12,
    half=False,
    dynamic=False,
    batch=1,
    device='cpu',
    name='bestYolov11',
)