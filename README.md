# FarmIoT

ระบบ IoT สำหรับรับข้อมูลเซนเซอร์จาก HiveMQ บันทึกลง PostgreSQL และแสดงผลผ่าน React dashboard

## โครงสร้างโปรเจกต์

- `backend/MyAspNetCoreHiveMQ`: ASP.NET Core API และ MQTT service
- `frontend`: React + TypeScript dashboard
- `tests`: ชุดทดสอบ backend

## Backend

```powershell
dotnet run --project .\backend\MyAspNetCoreHiveMQ\MyAspNetCoreHiveMQ.csproj
```

API สำหรับอ่านข้อมูลเซนเซอร์:

```text
GET /api/Sensor?limit=30
```

Backend รองรับข้อมูล topic ที่ลงท้ายด้วย `temperature`, `temp`, `humidity` หรือ `hum`

## Frontend

```powershell
cd frontend
npm.cmd install
npm.cmd run dev
```

เปิดที่ `http://localhost:5173/`

Frontend จะ polling ข้อมูลจาก backend ทุก 5 วินาที
