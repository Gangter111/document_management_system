# Hướng Dẫn Vận Hành Thực Tế - QuanLyVanBan

**Đối tượng**: Nhân viên vận hành hệ thống | **Cập nhật lần cuối**: 2026-05-11

---

## Kiến Thức Cơ Bản

### Hệ Thống Gồm Những Gì

Hệ thống QuanLyVanBan có 3 phần chính:

1. **Máy server** - Chạy dịch vụ API và lưu cơ sở dữ liệu trung tâm
2. **Dịch vụ API** - Chương trình chạy ngầm trên server (tên dịch vụ: `DocumentManagementApi`)
3. **Máy người dùng** - Ứng dụng WPF cho phép người dùng đăng nhập và quản lý tài liệu

```
Máy người dùng  →  Gọi tới server  →  Server đọc/ghi database
```

### Thông Tin Cần Ghi Nhớ

Trước khi vận hành, ghi lại các thông tin này:

```
TÊN SERVER:           _____________________
ĐỊA CHỈ IP SERVER:    _____________________
CỔNG API:             _____________________
ĐƯỜNG DẪN API:        _____________________
THƯ MỤC CÀI API:      _____________________
THƯ MỤC SAO LƯU:      _____________________
LOẠI DATABASE:        _____________________
TÀI KHOẢN ADMIN:      _____________________
NGƯỜI PHỤ TRÁCH KTECH: _____________________
ĐIỆN THOẠI HỖ TRỢ:    _____________________
```

---

## Kiểm Tra Hàng Ngày

### Sáng: Kiểm Tra Hệ Thống

#### Cách 1: Kiểm Tra Bằng Trình Duyệt

Trên máy server, mở Chrome/Edge vào:

```
http://localhost:5033/health
```

Nếu thấy:
- ✅ Trang trả về "Healthy" → API đang chạy bình thường
- ❌ Trang không mở → API không chạy (xem [Xử Lý Sự Cố](#xử-lý-sự-cố))

Từ máy người dùng (thay `SERVER_IP` = IP server):

```
http://SERVER_IP:5033/health
```

Ví dụ: `http://192.168.1.10:5033/health`

#### Cách 2: Kiểm Tra Windows Service

Trên server, nhấn Start → Gõ `Services` → Mở ứng dụng `Services`

Tìm dịch vụ: **DocumentManagement.Api**

Trạng thái đúng:
- ✅ Status: **Running**
- ✅ Startup Type: **Automatic**

Nếu Status = **Stopped**:
1. Nhấn chuột phải → Chọn **Start**
2. Đợi 10 giây
3. Kiểm tra lại

---

### Hàng Ngày: Kiểm Tra Cơ Bản

#### 1. Dung Lượng Ổ Đĩa

Mở File Explorer → Nhấn chuột phải **Ổ C:** → Chọn **Properties**

- ✅ Dung lượng trống > 20% → OK
- ⚠️ Dung lượng trống < 20% → Cần xóa dữ liệu cũ hoặc mở rộng ổ đĩa

#### 2. Sao Lưu Mới Nhất

Kiểm tra thư mục: `C:\QuanLyVanBan\Backups\`

- ✅ Có file mới trong vòng 24 giờ → OK
- ❌ Không có file hoặc file cũ hơn 24 giờ → Chạy sao lưu thủ công

#### 3. Xem Nhật Ký API

Mở File Explorer → Vào: `C:\QuanLyVanBan\Logs\`

Mở file `.log` mới nhất bằng Notepad. Tìm từ **"ERROR"** hoặc **"FATAL"**:

- ✅ Không có lỗi → OK
- ❌ Có lỗi → Gọi người kỹ thuật

---

## Sao Lưu & Phục Hồi

### Sao Lưu Thủ Công (SQLite)

Nếu sao lưu tự động không chạy, sao lưu thủ công:

1. Mở PowerShell (Run as Administrator)
2. Nhập lệnh:

```powershell
Copy-Item `
  "C:\QuanLyVanBan\Database\app.db" `
  "C:\QuanLyVanBan\Backups\app.db.$(Get-Date -Format yyyyMMdd-HHmmss).bak"
```

3. Kiểm tra thư mục `Backups` có file mới không

### Phục Hồi Database (CHỈ DÙNG TRONG MÔI TRƯỜNG STAGING)

⚠️ **CẢNH CÁO**: Phục hồi sẽ ghi đè lên database hiện tại. **KHÔNG DÙNG trong PRODUCTION**.

1. Dừng dịch vụ API:

```powershell
Stop-Service DocumentManagementApi
```

2. Phục hồi từ file sao lưu:

```powershell
Copy-Item `
  "C:\QuanLyVanBan\Backups\app.db.20260510-140000.bak" `
  "C:\QuanLyVanBan\Database\app.db" `
  -Force
```

3. Khởi động lại dịch vụ:

```powershell
Start-Service DocumentManagementApi
```

4. Kiểm tra:

```
http://localhost:5033/health
```

---

## Quản Lý Dịch Vụ

### Khởi Động Dịch Vụ

```powershell
Start-Service DocumentManagementApi
```

### Dừng Dịch Vụ

```powershell
Stop-Service DocumentManagementApi
```

### Khởi Động Lại (Xóa bộ nhớ/kết nối)

```powershell
Restart-Service DocumentManagementApi
```

### Kiểm Tra Trạng Thái

```powershell
Get-Service DocumentManagementApi
```

---

## Xử Lý Sự Cố

### API Không Chạy

**Bước 1**: Kiểm tra dịch vụ đang chạy không?

```powershell
Get-Service DocumentManagementApi
```

- Nếu `Stopped` → Nhập: `Start-Service DocumentManagementApi`

**Bước 2**: Dung lượng ổ đĩa có đủ không?

Xem [Kiểm Tra Dung Lượng Ổ Đĩa](#hàng-ngày-kiểm-tra-cơ-bản)

- Nếu < 5% trống → Xóa sao lưu cũ hoặc file log cũ

**Bước 3**: Kiểm tra nhật ký lỗi

Mở thư mục: `C:\QuanLyVanBan\Logs\`

Tìm file `.log` mới nhất, mở bằng Notepad, tìm từ **"ERROR"**

**Bước 4**: Nếu vẫn không chạy

→ Gọi người kỹ thuật, cung cấp:
- Lỗi từ bước 3
- Dung lượng ổ đĩa
- Dung lượng trống

### Người Dùng Không Thể Đăng Nhập

**Bước 1**: API đang chạy không?

Xem [Kiểm Tra Hàng Ngày](#sáng-kiểm-tra-hệ-thống)

**Bước 2**: Kiểm tra tài khoản người dùng

- Mở ứng dụng Admin
- System → Users
- Tìm tài khoản → Kiểm tra có **Active** không (không bị khóa)

**Bước 3**: Người dùng quên mật khẩu

- Admin → System → Users → Chọn người dùng → Reset Password
- Cung cấp mật khẩu tạm thời cho người dùng

**Bước 4**: Kiểm tra server/client có cùng mạng không

- Server: `192.168.1.10`
- Client: `192.168.1.x` (phải cùng dãy)

### Performance Chậm

**Bước 1**: Dung lượng ổ đĩa

Xem [Kiểm Tra Dung Lượng Ổ Đĩa](#hàng-ngày-kiểm-tra-cơ-bản)

**Bước 2**: Database quá lớn?

```powershell
(Get-Item C:\QuanLyVanBan\Database\app.db).Length / 1MB
```

- Nếu > 500 MB → Cần xóa tài liệu cũ

**Bước 3**: Nhiều người dùng cùng lúc?

- Đợi khoảng thời gian ít người dùng

---

## Quản Lý Tài Khoản Người Dùng (Admin)

### Tạo Tài Khoản Mới

1. Mở ứng dụng Admin (hoặc login quyền Admin)
2. System → Users → Create
3. Điền thông tin:
   - Username
   - Password tạm thời
   - Role (Admin, Manager, Publisher, Staff)
   - Department (phòng ban)
4. Nhấn Create

### Thay Đổi Mật Khẩu Người Dùng

1. System → Users → Chọn người dùng
2. Nhấn "Reset Password"
3. Cấp mật khẩu tạm thời

### Khóa Tài Khoản Người Dùng

1. System → Users → Chọn người dùng
2. Nhấn "Deactivate"
3. Người dùng không thể đăng nhập

---

## Hàng Tuần: Kiểm Tra Nâng Cao

### Thứ Hai: Xem Nhật Ký Kiểm Toán

1. Login Admin
2. System → Audit Logs
3. Xem các hoạt động:
   - ✅ Không có truy cập trái phép → OK
   - ❌ Có hoạt động lạ → Ghi lại và báo cáo

### Thứ Năm: Kiểm Tra Hiệu Năng

1. Kiểm tra tốc độ ứng dụng
   - Có chậm không?
   - Có lỗi không?

2. Kiểm tra dung lượng database:

```powershell
(Get-Item C:\QuanLyVanBan\Database\app.db).Length / 1MB
```

---

## Liên Hệ Hỗ Trợ

Gọi người kỹ thuật nếu:

- ❌ API không khởi động được
- ❌ Nghi ngờ database bị hỏng
- ❌ Hiệu năng suy giảm đột ngột
- ❌ Có dấu hiệu tấn công
- ❌ Cần phục hồi database (production)

**Chuẩn bị thông tin khi gọi**:
- Lỗi từ nhật ký (C:\QuanLyVanBan\Logs\)
- Dung lượng ổ đĩa trống
- Lần gần đây nhất sao lưu

---

**Tài Liệu Liên Quan**:
- [Hướng Dẫn Triển Khai](../deployment/CHECKLIST.md)
- [Hướng Dẫn Bảo Mật](../security/SECURITY.md)
