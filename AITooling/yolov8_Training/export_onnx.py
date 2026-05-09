from ultralytics import YOLO

model = YOLO(
    "../yolov8_Training/weights/best.pt"
)

model.export(
    format="onnx",        
    imgsz=640,              
    opset=12,               
    simplify=True,          
    optimize=True,          
    dynamic=False,         
    batch=1,                
    half=False,            
    device="cpu",          
    name="ppe_yolov8_cpu"   
)

print("Export ONNX thành công!")