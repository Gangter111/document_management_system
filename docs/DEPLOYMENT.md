# Deployment Guide

Huong dan nay danh cho ban phat hanh noi bo hien tai cua QuanLyVanBan / DocumentManagement.

## Production Publish Path

Use `tools/publish-production.ps1` for installer-ready publish artifacts. The script runs the required build and tests, publishes API and WPF in `Release`, creates self-contained `win-x64` outputs by default, writes artifacts under `artifacts/publish/`, refuses placeholder JWT secrets, and removes transient `.db`, `.sqlite`, and `.log` files from the publish folders.

Example:

```powershell
$env:DMS_JWT_SECRET = "<48-plus-character-secret-from-approved-secret-manager>"
powershell -ExecutionPolicy Bypass -File .\tools\publish-production.ps1 `
  -ApiBaseUrl http://SERVER_IP:5033/ `
  -ApiUrls http://0.0.0.0:5033 `
  -DatabaseProvider SqlServer `
  -ConnectionString "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True" `
  -DataRoot "C:\ProgramData\QuanLyVanBan" `
  -AttachmentsPath "C:\ProgramData\QuanLyVanBan\storage\attachments" `
  -BackupPath "C:\ProgramData\QuanLyVanBan\backups" `
  -LogFilePath "C:\ProgramData\QuanLyVanBan\logs\api-.log"
```

Outputs:

```text
artifacts/publish/api
artifacts/publish/wpf
artifacts/publish/DocumentManagement.Api-win-x64.zip
artifacts/publish/DocumentManagement.Wpf-win-x64.zip
```

The ZIPs are installer-ready inputs only. Production still requires code signing and an MSI/MSIX packaging step owned by the deployment team. Do not deploy raw publish folders to end users as the final product.

Production configuration should come from `appsettings.Production.json`, machine environment variables, or a managed secret store. Required settings:

- `Jwt:Secret`: high-entropy secret, at least 48 characters, not committed.
- `AdminSeed:Password`: omit in production unless intentionally provisioning a rotated break-glass account.
- `Storage:DataRoot`: base runtime data folder. Recommended: `C:\ProgramData\QuanLyVanBan`.
- `Database:Path` or `Database:ConnectionString`: production database location.
- `Storage:AttachmentsPath`: durable attachment storage outside the app binary folder.
- `Backup:Path`: durable backup location outside the app binary folder.
- `Logging:FilePath`: rolling log path, preferably under `C:\ProgramData\QuanLyVanBan\logs`.
- `PdfExtraction:Ocr:Enabled`: enable only when local OCR dependencies are installed and governed.
- `PdfExtraction:Ocr:TesseractPath`: path to the local Tesseract executable.
- `PdfExtraction:Ocr:TessdataPath`: path to the local Tesseract language data folder.
- `PdfExtraction:Ocr:Language`: recommended `vie+eng` when Vietnamese trained data is installed.

Environment variable names used by release scripts:

- `DMS_JWT_SECRET`: production JWT signing secret used by `tools/publish-production.ps1`.
- `DMS_ADMIN_SEED_PASSWORD`: optional one-time admin seed password for controlled provisioning.
- `ASPNETCORE_ENVIRONMENT`: set to `Production` for the API service.

First production install outline:

1. Generate a high-entropy `DMS_JWT_SECRET` with at least 48 characters using an approved password manager or secret generator.
2. Publish with `tools/publish-production.ps1`; confirm `artifacts/publish/deployment-manifest.json`.
3. Code-sign binaries and package the artifacts with the enterprise MSI/MSIX process.
4. Install API binaries under `C:\Program Files\QuanLyVanBan\Api`.
5. Create `C:\ProgramData\QuanLyVanBan` folders and lock ACLs to Administrators and the API service account.
6. Configure `appsettings.Production.json` or machine environment configuration.
7. Install/start the API Windows Service and verify `/health`.
8. Install the WPF client package and run the smoke checklist.

Admin password rotation:

- Do not rely on development accounts in production.
- Create the initial administrator through the approved provisioning process.
- If `DMS_ADMIN_SEED_PASSWORD` is used for provisioning, rotate it immediately after first login and remove the seed value.

Maintenance and restore procedure:

1. Notify users and stop normal activity.
2. Set `Maintenance:Mode=true`.
3. Set `Maintenance:RestoreEnabled=true` only for the restore window.
4. Perform restore from a verified backup.
5. Check `/health` and admin operations status.
6. Set `Maintenance:RestoreEnabled=false`.
7. Set `Maintenance:Mode=false`.

Attachment reconciliation procedure:

1. Run the reconciliation report endpoint first.
2. Review missing metadata/files and orphan counts.
3. Run cleanup only with explicit approval.
4. Keep the cleanup default as dry-run unless deleting verified file-only orphans.

Rollback procedure:

- Stop the API service.
- Restore the previous API package.
- Do not overwrite `C:\ProgramData\QuanLyVanBan`.
- Restart the service and run `/health` plus the smoke checklist.

Smoke-test checklist:

- `/health` returns healthy.
- Login works for an approved account.
- Document search/list loads.
- Create/update/delete permissions match role policy.
- Backup status and operations status endpoints do not expose local paths.
- PDF extraction handles invalid PDFs with classified failure.
- Scanned PDF extraction either runs bounded local OCR or returns a calm scanned/image-only limitation message.

Recommended installation layout:

```text
C:\Program Files\QuanLyVanBan\Api
C:\Program Files\QuanLyVanBan\Client
C:\ProgramData\QuanLyVanBan\database
C:\ProgramData\QuanLyVanBan\storage\attachments
C:\ProgramData\QuanLyVanBan\backups
C:\ProgramData\QuanLyVanBan\logs
```

Keep runtime data out of `Program Files`. Grant write access only to the API service identity and administrators. The WPF client should be installed read-only for normal users.

API hosting:

- Run the API as a Windows Service under a least-privilege service account.
- Use IIS, YARP, Nginx, or another approved reverse proxy when TLS termination, enterprise certificates, or network segmentation are required.
- Open only the required inbound port from trusted client subnets.
- Keep Swagger disabled outside Development unless explicitly required by internal operations policy.

Backup and update guidance:

- Keep at least 30 daily backups for pilot SQLite and follow the SQL Server retention policy for production SQL Server.
- Test restore on a separate machine before rollout and after backup policy changes.
- Deploy updates through a signed MSI/MSIX or enterprise software distribution system.
- Stop the Windows Service before replacing API binaries.
- Never overwrite `ProgramData` runtime data during an application update.

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
powershell -ExecutionPolicy Bypass -File .\tools\publish-production.ps1 `
  -ApiBaseUrl http://SERVER_IP:5033/ `
  -ApiUrls http://0.0.0.0:5033 `
  -DatabaseProvider Sqlite `
  -DatabasePath database/app.db
```

### SQL Server production

Dung cho rollout chinh thuc 50-70 user:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\publish-production.ps1 `
  -ApiBaseUrl http://SERVER_IP:5033/ `
  -ApiUrls http://0.0.0.0:5033 `
  -DatabaseProvider SqlServer `
  -ConnectionString "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True"
```

Output:

```text
D:\QuanLyVanBan\artifacts\publish\api
D:\QuanLyVanBan\artifacts\publish\DocumentManagement.Api-win-x64.zip
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
    "Secret": "",
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

Tai khoan nay chi dung cho Development/smoke test noi bo, khong dung cho production:

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
