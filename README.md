# FarmIoT

ระบบ IoT สำหรับรับข้อมูลเซนเซอร์จาก HiveMQ บันทึกลง PostgreSQL และแสดงผลผ่าน React dashboard

## โครงสร้างโปรเจกต์

- `backend/MyAspNetCoreHiveMQ`: ASP.NET Core API และ MQTT service
- `frontend`: React + TypeScript dashboard
- `tests`: ชุดทดสอบ backend

## Backend

กำหนดค่าการเชื่อมต่อผ่าน environment variables ก่อนรัน backend โดยไม่ใส่ credentials ลงในไฟล์ที่ commit:

```powershell
$env:ConnectionStrings__DefaultConnection = "<PostgreSQL connection string>"
$env:HiveMQ__Broker = "<MQTT broker>"
$env:HiveMQ__Username = "<MQTT username>"
$env:HiveMQ__Password = "<MQTT password>"
```

ถ้าต้องใช้ Supabase service role ให้กำหนด `$env:Supabase__ServiceRoleKey` เพิ่มเติม

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
