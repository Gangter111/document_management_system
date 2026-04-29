# Deployment Guide

Huong dan nay danh cho ban phat hanh noi bo hien tai cua QuanLyVanBan / DocumentManagement.

Neu nguoi van hanh khong phai IT, doc ban huong dan don gian hon truoc:

```text
docs/HUONG_DAN_VAN_HANH_THUC_TE.md
```

Kien truc trien khai:

- WPF client cai tren may nguoi dung.
- ASP.NET Core API cai tren mot may server noi bo.
- SQLite chi dung cho pilot nho. Voi 50-70 user, bat buoc chuyen sang SQL Server Express/Standard hoac PostgreSQL truoc rollout chinh thuc.

## 1. Yeu Cau May Server

De pilot 5-15 user:

- Windows 10/11 Pro hoac Windows Server.
- CPU 4 core tro len.
- RAM 8 GB tro len.
- O dia con trong toi thieu 20 GB.
- May server co IP tinh trong LAN.
- Port API mac dinh: `5033`.

Voi 50-70 user:

- Khuyen dung Windows Server.
- CPU 8 core tro len.
- RAM 16 GB tro len.
- DB production: SQL Server Express/Standard.
- Khong dung SQLite lam DB chinh thuc.

Thu muc khuyen dung tren server:

```powershell
C:\QuanLyVanBan\Api
C:\QuanLyVanBan\Backups
```

## 2. Build Goi API Server

### SQLite pilot

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\publish-api.ps1 `
  -Urls http://0.0.0.0:5033 `
  -DatabaseProvider Sqlite `
  -DatabasePath database/app.db `
  -JwtSecret CHANGE_THIS_TO_A_LONG_SECURE_SECRET_KEY_32_CHARS_MIN_2026
```

### SQL Server production

Dung cho rollout chinh thuc 50-70 user:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\publish-api.ps1 `
  -Urls http://0.0.0.0:5033 `
  -DatabaseProvider SqlServer `
  -ConnectionString "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True" `
  -JwtSecret CHANGE_THIS_TO_A_LONG_SECURE_SECRET_KEY_32_CHARS_MIN_2026
```

Output:

```text
D:\QuanLyVanBan\publish\api-server\app
D:\QuanLyVanBan\publish\api-server\DocumentManagement.Api-win-x64.zip
```

Copy file ZIP sang server va giai nen vao:

```text
C:\QuanLyVanBan\Api
```

## 3. Cau Hinh API Production

File can sua tren server:

```text
C:\QuanLyVanBan\Api\appsettings.Production.json
```

Cac gia tri quan trong:

```json
{
  "Database": {
    "Provider": "Sqlite",
    "Path": "database/app.db",
    "ConnectionString": "Server=localhost\\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Issuer": "DocumentManagement",
    "Audience": "DocumentManagementClient",
    "Secret": "CHANGE_THIS_TO_A_LONG_SECURE_SECRET_KEY_32_CHARS_MIN_2026",
    "AccessTokenMinutes": 60
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5033"
      }
    }
  }
}
```

Bat buoc doi `Jwt:Secret` khi cai that. Secret toi thieu 32 ky tu, nen dung chuoi dai va rieng cho tung cong ty.

Neu dung SQL Server, dat:

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost\\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Tai khoan chay API service phai co quyen doc/ghi database. Neu dung SQL authentication, thay connection string bang user rieng cho ung dung, khong dung tai khoan `sa`.

## 4. Cai API Thanh Windows Service

Mo PowerShell bang quyen Administrator tren server:

```powershell
cd C:\QuanLyVanBan\Api
powershell -ExecutionPolicy Bypass -File .\install-service.ps1
```

Kiem tra service:

```powershell
Get-Service DocumentManagement.Api
```

Go service khi can cai lai:

```powershell
cd C:\QuanLyVanBan\Api
powershell -ExecutionPolicy Bypass -File .\uninstall-service.ps1
```

Chay thu bang console, khong cai service:

```powershell
cd C:\QuanLyVanBan\Api
powershell -ExecutionPolicy Bypass -File .\run-api.ps1
```

## 5. Mo Firewall Cho API

Mo port `5033` tren Windows Firewall:

```powershell
New-NetFirewallRule `
  -DisplayName "QuanLyVanBan API 5033" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 5033 `
  -Action Allow
```

Test tu may client trong LAN:

```powershell
Invoke-WebRequest http://SERVER_IP:5033/swagger/index.html -UseBasicParsing
```

Health check production:

```powershell
Invoke-WebRequest http://SERVER_IP:5033/health -UseBasicParsing
```

## 6. Build Goi WPF Client

Chay tren may dev, thay `SERVER_IP` bang IP server that:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\publish-wpf-client.ps1 `
  -ApiBaseUrl http://SERVER_IP:5033/
```

Output:

```text
D:\QuanLyVanBan\publish\wpf-client\app
D:\QuanLyVanBan\publish\wpf-client\DocumentManagement.Wpf-win-x64.zip
```

Copy ZIP sang may nguoi dung va giai nen vao:

```text
C:\QuanLyVanBan\Client
```

Chay:

```text
C:\QuanLyVanBan\Client\DocumentManagement.Wpf.exe
```

## 7. Cau Hinh Client

File tren may client:

```text
C:\QuanLyVanBan\Client\appsettings.json
```

Gia tri can dung:

```json
{
  "Api": {
    "BaseUrl": "http://SERVER_IP:5033/"
  }
}
```

Neu doi server, sua `Api:BaseUrl`, dong app va mo lai.

## 8. Tai Khoan Kiem Thu Pilot

Tai khoan hien dung cho smoke test:

- `admin` / `admin123`
- `manager` / `manager123`
- `staff` / `staff123`

Mat khau duoc luu bang BCrypt trong bang `Users`. Truoc rollout that can doi mat khau mac dinh, toi thieu la tai khoan `admin`.

## 9. Backup SQLite Pilot

Dung SQLite pilot, file DB mac dinh nam trong:

```text
C:\QuanLyVanBan\Api\database\app.db
```

Backup thu cong:

```powershell
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
New-Item -ItemType Directory -Path C:\QuanLyVanBan\Backups -Force | Out-Null
Copy-Item C:\QuanLyVanBan\Api\database\app.db C:\QuanLyVanBan\Backups\app-$stamp.db
```

Backup thu cong nhu tren dung cho pilot. Khuyen dung:

- Backup moi ngay bang Windows Task Scheduler.
- Giu toi thieu 14 ban backup gan nhat.
- Moi tuan test restore mot lan tren may khac.

Cai lich backup tu dong hang ngay:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\install-backup-task.ps1 `
  -DatabasePath C:\QuanLyVanBan\Api\database\app.db `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -Time 23:00 `
  -RetentionDays 30
```

Chay backup ngay lap tuc:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\backup-sqlite.ps1 `
  -DatabasePath C:\QuanLyVanBan\Api\database\app.db `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -RetentionDays 30
```

Restore:

```powershell
Stop-Service DocumentManagement.Api
Copy-Item C:\QuanLyVanBan\Backups\app-YYYYMMDD-HHMMSS.db C:\QuanLyVanBan\Api\database\app.db -Force
Start-Service DocumentManagement.Api
```

## 10. Backup SQL Server Production

Thu muc backup khuyen dung:

```text
C:\QuanLyVanBan\Backups
```

Chay backup ngay lap tuc:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\backup-sqlserver.ps1 `
  -ConnectionString "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True" `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -RetentionDays 30
```

Cai lich backup hang ngay luc 23:00:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\install-sqlserver-backup-task.ps1 `
  -ConnectionString "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True" `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -Time 23:00 `
  -RetentionDays 30
```

Restore tu file `.bak`:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\restore-sqlserver.ps1 `
  -ConnectionString "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True" `
  -BackupPath C:\QuanLyVanBan\Backups\DocumentManagementDb-YYYYMMDD-HHMMSS.bak `
  -ServiceName DocumentManagement.Api
```

Luu y quan trong:

- SQL Server service account phai co quyen ghi vao `C:\QuanLyVanBan\Backups`.
- Test restore tren may khac moi tuan truoc khi rollout rong.
- Voi SQL Server production, uu tien backup bang SQL Server Agent neu dung ban Standard; voi SQL Server Express co the dung Task Scheduler script tren.
- Neu da xac nhan SQL Server ho tro backup compression, co the them tham so `-Compress` vao lenh backup/schedule.

## 11. Kiem Tra Sau Trien Khai

Tu may dev hoac may trong LAN:

```powershell
Invoke-WebRequest http://SERVER_IP:5033/swagger/index.html -UseBasicParsing
```

Tren may client:

1. Mo WPF app.
2. Dang nhap bang tai khoan test.
3. Mo Dashboard.
4. Tao mot van ban test.
5. Cap nhat bang Manager.
6. Xoa bang Admin.
7. Kiem tra van ban da xoa tra ve 404.

Neu lam tren source dev, chay:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\session-close.ps1
```

## 12. Log He Thong

API ghi log rolling theo ngay trong thu muc:

```text
C:\QuanLyVanBan\Api\logs
```

Khi co loi 500, API tra ve JSON ngan gon cho client va ghi chi tiet exception vao file log server.

## 13. Checklist Rollout Noi Bo

Server:

- IP tinh da chot.
- Port `5033` da mo firewall.
- API service dang `Running`.
- `appsettings.Production.json` da doi `Jwt:Secret`.
- Thu muc `database` co quyen ghi.
- Backup folder da tao.
- Lich backup da cau hinh.
- `/health` tra ve OK va check duoc database.

Client:

- `appsettings.json` tro dung `Api:BaseUrl`.
- May client ping duoc server.
- Dang nhap thanh cong.
- Tao/sua/xoa theo role dung nhu smoke test.

Van hanh:

- Co nguoi phu trach backup.
- Co quy trinh restore.
- Co noi ghi nhan loi nguoi dung.
- Chot SQL Server Express/Standard cho quy mo 50-70 user.

## 14. Gioi Han Ban Hien Tai

Ban hien tai phu hop pilot noi bo. Truoc rollout lon can lam tiep:

- SQL Server Express hoac PostgreSQL.
- Auto update WPF.
- Load test multi-user.
