# PPE Detection & Safety Monitoring System

## 🌟 Overview / Tổng quan
**English:**
The **PPE Detection App** is an AI-powered safety monitoring solution designed for industrial environments. It uses YOLOv8 and YOLOv11 models to detect Personal Protective Equipment (PPE) compliance (hardhats, vests, masks) and critical safety events like falls in real-time.

**Tiếng Việt:**
**PPE Detection App** là giải pháp giám sát an toàn thông minh sử dụng trí tuệ nhân tạo. Hệ thống ứng dụng mô hình YOLOv8 và YOLOv11 để nhận diện việc đeo thiết bị bảo hộ (mũ, áo phản quang, khẩu trang) và các sự cố an toàn như té ngã theo thời gian thực.

---

## 🚀 Key Features / Tính năng chính
*   **Real-time Detection:** Process camera streams via WebSockets with low latency.
*   **Hybrid AI Support:** Seamlessly switch between YOLOv8 and YOLOv11.
*   **Safety Dashboard:** Comprehensive analytics on violation trends and peak hours.
*   **eKYC Integration:** Identify workers using Face Recognition.
*   **Automated Reporting:** Export violation logs to Excel for compliance auditing.

---

## 🛠 Tech Stack / Công nghệ sử dụng

### Backend (.NET 8)
*   **ASP.NET Core Web API:** Robust and scalable server architecture.
*   **ONNX Runtime:** High-performance AI inference for C#.
*   **WebSockets:** Real-time frame transmission.
*   **Entity Framework Core:** Efficient data management.

### Frontend (Next.js 14)
*   **React & TypeScript:** Type-safe frontend development.
*   **Tailwind CSS & shadcn/ui:** Modern and responsive UI components.
*   **Recharts / Chart.js:** Dynamic data visualization.

### AI & Computer Vision
*   **YOLOv8 & YOLOv11:** State-of-the-art object detection.
*   **OpenCV:** Image preprocessing and video handling.
*   **Python:** Model training and evaluation scripts.

---

## 📂 Project Structure / Cấu trúc dự án
*   `/Backend`: ASP.NET Core API source code.
*   `/frontend`: Next.js web application.
*   `/AITooling`: Training scripts, datasets, and ONNX model weights.

---

## ⚙️ Installation / Cài đặt

### Prerequisites
*   .NET 8 SDK
*   Node.js 18+
*   Python 3.9+ (for AI testing)

### Steps
1.  **Clone the repository:**
    ```bash
    git clone https://github.com/VuNam002/Graduation-Project
    ```
2.  **Setup Backend:**
    ```bash
    cd Backend/src/PPE_Detection_App.Api
    dotnet restore
    dotnet run
    ```
3.  **Setup Frontend:**
    ```bash
    cd frontend
    npm install
    npm run dev
    ```

---

## 📝 License
This project is developed for Graduation Thesis purposes.

*Developed by [Vu Ha Nam]*
