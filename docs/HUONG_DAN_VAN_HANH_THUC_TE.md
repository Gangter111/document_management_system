# Huong Dan Van Hanh Thuc Te QuanLyVanBan

Tai lieu nay danh cho nguoi phu trach van hanh noi bo, khong yeu cau la IT chuyen nghiep.

Muc tieu: biet cach kiem tra he thong dang chay, sao luu du lieu, khoi dong lai khi can, xu ly cac loi thuong gap va biet khi nao can goi nguoi ky thuat.

## 1. He Thong Gom Nhung Gi

He thong QuanLyVanBan co 3 phan:

1. May server

   Day la may chay chuong trinh API va chua database trung tam. Tat ca may client se ket noi ve may nay.

2. Chuong trinh API

   Day la dich vu chay ngam tren server. Ten service mac dinh:

   ```text
   DocumentManagement.Api
   ```

3. May nguoi dung

   Moi nguoi dung mo ung dung WPF tren may tinh cua minh de dang nhap, tao va tra cuu van ban.

Mo hinh don gian:

```text
May nguoi dung
  -> goi den server
  -> server doc/ghi database trung tam
```

## 2. Thong Tin Can Ghi Lai Truoc Khi Van Hanh

Nguoi quan ly he thong nen in hoac luu lai cac thong tin sau:

```text
Ten server:
IP server:
Cong API:
Dia chi API:
Thu muc cai API:
Thu muc backup:
Loai database:
Ten database:
Tai khoan admin he thong:
Nguoi phu trach ky thuat:
So dien thoai ho tro:
```

Vi du:

```text
Ten server: SERVER-VANBAN
IP server: 192.168.1.10
Cong API: 5033
Dia chi API: http://192.168.1.10:5033
Thu muc cai API: C:\QuanLyVanBan\Api
Thu muc backup: C:\QuanLyVanBan\Backups
Loai database: SQL Server Express
Ten database: DocumentManagementDb
Tai khoan admin he thong: admin
Nguoi phu trach ky thuat: Nguyen Van A
So dien thoai ho tro: 09xx xxx xxx
```

## 3. Kiem Tra He Thong Dang Chay Hay Khong

Lam tren may server.

### Cach 1: Kiem tra bang trinh duyet

Mo Chrome/Edge tren server, vao:

```text
http://localhost:5033/health
```

Neu thay chu:

```text
Healthy
```

hoac trang tra ve thanh cong, nghia la API dang chay.

Tu may nguoi dung, thay `SERVER_IP` bang IP server:

```text
http://SERVER_IP:5033/health
```

Vi du:

```text
http://192.168.1.10:5033/health
```

### Cach 2: Kiem tra Windows Service

Tren server:

1. Bam Start.
2. Go `Services`.
3. Mo ung dung `Services`.
4. Tim service:

   ```text
   DocumentManagement.Api
   ```

Trang thai dung:

```text
Status: Running
Startup Type: Automatic
```

Neu service dang `Stopped`, bam chuot phai va chon `Start`.

## 4. Mo Ung Dung Tren May Nguoi Dung

Tren may nguoi dung, mo:

```text
C:\QuanLyVanBan\Client\DocumentManagement.Wpf.exe
```

Dang nhap bang tai khoan duoc cap.

Neu khong dang nhap duoc, kiem tra theo thu tu:

1. May nguoi dung co mang noi bo khong.
2. May nguoi dung co truy cap duoc:

   ```text
   http://SERVER_IP:5033/health
   ```

3. Server co dang chay service `DocumentManagement.Api` khong.
4. Dia chi server trong file client co dung khong.

File cau hinh client:

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

## 5. Cong Viec Hang Ngay

Moi ngay, nguoi phu trach nen kiem tra nhanh:

1. Mo ung dung va dang nhap duoc.
2. Dashboard hien so lieu.
3. Tao thu hoac tim mot van ban cu.
4. Kiem tra thu muc backup co file moi.

Thu muc backup thuong la:

```text
C:\QuanLyVanBan\Backups
```

File backup SQL Server co dang:

```text
DocumentManagementDb-YYYYMMDD-HHMMSS.bak
```

File backup SQLite co dang:

```text
app-YYYYMMDD-HHMMSS.db
```

Neu hom nay khong co file backup moi, bao nguoi ky thuat kiem tra lich backup.

## 6. Cong Viec Hang Tuan

Moi tuan nen lam:

1. Kiem tra dung luong o dia server.
2. Kiem tra thu muc backup co tang deu khong.
3. Copy mot ban backup moi nhat ra o cung ngoai, NAS, hoac may khac.
4. Nho nguoi ky thuat test restore tren may khac.

Khong chi luu backup tren chinh may server. Neu server hong o cung, backup nam tren cung o cung cung co the mat.

## 7. Sao Luu Du Lieu

### Neu dung SQL Server

Chay PowerShell tren server hoac may quan tri:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\backup-sqlserver.ps1 `
  -ConnectionString "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True" `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -RetentionDays 30
```

Neu thay:

```text
[PASS] SQL Server backup completed.
```

la thanh cong.

### Neu dung SQLite pilot

Chay:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\backup-sqlite.ps1 `
  -DatabasePath C:\QuanLyVanBan\Api\database\app.db `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -RetentionDays 30
```

Neu thay:

```text
[PASS]
```

la thanh cong.

## 8. Cai Lich Sao Luu Tu Dong

Viec nay chi can lam mot lan sau khi cai server.

Mo PowerShell bang quyen Administrator.

### SQL Server

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\install-sqlserver-backup-task.ps1 `
  -ConnectionString "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True" `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -Time 23:00 `
  -RetentionDays 30
```

### SQLite

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\install-backup-task.ps1 `
  -DatabasePath C:\QuanLyVanBan\Api\database\app.db `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -Time 23:00 `
  -RetentionDays 30
```

Sau khi cai, mo `Task Scheduler` va kiem tra co task:

```text
QuanLyVanBan SQL Server Backup
```

hoac:

```text
QuanLyVanBan SQLite Backup
```

## 9. Phuc Hoi Du Lieu Khi Co Su Co

Can can than: restore se dua database ve thoi diem file backup. Du lieu nhap sau thoi diem backup co the mat.

Truoc khi restore, nen:

1. Bao nguoi dung tam ngung su dung he thong.
2. Ghi lai thoi gian bat dau restore.
3. Sao chep file backup can restore ra noi rieng.
4. Neu khong chac, goi nguoi ky thuat.

### Restore SQL Server

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\restore-sqlserver.ps1 `
  -ConnectionString "Server=localhost\SQLEXPRESS;Database=DocumentManagementDb;Trusted_Connection=True;TrustServerCertificate=True" `
  -BackupPath C:\QuanLyVanBan\Backups\DocumentManagementDb-YYYYMMDD-HHMMSS.bak `
  -ServiceName DocumentManagement.Api
```

Sau khi restore:

1. Mo lai ung dung.
2. Dang nhap.
3. Kiem tra dashboard.
4. Tim mot vai van ban gan nhat trong ban backup.

### Restore SQLite

Lam tren server:

```powershell
Stop-Service DocumentManagement.Api
Copy-Item C:\QuanLyVanBan\Backups\app-YYYYMMDD-HHMMSS.db C:\QuanLyVanBan\Api\database\app.db -Force
Start-Service DocumentManagement.Api
```

## 10. Khoi Dong Lai API

Neu nguoi dung bao khong dang nhap duoc, hoac API bi loi, co the khoi dong lai service.

Mo PowerShell bang quyen Administrator:

```powershell
Restart-Service DocumentManagement.Api
```

Sau do kiem tra:

```text
http://localhost:5033/health
```

Neu van loi, xem phan log.

## 11. Xem Log Loi

Log API nam trong:

```text
C:\QuanLyVanBan\Api\logs
```

File log co dang:

```text
api-YYYYMMDD.log
```

Khi co loi, can gui cho nguoi ky thuat:

1. Anh chup man hinh loi tren may nguoi dung.
2. Thoi gian xay ra loi.
3. Tai khoan dang thao tac.
4. File log cua ngay hom do.

Khong tu xoa log neu chua sao chep lai.

## 12. Loi Thuong Gap Va Cach Xu Ly

### Khong mo duoc ung dung WPF

Kiem tra:

1. File `DocumentManagement.Wpf.exe` co ton tai khong.
2. May co bi antivirus chan khong.
3. Thu muc client co du file cau hinh `appsettings.json` khong.

### Dang nhap bao loi ket noi server

Kiem tra:

1. May client co mang khong.
2. Client co vao duoc `http://SERVER_IP:5033/health` khong.
3. Server co service `DocumentManagement.Api` dang `Running` khong.
4. Firewall server co mo port `5033` khong.

### Dang nhap sai mat khau

Xu ly:

1. Kiem tra dung ten dang nhap.
2. Bat/tat Caps Lock.
3. Neu van sai, lien he Admin de doi mat khau.

### Dashboard khong hien so lieu

Kiem tra:

1. Dang nhap lai.
2. Kiem tra API `/health`.
3. Kiem tra log API.

### Nguoi dung khong thay van ban can tim

Kiem tra:

1. Tu khoa tim kiem co dung khong.
2. Bo loc ngay co dang gioi han qua hep khong.
3. Tai khoan co quyen xem van ban do khong.
4. Van ban co bi xoa mem khong.

### Backup khong tao file moi

Kiem tra:

1. Task Scheduler co task backup khong.
2. Task co bi `Disabled` khong.
3. O dia backup co day khong.
4. Tai khoan chay backup co quyen ghi vao thu muc backup khong.

## 13. Nhung Viec Khong Nen Tu Lam

Nguoi khong phai IT khong nen tu lam cac viec sau neu chua duoc huong dan:

1. Xoa file trong thu muc API.
2. Sua file database truc tiep.
3. Sua connection string neu khong biet ro.
4. Xoa database trong SQL Server.
5. Tat firewall server.
6. Doi port API khi client dang dung.
7. Restore database khi chua thong bao nguoi dung.

## 14. Quy Trinh Khi Co Su Co

Lam theo thu tu:

1. Ghi lai thoi gian xay ra loi.
2. Hoi nguoi dung dang lam thao tac gi.
3. Chup man hinh loi.
4. Kiem tra `/health`.
5. Kiem tra service `DocumentManagement.Api`.
6. Restart service neu API khong phan hoi.
7. Neu van loi, lay log va goi nguoi ky thuat.

Thong tin can gui:

```text
Thoi gian loi:
May nguoi dung:
Tai khoan:
Thao tac dang lam:
Anh chup man hinh:
Ket qua /health:
File log ngay hom do:
```

## 15. Checklist Hang Ngay

```text
[ ] Dang nhap duoc ung dung
[ ] Dashboard hien so lieu
[ ] Tim kiem duoc van ban cu
[ ] Service DocumentManagement.Api dang Running
[ ] Hom nay co file backup moi
[ ] O dia server con du dung luong
```

## 16. Checklist Truoc Khi Cap Nhat Phien Ban

Truoc khi cap nhat API hoac client:

```text
[ ] Bao nguoi dung tam ngung su dung
[ ] Backup database thanh cong
[ ] Copy file backup ra noi khac
[ ] Ghi lai phien ban hien tai
[ ] Co file cai dat phien ban moi
[ ] Co cach quay lai phien ban cu neu loi
```

Sau khi cap nhat:

```text
[ ] API /health thanh cong
[ ] Dang nhap thanh cong
[ ] Dashboard hien du lieu
[ ] Tao thu mot van ban test
[ ] Tim kiem van ban test
[ ] Xem log khong co loi nghiem trong
```

## 17. Nguyen Tac Sao Luu An Toan

Nen ap dung quy tac 3-2-1:

```text
3 ban sao du lieu
2 noi luu khac nhau
1 ban nam ngoai may server
```

Vi du:

1. Database dang chay tren server.
2. Backup hang ngay trong `C:\QuanLyVanBan\Backups`.
3. Backup copy sang o cung ngoai, NAS, hoac may khac.

## 18. Lich Van Hanh De Xuat

Hang ngay:

```text
08:00 kiem tra dang nhap va dashboard
17:00 kiem tra he thong van hoat dong
23:00 backup tu dong
```

Hang tuan:

```text
Kiem tra backup
Copy backup ra noi khac
Test restore tren may test neu co
Xem log loi lon
```

Hang thang:

```text
Kiem tra tai khoan nguoi dung
Khoa tai khoan nhan vien da nghi
Kiem tra dung luong server
Kiem tra lai quyen Admin/Manager/Publisher/Staff
```

