# SafeFreeSpace

Ứng dụng Windows giúp làm giảm khả năng khôi phục nội dung các tệp đã xóa trong vùng trống, đồng thờigiữ nguyên các tệp hiện đang tồn tại.

## Phạm vi phiên bản 1

- **HDD + NTFS**: gọi `cipher.exe /w:<ổ>:\` để ghi đè vùng trống.
- **SSD/NVMe**: gửi lại lệnh TRIM bằng `Optimize-Volume -ReTrim`.
- Không format ổ, không xóa phân vùng, không Secure Erase toàn ổ.
- Không tuyên bố “không thể khôi phục 100%”.

## Kiến trúc

- C# 14 / .NET 10
- WPF + MVVM tối giản
- Worker đặc quyền riêng biệt, giao tiếp qua Named Pipe
- JSON Lines cho lịch sử và nhật ký
- xUnit cho kiểm thử

## Build

```powershell
dotnet restore --locked-mode
dotnet build -c Release
dotnet test -c Release --no-build
dotnet format --verify-no-changes
```

## Lưu ý an toàn

Đây là phần mềm có khả năng gây mất dữ liệu nếu triển khai sai. Chỉ chạy thao tác ghi đè trên volume bạn xác định rõ. Integration test ghi đè chỉ chạy trên VHD được tạo trong test và khi có biến môi trường `RUN_SAFEFREESPACE_VHD_TESTS=1`.
