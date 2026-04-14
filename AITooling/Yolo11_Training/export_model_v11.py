from ultralytics import YOLO


print("Dang tai mo hinh 'Yolo11_Training/best.pt'...")
model = YOLO('../Yolo11_Training//weights/best.pt')

print("bat dau qua trinh chuyen doi sang ONNX...")
model.export(format='onnx', imgsz=640, opset=12)

print("\nChuyen doi thanh cong!")
print("File 'best.onnx' da duoc tao trong thu muc 'Yolo11_Training/'.")

