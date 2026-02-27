from ultralytics import YOLO
import os

# Get the directory of the current script
script_dir = os.path.dirname(os.path.abspath(__file__))
# Build the path to the model
model_path = os.path.join(script_dir, 'yolo_model', 'best.pt')
model = YOLO(model_path)


model.export(format='onnx', imgsz=640, simplify=True, opset=12)


