# QuanLyVanBan / DocumentManagement

QuanLyVanBan là hệ thống quản lý văn bản nội bộ cho doanh nghiệp hoặc phòng ban. Người dùng làm việc trên ứng dụng Windows, dữ liệu được lưu tập trung trên một máy server nội bộ.

Tài liệu này viết theo hướng dễ hiểu cho người không chuyên IT. Các lệnh kỹ thuật chi tiết hơn nằm trong:

- `docs/DEPLOYMENT.md`
- `docs/HUONG_DAN_VAN_HANH_THUC_TE.md`

## 1. Hệ Thống Dùng Để Làm Gì

Hệ thống hỗ trợ:

- Lưu hồ sơ văn bản đến, văn bản đi hoặc văn bản nội bộ.
- Tạo, sửa, tìm kiếm, xem chi tiết và xóa mềm văn bản.
- Theo dõi trạng thái xử lý: bản nháp, chờ duyệt, đã duyệt, đã ban hành, lưu trữ, từ chối.
- Quản lý mức độ khẩn và mức độ bảo mật của văn bản.
- Đính kèm file tài liệu.
- Phân quyền theo vai trò người dùng.
- Xem dashboard thống kê.
- Ghi lịch sử thao tác để phục vụ kiểm tra sau này.
- Trích xuất nội dung cơ bản từ file PDF.

## 2. Hệ Thống Gồm Những Phần Nào

Mô hình vận hành đơn giản:

```text
Máy người dùng
  -> mở ứng dụng QuanLyVanBan
  -> kết nối về server nội bộ
  -> server đọc/ghi dữ liệu vào database
```

Các thành phần trong dự án:

- `DocumentManagement.Api`: chương trình API chạy trên server.
- `DocumentManagement.Wpf`: ứng dụng Windows cho người dùng.
- `DocumentManagement.Application`: xử lý nghiệp vụ chính.
- `DocumentManagement.Infrastructure`: kết nối database, lưu file, xuất báo cáo, backup.
- `DocumentManagement.Domain`: các đối tượng và trạng thái nghiệp vụ.
- `DocumentManagement.Contracts`: dữ liệu trao đổi giữa API và client.
- `DocumentManagement.Tests`: bộ kiểm thử tự động.

## 3. Yêu Cầu Máy Chủ Và Máy Người Dùng

Máy server pilot nhỏ:

- Windows 10/11 Pro hoặc Windows Server.
- CPU 4 nhân trở lên.
- RAM 8 GB trở lên.
- Ổ đĩa trống tối thiểu 20 GB.
- Có IP cố định trong mạng nội bộ.
- Port mặc định của API: `5033`.

Máy người dùng:

- Windows 10/11.
- Kết nối được tới server nội bộ.
- Có quyền chạy file `DocumentManagement.Wpf.exe`.

Database:

- SQLite phù hợp pilot nhỏ.
- SQL Server Express/Standard nên dùng khi triển khai thật cho nhiều người dùng.

## 4. Cách Chạy Kiểm Tra Trên Máy Phát Triển

Mở PowerShell tại thư mục dự án:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\verify.ps1 -SkipSmoke
```

Lệnh này sẽ build các project, chạy kiểm tra kiến trúc và chạy test tự động.

Chạy API thử:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\start-local-api.ps1
```

Kiểm tra API:

```text
http://localhost:5033/health
```

Chạy ứng dụng Windows:

```powershell
cd D:\QuanLyVanBan\DocumentManagement.Wpf
dotnet run
```

## 5. Cách Triển Khai Cho Người Dùng Nội Bộ

Triển khai gồm 2 việc chính:

1. Cài API trên một máy server nội bộ.
2. Cài ứng dụng WPF trên từng máy người dùng.

### Bước 1: Đóng Gói API Server

Trên máy phát triển:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\publish-api.ps1 `
  -Urls http://0.0.0.0:5033 `
  -DatabaseProvider Sqlite `
  -DatabasePath database/app.db `
  -JwtSecret CHANGE_THIS_TO_A_LONG_SECURE_SECRET_KEY_32_CHARS_MIN_2026
```

Kết quả nằm ở:

```text
D:\QuanLyVanBan\publish\api-server
```

Copy file ZIP API sang server và giải nén vào:

```text
C:\QuanLyVanBan\Api
```

### Bước 2: Cài API Thành Dịch Vụ Windows

Trên server, mở PowerShell bằng quyền Administrator:

```powershell
cd C:\QuanLyVanBan\Api
powershell -ExecutionPolicy Bypass -File .\install-service.ps1
```

Kiểm tra dịch vụ:

```powershell
Get-Service DocumentManagement.Api
```

Trạng thái đúng là `Running`.

### Bước 3: Mở Firewall

Trên server, mở port `5033`:

```powershell
New-NetFirewallRule `
  -DisplayName "QuanLyVanBan API 5033" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 5033 `
  -Action Allow
```

Từ máy người dùng, mở trình duyệt và kiểm tra:

```text
http://SERVER_IP:5033/health
```

Nếu hiện `Healthy` hoặc trang phản hồi thành công, server đang chạy.

### Bước 4: Đóng Gói Ứng Dụng WPF

Trên máy phát triển, thay `SERVER_IP` bằng IP server thật:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\publish-wpf-client.ps1 `
  -ApiBaseUrl http://SERVER_IP:5033/
```

Kết quả nằm ở:

```text
D:\QuanLyVanBan\publish\wpf-client
```

Copy file ZIP client sang máy người dùng và giải nén vào:

```text
C:\QuanLyVanBan\Client
```

Người dùng mở:

```text
C:\QuanLyVanBan\Client\DocumentManagement.Wpf.exe
```

## 6. Tài Khoản Kiểm Thử Ban Đầu

Tài khoản thường dùng cho kiểm thử pilot:

- `admin` / `admin123`
- `manager` / `manager123`
- `staff` / `staff123`

Khi triển khai thật, cần đổi mật khẩu mặc định, tối thiểu là tài khoản `admin`.

## 7. Việc Vận Hành Hằng Ngày

Người phụ trách nên kiểm tra nhanh mỗi ngày:

- Mở ứng dụng và đăng nhập được.
- Dashboard hiển thị số liệu.
- Tìm kiếm được một văn bản cũ.
- Service `DocumentManagement.Api` trên server đang `Running`.
- Thư mục backup có file mới.
- Ổ đĩa server còn đủ dung lượng.

Thư mục log API thường nằm ở:

```text
C:\QuanLyVanBan\Api\logs
```

Thư mục backup khuyến nghị:

```text
C:\QuanLyVanBan\Backups
```

## 8. Backup Và Khôi Phục

Với SQLite pilot, database thường là:

```text
C:\QuanLyVanBan\Api\database\app.db
```

Backup thủ công:

```powershell
cd D:\QuanLyVanBan
powershell -ExecutionPolicy Bypass -File .\tools\backup-sqlite.ps1 `
  -DatabasePath C:\QuanLyVanBan\Api\database\app.db `
  -BackupDirectory C:\QuanLyVanBan\Backups `
  -RetentionDays 30
```

Với SQL Server, xem lệnh chi tiết trong `docs/DEPLOYMENT.md`.

Nguyên tắc quan trọng: không chỉ giữ backup trên chính server. Nên copy thêm ra ổ cứng ngoài, NAS hoặc máy khác.

## 9. Khi Có Sự Cố

Làm theo thứ tự:

1. Ghi lại thời gian xảy ra lỗi.
2. Hỏi người dùng đang thao tác gì.
3. Chụp màn hình lỗi.
4. Kiểm tra `http://SERVER_IP:5033/health`.
5. Kiểm tra service `DocumentManagement.Api`.
6. Nếu API không phản hồi, restart service.
7. Nếu vẫn lỗi, gửi log trong `C:\QuanLyVanBan\Api\logs` cho người kỹ thuật.

Restart API:

```powershell
Restart-Service DocumentManagement.Api
```

## 10. Những Việc Không Nên Tự Làm Nếu Không Chắc

- Không xóa file trong thư mục API.
- Không sửa trực tiếp file database.
- Không xóa database trong SQL Server.
- Không tắt firewall server.
- Không đổi port API khi client đang dùng.
- Không restore database khi chưa báo người dùng tạm ngừng sử dụng.

## 11. Trạng Thái Kiểm Tra Hiện Tại

Bộ kiểm tra chính của dự án:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\verify.ps1 -SkipSmoke
```

Bộ smoke test đầy đủ có thể chạy bằng:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\verify-all.ps1
```

Smoke test sẽ khởi động API tạm thời trên port `5033`, chạy kiểm thử đăng nhập và thao tác API, sau đó tắt API.
