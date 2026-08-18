# SafeFreeSpace — Đặc tả đầy đủ cho Codex


---

# README

# SafeFreeSpace — Bộ tài liệu triển khai bằng Codex

**Mục tiêu:** xây dựng ứng dụng Windows giúp người dùng làm giảm khả năng khôi phục **nội dung các tệp đã xóa trong vùng trống**, đồng thời giữ nguyên các tệp hiện đang tồn tại.

**Phạm vi an toàn của phiên bản 1:**

- HDD + NTFS: gọi công cụ hệ thống `cipher.exe /w:<ổ>:\` để ghi đè vùng trống.
- SSD/NVMe: gửi lại lệnh TRIM bằng `Optimize-Volume -ReTrim`.
- Không format ổ.
- Không xóa phân vùng.
- Không gọi `diskpart clean`, `clean all`, Secure Erase, Sanitize hay PSID Revert.
- Không tuyên bố “không thể khôi phục 100%”.
- Không thao tác khi loại ổ, ánh xạ phân vùng hoặc trạng thái an toàn chưa xác định.

## Công nghệ đã chọn

- C# 14
- .NET 10 LTS
- WPF
- Kiến trúc MVVM tối giản, không phụ thuộc framework MVVM nếu chưa cần
- JSON Lines cho lịch sử và nhật ký; không cần cơ sở dữ liệu
- xUnit cho kiểm thử
- Windows Storage/CIM APIs để nhận diện ổ
- Tiến trình đặc quyền tách riêng khỏi giao diện người dùng

## Thứ tự đọc

1. `AGENTS.md`
2. `docs/PRODUCT_SPEC.md`
3. `docs/SECURITY_MODEL.md`
4. `docs/ARCHITECTURE.md`
5. `docs/TEST_PLAN.md`
6. `docs/CODEX_EXECUTION_PLAN.md`
7. `MASTER_PROMPT.txt`

## Cách bắt đầu với Codex

Tạo một thư mục repository mới, giải nén toàn bộ bộ tài liệu vào thư mục đó, mở Codex tại thư mục repository và dán nội dung trong `MASTER_PROMPT.txt`.

Codex phải triển khai theo từng milestone, chạy build và test sau mỗi milestone, ghi tiến độ vào `PROGRESS.md`, và không chạy kiểm thử ghi đè trên ổ thật của người dùng.

## Tiêu chí hoàn thành phiên bản 1.0

- Liệt kê đúng các volume và loại ổ khi Windows cung cấp đủ dữ liệu.
- Khóa chức năng khi loại ổ là `Unknown`, ổ mạng, ổ quang, volume chỉ đọc hoặc ánh xạ nhiều lớp không đáng tin cậy.
- HDD/NTFS sử dụng `cipher.exe` với đối số đã kiểm tra.
- SSD/NVMe chỉ thực hiện ReTrim và hiển thị giới hạn rõ ràng.
- Tệp hiện có không bị thay đổi.
- Nhật ký không chứa tên tệp của người dùng.
- Có unit tests và integration tests trên VHD dùng một lần.
- Trình cài đặt hoặc bản portable đã ký là mục tiêu phát hành; bản chưa ký phải hiển thị là bản phát triển.


---

# AGENTS.MD

# AGENTS.md — Quy tắc bắt buộc cho Codex

## 1. Mục tiêu repository

Repository này xây dựng `SafeFreeSpace`, ứng dụng Windows làm sạch vùng trống trên HDD và gửi ReTrim trên SSD/NVMe, trong khi giữ nguyên dữ liệu đang tồn tại.

Đây là phần mềm có khả năng gây mất dữ liệu nếu triển khai sai. Ưu tiên số một là **an toàn, khả năng kiểm chứng và từ chối thao tác khi không chắc chắn**.

## 2. Quy tắc an toàn tuyệt đối

1. Không thêm bất kỳ lệnh hoặc API nào có thể xóa toàn bộ ổ, xóa phân vùng hoặc format:
   - `diskpart clean`
   - `diskpart clean all`
   - `format`
   - ATA Secure Erase
   - NVMe Sanitize
   - PSID Revert
   - `Clear-Disk`
   - `Remove-Partition`
2. Không ghi trực tiếp vào `\\.\PhysicalDriveN`.
3. Không mở volume ở chế độ ghi thô.
4. Không chạy thử thao tác xóa trên ổ thật của máy phát triển.
5. Integration test chỉ được chạy trên VHD/VHDX dùng một lần, có nhãn kiểm thử rõ ràng.
6. Khi phân loại ổ hoặc ánh xạ volume không chắc chắn, trả về `Unknown` và khóa nút thao tác.
7. Không nội suy ký tự ổ vào command line nếu chưa xác thực bằng biểu thức `^[A-Z]$`.
8. Không chạy thông qua `cmd.exe /c`. Khởi chạy trực tiếp executable với `ProcessStartInfo.ArgumentList`.
9. Không phân tích thông báo thành công bằng chuỗi được bản địa hóa. Dùng exit code và các trạng thái API.
10. Không ghi tên tệp, đường dẫn người dùng, nội dung tệp hoặc serial number đầy đủ vào telemetry/log mặc định.
11. Không sử dụng từ ngữ “xóa tuyệt đối”, “không thể khôi phục 100%” hoặc “đạt chuẩn pháp lý” trong UI.
12. Mọi thay đổi liên quan đến đặc quyền, process invocation, disk mapping hoặc cancellation phải có test.

## 3. Quy trình làm việc bắt buộc

Mỗi task phải theo vòng lặp:

1. Đọc tài liệu liên quan.
2. Viết hoặc cập nhật test trước khi sửa hành vi quan trọng.
3. Triển khai thay đổi nhỏ, có thể review.
4. Chạy:
   ```powershell
   dotnet restore
   dotnet build -c Release
   dotnet test -c Release --no-build
   dotnet format --verify-no-changes
   ```
5. Tự review diff về:
   - mất dữ liệu
   - command injection
   - privilege escalation
   - deadlock
   - cancellation
   - log rò rỉ dữ liệu
6. Sửa đến khi toàn bộ test xanh.
7. Cập nhật `PROGRESS.md`.
8. Chỉ chuyển milestone khi tiêu chí nghiệm thu đã đạt.

## 4. Kiến trúc và quy ước mã

- Target framework: `net10.0-windows10.0.19041.0`.
- Bật:
  - `Nullable`
  - `ImplicitUsings`
  - .NET analyzers
  - warnings as errors trong CI
- UI không chạy thường trực với quyền Administrator.
- Tác vụ đặc quyền nằm trong `SafeFreeSpace.ElevatedWorker`.
- Worker chỉ nhận message typed, không nhận command string tùy ý.
- Giao tiếp bằng Named Pipe giới hạn ACL cho SID người dùng hiện tại và token một lần.
- Core không tham chiếu WPF hoặc `System.Management`.
- Infrastructure không chứa logic trình bày.
- Không thêm package mới nếu BCL hoặc Windows API đã đáp ứng.
- Pin dependency và kiểm tra license trước khi thêm.
- Dùng `CancellationToken` xuyên suốt.
- Tất cả process phải redirect stdout/stderr và có timeout/kill-tree được kiểm soát.
- Mọi thời gian lưu UTC; UI hiển thị giờ địa phương.
- Log dạng JSONL dưới `%LocalAppData%\SafeFreeSpace\Logs`.

## 5. Chính sách kiểm thử

- Unit tests không yêu cầu admin.
- Integration tests có thuộc tính/category riêng.
- Integration tests mặc định bị skip.
- Chỉ chạy khi:
  - tiến trình có quyền admin;
  - biến môi trường `RUN_SAFEFREESPACE_VHD_TESTS=1`;
  - volume kiểm thử được tạo trong chính lần chạy hiện tại;
  - nhãn volume bắt đầu bằng `SFS_TEST_`.
- Test teardown phải cố gắng detach và xóa VHD ngay cả khi test lỗi.
- Không dùng ổ C, D hoặc bất kỳ drive letter cố định nào; chọn ký tự trống động.

## 6. Yêu cầu báo cáo của Codex

Sau mỗi milestone, báo cáo ngắn:

- Files changed
- Build result
- Test result
- Rủi ro còn lại
- Việc tiếp theo

Không tuyên bố hoàn thành khi chưa chạy test tương ứng.


---

# ĐẶC TẢ SẢN PHẨM

# Đặc tả sản phẩm — SafeFreeSpace

## 1. Vấn đề cần giải quyết

Khi người dùng xóa một tệp, hệ điều hành thường chỉ đánh dấu vùng lưu trữ là có thể tái sử dụng. Trên HDD, nội dung cũ có thể còn trong các cluster chưa cấp phát cho đến khi bị ghi đè. Trên SSD/NVMe, TRIM và bộ điều khiển flash xử lý việc thu hồi khối theo cách không cho phần mềm cấp hệ điều hành kiểm soát hoàn toàn từng ô NAND.

Ứng dụng phải giúp người dùng xử lý **vùng trống** mà không xóa tệp hiện có, đồng thời giải thích trung thực phạm vi và giới hạn.

## 2. Người dùng mục tiêu

- Người dùng Windows chuẩn bị bàn giao máy nhưng vẫn cần giữ một số dữ liệu.
- Doanh nghiệp nhỏ muốn làm sạch vùng trống định kỳ trên HDD.
- Kỹ thuật viên cần một giao diện an toàn thay cho việc gõ lệnh thủ công.
- Người dùng không chuyên cần biết ổ là HDD, SSD hay chưa xác định.

## 3. Tuyên bố sản phẩm được phép

### HDD/NTFS

Được phép hiển thị:

> Đã ghi đè vùng trống của volume bằng công cụ hệ thống Windows. Nội dung tệp đang tồn tại không được chọn để xóa.

Phải kèm:

> Các bản sao trong Recycle Bin, Shadow Copies, File History, backup, cloud sync, pagefile, hibernation, ứng dụng khác hoặc metadata hệ thống có thể vẫn tồn tại.

### SSD/NVMe

Được phép hiển thị:

> Đã gửi lại yêu cầu TRIM cho vùng trống.

Phải kèm:

> TRIM không phải Secure Erase và không bảo đảm mọi ô nhớ vật lý đã được xóa. Muốn bảo đảm cao hơn phải sao lưu dữ liệu cần giữ, sanitize toàn ổ bằng công cụ phù hợp rồi chép dữ liệu trở lại.

## 4. Ngoài phạm vi phiên bản 1

- Xóa an toàn một tệp cụ thể.
- Xóa MFT record hoặc tên file đã xóa.
- Xóa Shadow Copies.
- Xóa lịch sử trình duyệt hoặc cache ứng dụng.
- Xóa pagefile, hiberfil hoặc crash dump.
- Sanitize toàn ổ.
- Hỗ trợ macOS/Linux.
- Chứng nhận tuân thủ pháp lý.
- Cloud telemetry.
- Tự động chạy theo lịch.
- Tự động cập nhật không có chữ ký.

## 5. User journey

### 5.1 Màn hình chính

Ứng dụng hiển thị danh sách volume dạng card:

- Drive letter
- Volume label
- File system
- Dung lượng tổng
- Dung lượng trống
- Model ổ vật lý, đã rút gọn
- Media type: HDD / SSD / SCM / Unknown
- Bus type: SATA / NVMe / USB / RAID / Virtual / Unknown
- Health status
- System volume / boot volume
- BitLocker: Unlocked / Locked / Unknown
- Action được đề xuất
- Lý do bị khóa nếu không đủ điều kiện

### 5.2 Hành động với HDD

Điều kiện:

- Drive letter hợp lệ.
- Fixed hoặc removable local volume.
- File system NTFS.
- Media type xác định chắc chắn là HDD.
- Volume không read-only.
- Volume đang mounted và có thể truy cập.
- Không thuộc cluster/shared volume không hỗ trợ.
- BitLocker không ở trạng thái locked.
- Worker có quyền admin.

Luồng:

1. Người dùng nhấn `Làm sạch vùng trống`.
2. Hiện trang preflight.
3. Hiển thị dung lượng trống, cảnh báo, phạm vi.
4. Yêu cầu nhập chính xác:
   `WIPE FREE SPACE X:`
5. Worker chạy trực tiếp:
   `cipher.exe /w:X:\`
6. UI hiển thị:
   - trạng thái đang chạy;
   - thời gian đã chạy;
   - output được rút gọn;
   - progress dạng indeterminate vì Cipher không có giao thức phần trăm ổn định.
7. Exit code 0 → `Completed`.
8. Exit code khác → `Failed` với mã lỗi và hướng dẫn.
9. Nếu hủy → `Interrupted`; không báo thành công.

### 5.3 Hành động với SSD/NVMe

Điều kiện:

- Drive letter hợp lệ.
- Volume local.
- Media type SSD hoặc bus NVMe với bằng chứng nhất quán.
- Volume không read-only và đang mounted.
- Worker có quyền admin.

Luồng:

1. Người dùng nhấn `Gửi lại TRIM`.
2. Hiển thị giới hạn của TRIM.
3. Yêu cầu nhập:
   `RETRIM X:`
4. Worker chạy Windows PowerShell không profile:
   ```powershell
   Optimize-Volume -DriveLetter X -ReTrim -Verbose -ErrorAction Stop
   ```
5. Dựa vào exit code để quyết định thành công.
6. Không chuyển trạng thái thành “đã xóa tuyệt đối”.

### 5.4 Loại ổ Unknown

- Không đoán.
- Không dùng model-name heuristic làm nguồn duy nhất.
- Nút hành động bị khóa.
- Cung cấp nút `Sao chép thông tin chẩn đoán` đã ẩn serial number.
- Có hướng dẫn người dùng kiểm tra bằng công cụ của nhà sản xuất.

## 6. Preflight checks

Ứng dụng phải kiểm tra:

- Có tác vụ khác đang chạy không.
- Volume còn mounted không.
- Drive letter có thay đổi từ lúc scan không.
- Volume GUID vẫn trùng.
- File system và media type chưa thay đổi.
- Volume không read-only.
- Dung lượng trống đọc được.
- Worker elevation thành công.
- Power/battery:
  - nếu laptop đang dùng pin và pin thấp, cảnh báo hoặc khóa;
  - không yêu cầu trên desktop.
- Dirty bit:
  - nếu phát hiện volume dirty, khóa và yêu cầu kiểm tra ổ trước.
- System drive:
  - cho phép ở `Advanced mode`;
  - yêu cầu cảnh báo tăng cường;
  - khuyên đóng ứng dụng;
  - không hứa làm sạch pagefile/hiberfil/metadata.

## 7. Xác nhận và chống chọn nhầm

- Luôn hiển thị drive letter lớn.
- Hiển thị volume label và capacity.
- Confirmation phrase có drive letter.
- Nếu scan lại cho ra volume GUID khác, hủy thao tác.
- Sau khi bấm xác nhận, chờ 3 giây trước khi cho phép nút `Start`.
- Không hỗ trợ chọn nhiều ổ trong phiên bản 1.
- Không có thao tác “one click”.

## 8. Cancellation

- `Cancel` gửi yêu cầu tới worker.
- Worker dừng tiến trình con bằng quy trình kiểm soát:
  1. đánh dấu cancellation;
  2. chờ grace period ngắn;
  3. kill process tree nếu vẫn chạy;
  4. thu stdout/stderr còn lại;
  5. trả `Interrupted`.
- UI giải thích rằng thao tác bị dừng không được coi là hoàn thành.
- Sau cancellation phải rescan volume.
- Không tự động chạy lại.

## 9. Logging và riêng tư

Mỗi operation log:

- OperationId
- Timestamp UTC
- AppVersion
- ActionType
- DriveLetter
- VolumeGuid được hash với salt cục bộ
- MediaType
- BusType
- FileSystem
- Capacity/FreeSpace làm tròn
- Start/End/Duration
- ExitCode
- Result
- ErrorCategory
- Output đã lọc

Không log:

- Tên file
- Đường dẫn file
- Nội dung file
- Username đầy đủ
- Serial number đầy đủ
- Recovery key
- BitLocker key material
- Environment variables
- Command line chứa dữ liệu chưa kiểm tra

Log retention mặc định 30 ngày, người dùng có thể xóa lịch sử.

## 10. Khả năng truy cập và ngôn ngữ

- Tiếng Việt là ngôn ngữ mặc định.
- Tách resource strings để thêm tiếng Anh.
- Hỗ trợ keyboard navigation.
- Không chỉ dùng màu để truyền trạng thái.
- Text cảnh báo dễ đọc, không dùng thuật ngữ kỹ thuật nếu không có giải thích.
- High DPI và scaling Windows.

## 11. Tiêu chí nghiệm thu chức năng

1. Sentinel files trước và sau thao tác có SHA-256 giống nhau.
2. App không phát lệnh destructive toàn ổ.
3. HDD/NTFS gọi đúng `cipher.exe`.
4. SSD/NVMe gọi đúng ReTrim.
5. Unknown bị khóa.
6. Lỗi quyền admin được xử lý rõ ràng.
7. Hủy tác vụ không báo thành công.
8. Không log tên file.
9. App phục hồi được sau crash và đánh dấu tác vụ trước là `Abandoned`.
10. Không cho chạy hai operation đồng thời.


---

# MÔ HÌNH AN TOÀN

# Mô hình an toàn và giới hạn

## 1. Threat model

### Tài sản cần bảo vệ

- Dữ liệu hiện có trên mọi volume.
- Quyền Administrator.
- Quyền riêng tư của tên file/đường dẫn.
- Tính toàn vẹn của app và worker.
- Sự chính xác của volume selection.

### Mối đe dọa chính

1. Chọn nhầm ổ.
2. Drive letter được gán lại giữa lúc scan và chạy.
3. Command injection.
4. UI bị compromise và gửi command tùy ý cho worker.
5. Named pipe bị process khác chiếm.
6. RAID/Storage Spaces bị phân loại sai.
7. App crash trong lúc chạy.
8. Log rò rỉ thông tin nhạy cảm.
9. Người dùng hiểu sai TRIM là Secure Erase.
10. Test vô tình chạy trên ổ thật.

## 2. Biện pháp giảm thiểu

| Rủi ro | Biện pháp |
|---|---|
| Chọn nhầm ổ | Confirmation phrase chứa drive letter, label, capacity, delay 3 giây |
| Drive letter đổi | So sánh Volume GUID tại worker ngay trước chạy |
| Command injection | Chỉ chấp nhận char A–Z; `ArgumentList`; không dùng shell |
| Worker bị lạm dụng | Allow-list typed operations, nonce một lần, pipe ACL |
| Pipe hijack | Random name, SID ACL, nonce, protocol version |
| Phân loại sai | Nhiều nguồn, mâu thuẫn → Unknown, fail closed |
| Crash | Append-only operation journal và trạng thái Abandoned |
| Log leak | Redaction, không log filename/path/serial đầy đủ |
| Hiểu sai SSD | UI bắt buộc hiển thị disclaimer trước khi ReTrim |
| Test phá ổ | VHD-only guard + environment flag + test volume label |

## 3. Fail-closed rules

Ứng dụng phải từ chối thao tác khi:

- Không xác định được Volume GUID.
- Drive letter không phải A–Z.
- Volume không local.
- File system không hỗ trợ.
- Media type Unknown cho HDD wipe.
- Mapping tới nhiều physical disk chưa được hỗ trợ.
- Volume read-only hoặc locked.
- Volume vừa thay đổi.
- Worker không xác thực được UI.
- Tool hệ thống không nằm trong đường dẫn Windows kỳ vọng.
- Có operation khác đang chạy.
- Integration test target không có nhãn `SFS_TEST_`.

## 4. Nội dung không được xóa bởi app

App phải công khai rằng tác vụ vùng trống không xử lý chắc chắn:

- Recycle Bin chưa được làm rỗng.
- Volume Shadow Copies/System Restore.
- File History.
- Backup ngoài ổ.
- Cloud sync/version history.
- Pagefile.
- Hibernation file.
- Crash dumps.
- Temporary files vẫn đang tồn tại.
- Browser/app databases.
- Nội dung còn trong RAM.
- Tên file hoặc metadata còn trong NTFS MFT.
- Dữ liệu trong bad sectors/remapped sectors.
- SSD over-provisioned/spare areas.
- Controller cache hoặc firmware-managed NAND.

## 5. Ngôn ngữ UI bắt buộc

### Không dùng

- “Xóa vĩnh viễn 100%”
- “Không thể khôi phục”
- “Chuẩn quân đội”
- “DoD certified”
- “NIST certified”
- “Xóa sạch mọi dấu vết”

### Dùng

- “Ghi đè vùng trống trên HDD”
- “Gửi lại yêu cầu TRIM trên SSD/NVMe”
- “Giảm khả năng khôi phục nội dung đã xóa”
- “Không xử lý các bản sao, backup hoặc metadata ngoài phạm vi”
- “Không phải Secure Erase toàn ổ”

## 6. Privilege boundary

UI là process không đặc quyền.

Worker:

- nhỏ nhất có thể;
- không load plugin;
- không đọc cấu hình tùy ý từ thư mục user nếu cấu hình có thể thay đổi command;
- không thực thi script file;
- không mở URL;
- không update;
- chỉ xử lý một operation;
- thoát sau khi trả terminal result.

## 7. Supply-chain

- `Directory.Packages.props` quản lý version tập trung.
- Lock file dependency.
- Chỉ dùng package có nguồn rõ ràng.
- CI kiểm tra vulnerability.
- GitHub Actions hoặc CI action phải pin commit SHA.
- Release phải ký.
- Không nhúng SDelete hoặc tool bên thứ ba nếu chưa có quyền phân phối.
- Không tải executable tại runtime.

## 8. Review checklist cho mọi PR

- Có thể xóa dữ liệu đang tồn tại không?
- Có thể chọn nhầm volume không?
- Input nào tới process?
- Có đường dẫn shell injection không?
- Có race giữa scan và execute không?
- Có log dữ liệu riêng tư không?
- Cancellation có báo sai thành công không?
- Unknown có bị xử lý như HDD/SSD không?
- Test có thể chạm ổ thật không?
- UI có overclaim không?


---

# KIẾN TRÚC KỸ THUẬT

# Kiến trúc kỹ thuật — SafeFreeSpace

## 1. Cấu trúc solution

```text
SafeFreeSpace/
├─ SafeFreeSpace.sln
├─ Directory.Build.props
├─ Directory.Packages.props
├─ global.json
├─ AGENTS.md
├─ README.md
├─ PROGRESS.md
├─ docs/
├─ src/
│  ├─ SafeFreeSpace.App/
│  ├─ SafeFreeSpace.Core/
│  ├─ SafeFreeSpace.Infrastructure.Windows/
│  ├─ SafeFreeSpace.ElevatedWorker/
│  └─ SafeFreeSpace.Contracts/
├─ tests/
│  ├─ SafeFreeSpace.Tests.Unit/
│  └─ SafeFreeSpace.Tests.Integration/
└─ tools/
   └─ SafeFreeSpace.TestVolumeTool/
```

## 2. Trách nhiệm từng project

### SafeFreeSpace.Contracts

Chỉ chứa DTO/message contract:

- `WorkerRequest`
- `WorkerResponse`
- `OperationProgress`
- `VolumeIdentity`
- `OperationResult`
- protocol version

Không tham chiếu WPF.

### SafeFreeSpace.Core

Domain logic thuần:

- `DriveMediaType`
- `DriveBusType`
- `VolumeSnapshot`
- `EligibilityDecision`
- `OperationPlan`
- `SafetyPolicy`
- `ConfirmationPhraseService`
- `ILogRedactor`
- interfaces:
  - `IVolumeInventory`
  - `IPrivilegedOperationClient`
  - `IOperationHistory`
  - `IClock`
  - `IHashService`

Core không gọi process và không dùng WMI.

### SafeFreeSpace.Infrastructure.Windows

- Scan storage qua CIM/WMI.
- Map volume → partition → physical disk.
- BitLocker state.
- Dirty/read-only/system/boot flags.
- JSONL history.
- Named pipe client.
- Windows power status.
- Process-independent utilities.

### SafeFreeSpace.ElevatedWorker

Executable nhỏ, yêu cầu elevation khi khởi chạy.

Chỉ hỗ trợ allow-list:

- `RefreshVolumeIdentity`
- `WipeHddFreeSpace`
- `RetrimSsd`
- `CancelOperation`

Không hỗ trợ truyền executable hoặc command line tùy ý.

### SafeFreeSpace.App

- WPF UI.
- MVVM.
- Navigation.
- Dialog xác nhận.
- Progress, cancellation và history.
- Không trực tiếp chạy lệnh admin.

### TestVolumeTool

Tạo và hủy fixed VHD dùng riêng cho integration test. Không được dùng trong bản phát hành production.

## 3. Domain model đề xuất

```csharp
public enum DriveMediaType
{
    Unknown = 0,
    Hdd,
    Ssd,
    Scm
}

public enum DriveBusType
{
    Unknown = 0,
    Sata,
    Nvme,
    Usb,
    Sas,
    Raid,
    Virtual,
    StorageSpaces
}

public sealed record VolumeIdentity(
    string DriveLetter,
    string VolumeGuid,
    string? Label,
    string FileSystem,
    long CapacityBytes,
    long FreeBytes,
    bool IsSystem,
    bool IsBoot,
    bool IsReadOnly,
    bool IsDirty,
    BitLockerState BitLockerState,
    DriveMediaType MediaType,
    DriveBusType BusType,
    string? RedactedModel,
    string? HealthStatus);
```

`VolumeGuid` phải được re-check ngay trước khi chạy.

## 4. Storage discovery

### 4.1 Nguồn ưu tiên

Dùng namespace:

`root/Microsoft/Windows/Storage`

Các class có thể dùng:

- `MSFT_Volume`
- `MSFT_Partition`
- `MSFT_Disk`
- `MSFT_PhysicalDisk`

Fallback:

- `Win32_LogicalDisk`
- `Win32_DiskPartition`
- `Win32_DiskDrive`

### 4.2 Quy tắc phân loại

- `MediaType` do Windows Storage API báo là nguồn chính.
- `BusType=NVMe` hỗ trợ xác nhận SSD, nhưng không ghi đè một kết quả mâu thuẫn.
- Model-name heuristic chỉ dùng để hiển thị gợi ý, không cấp quyền chạy.
- RAID, Storage Spaces, VHD, SAN hoặc mapping nhiều physical disk:
  - mặc định `Unknown`;
  - khóa HDD wipe;
  - ReTrim chỉ được mở khi Windows báo rõ capability và test đã bao phủ.
- Khi có hai nguồn mâu thuẫn, kết quả là `Unknown`.

### 4.3 Volume identity

Trước thao tác:

1. Scan tại UI.
2. Tạo `OperationPlan` với Volume GUID và facts.
3. Worker scan lại bằng quyền admin.
4. So sánh GUID, drive letter, file system, media type, flags.
5. Mismatch → từ chối.

## 5. Elevated worker và named pipe

### 5.1 Nguyên tắc

- UI chạy quyền thường.
- Khi cần thao tác, UI tạo:
  - pipe name ngẫu nhiên;
  - nonce 256-bit;
  - operation id.
- Launch worker bằng `Verb=runas`.
- Worker kết nối pipe.
- Named pipe ACL chỉ cho SID người dùng hiện tại và LocalSystem.
- First message phải chứa protocol version và nonce.
- Nonce chỉ dùng một lần.
- Message có size limit.
- Serialize bằng `System.Text.Json`.
- Không dùng binary formatter.

### 5.2 State machine

```text
Idle
→ Preparing
→ AwaitingElevation
→ Preflight
→ Running
→ Completed | Failed | Interrupted
```

Mọi transition phải hợp lệ và có test.

## 6. Process execution

### 6.1 HDD

Executable:

```text
%SystemRoot%\System32\cipher.exe
```

Arguments:

```text
/w:X:\
```

Khởi chạy bằng `ProcessStartInfo`:

- `UseShellExecute=false`
- `RedirectStandardOutput=true`
- `RedirectStandardError=true`
- `CreateNoWindow=true`
- `ArgumentList.Add(...)`
- Working directory là thư mục hệ thống an toàn
- không dùng string concatenation ngoài drive letter đã validate

Không dựa vào nội dung text để xác định thành công; dùng exit code.

### 6.2 SSD/NVMe

Executable:

```text
%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe
```

Arguments riêng biệt:

```text
-NoLogo
-NoProfile
-NonInteractive
-Command
Optimize-Volume -DriveLetter 'X' -ReTrim -Verbose -ErrorAction Stop
```

Drive letter chỉ là một ký tự A–Z đã validate.

Có thể thay thế bằng API Storage Management trực tiếp ở phiên bản sau, nhưng MVP ưu tiên hành vi Windows chính thức dễ kiểm chứng.

### 6.3 Timeout

Không đặt timeout cứng cho HDD wipe vì thời gian phụ thuộc dung lượng và tốc độ ổ.

- heartbeat giữa worker và UI;
- elapsed time;
- cancellation;
- nếu pipe mất kết nối, worker tiếp tục trong grace period rồi dừng để tránh tác vụ mồ côi;
- history đánh dấu `Abandoned` nếu app trước đó chết mà không có terminal result.

## 7. UI architecture

ViewModels:

- `MainViewModel`
- `VolumeCardViewModel`
- `PreflightViewModel`
- `OperationViewModel`
- `HistoryViewModel`
- `SettingsViewModel`
- `AboutViewModel`

Services:

- `INavigationService`
- `IDialogService`
- `IClipboardService`
- `IAppUpdateService` — interface, chưa bật ở MVP

UI pages:

1. Dashboard
2. Volume details
3. Confirmation
4. Running operation
5. Result
6. History
7. Settings/About

## 8. Nhật ký JSONL

File:

```text
%LocalAppData%\SafeFreeSpace\Logs\operations-YYYY-MM.jsonl
```

Ghi append-only, một JSON object mỗi dòng.

Crash recovery:

- Khi bắt đầu: ghi `Started`.
- Khi hoàn thành: ghi record kết quả mới liên kết cùng OperationId.
- Khi khởi động, operation có `Started` nhưng không có terminal event được hiển thị `Abandoned`.

Không sửa record cũ để tránh file corruption.

## 9. Error taxonomy

```csharp
public enum OperationErrorCategory
{
    None,
    ElevationDenied,
    VolumeChanged,
    UnsupportedVolume,
    UnknownMediaType,
    VolumeLocked,
    VolumeReadOnly,
    VolumeDirty,
    ToolNotFound,
    ProcessStartFailed,
    ProcessExitedWithError,
    PipeAuthenticationFailed,
    Cancelled,
    AppDisconnected,
    Unexpected
}
```

UI hiển thị message thân thiện; log giữ error code đã lọc.

## 10. Packaging

MVP:

- x64 self-contained publish.
- Không bundle PowerShell hoặc Windows tools.
- App manifest cho worker yêu cầu admin.
- UI manifest `asInvoker`.
- Bản phát triển ghi rõ `DEV BUILD`.

Release:

- ký Authenticode cho UI và worker;
- hash SHA-256;
- tạo release notes;
- installer phải verify signature;
- auto-update chỉ triển khai sau khi có ký số và rollback.

## 11. Update module — phase sau

Không nằm trong MVP.

Nếu triển khai:

- lấy manifest qua HTTPS;
- manifest được ký;
- verify signature và hash trước install;
- không chạy script tải từ mạng;
- người dùng xác nhận;
- có rollback;
- update worker và UI theo atomic package;
- test downgrade/rollback.


---

# KẾ HOẠCH KIỂM THỬ

# Kế hoạch kiểm thử

## 1. Mục tiêu

Chứng minh rằng:

- Logic an toàn từ chối mục tiêu không phù hợp.
- Command được tạo đúng và không injection.
- Tệp đang tồn tại không đổi.
- HDD operation ghi đè nội dung mẫu đã xóa trong VHD kiểm thử.
- SSD operation chỉ gọi ReTrim.
- Cancellation và crash không báo thành công.
- Log không chứa tên file hoặc đường dẫn người dùng.

## 2. Unit tests

### 2.1 Drive letter validation

Cases:

- `C` → valid
- `c` → normalized `C`
- `C:` → parser UI có thể normalize nhưng worker contract chỉ nhận `C`
- `C:\` → reject tại worker contract
- `&&`, whitespace, quote, Unicode lookalike → reject
- empty/null → reject

### 2.2 Eligibility policy

- HDD + NTFS + local + unlocked + clean → allowed HDD wipe.
- SSD + NTFS → HDD wipe denied; ReTrim allowed.
- NVMe + Unknown media nhưng nguồn mapping chưa chắc chắn → không tự động cấp phép nếu policy yêu cầu consistency.
- Unknown → denied.
- Network/optical/read-only/dirty/locked → denied.
- Multi-disk/RAID → denied trong MVP.
- System volume → advanced confirmation required.

### 2.3 Confirmation phrase

- Exact ordinal comparison.
- Drive letter mismatch → denied.
- Extra whitespace → policy xác định rõ; khuyến nghị trim đầu/cuối nhưng không bỏ nội dung.
- Culture-independent.

### 2.4 Command builder

HDD:

- executable đúng System32.
- chỉ một argument `/w:X:\`.
- không dùng `cmd.exe`.
- không dùng raw concatenated command line.

SSD:

- executable đúng Windows PowerShell.
- `-NoProfile`, `-NonInteractive`.
- drive letter nằm trong script literal từ char hợp lệ.
- không chấp nhận chuỗi ngoài A–Z.

### 2.5 Worker protocol

- nonce sai → reject.
- protocol version sai → reject.
- message quá lớn → reject.
- operation không allow-list → reject.
- request thứ hai trên worker one-shot → reject.
- pipe user SID khác → reject nếu test môi trường hỗ trợ.

### 2.6 Logging

- path/filename giả không xuất hiện trong JSONL.
- serial được redacted/hash.
- recovery key không bao giờ serialize.
- exception message được lọc.
- malformed line không làm app crash.

### 2.7 State machine

Test toàn bộ transition hợp lệ và bất hợp lệ.

## 3. Integration tests trên fixed VHD

### 3.1 Guard bắt buộc

Test chỉ chạy khi:

```text
RUN_SAFEFREESPACE_VHD_TESTS=1
```

và process elevated.

Target volume phải:

- được tạo trong test process;
- có generated GUID trong registry nội bộ test;
- có label `SFS_TEST_<random>`;
- không phải system/boot;
- nằm trong file VHD dưới thư mục temp test;
- được detach trong `finally`.

### 3.2 Tạo test volume

PowerShell/diskpart helper:

1. Tạo fixed VHD khoảng 512 MB–1 GB.
2. Attach.
3. Initialize GPT.
4. Create single partition.
5. Format NTFS.
6. Gán drive letter trống động.
7. Set label `SFS_TEST_<id>`.

Không hard-code `D:`.

### 3.3 Sentinel integrity test

1. Tạo nhiều sentinel files:
   - small text
   - 1 MB binary random
   - nested directory
2. Tính SHA-256.
3. Chạy HDD wipe.
4. Tính lại SHA-256.
5. Assert file count, length, timestamps quan trọng và hash không đổi.

### 3.4 Deleted-pattern test

1. Sinh pattern ngẫu nhiên có entropy cao, tối thiểu 64 bytes.
2. Ghi lặp pattern vào file lớn, flush-to-disk.
3. Xóa file bằng direct delete, không Recycle Bin.
4. Chạy `cipher /w`.
5. Dismount VHD.
6. Scan byte toàn file fixed VHD.
7. Assert pattern nội dung không còn.
8. Không dùng filename làm pattern vì metadata có thể giữ tên.

Lưu ý: test này kiểm tra nội dung mẫu trong image, không chứng nhận sanitization toàn diện.

### 3.5 Cancellation test

1. VHD đủ lớn để operation kéo dài.
2. Start.
3. Cancel sau khi nhận trạng thái Running.
4. Assert result `Interrupted`.
5. Sentinel hashes không đổi.
6. Volume còn mount/readable.
7. Có thể chạy operation mới sau rescan.

### 3.6 Crash/disconnect test

- Kill UI, worker xử lý grace policy.
- Khởi động lại app.
- Operation history hiển thị `Abandoned` hoặc terminal result nếu worker đã gửi.
- Không hiển thị `Completed` khi thiếu terminal event.

## 4. SSD/ReTrim test

Không cần SSD vật lý trong CI.

- Unit test process specification.
- Integration test mock/fake process runner.
- Trên máy QA có SSD thử nghiệm, chạy manual:
  - xác nhận operation exit code;
  - xác nhận UI disclaimer;
  - không dùng recovery claim.
- Không benchmark độ xóa NAND.

## 5. UI tests

Tối thiểu:

- Empty state.
- Volume cards.
- Unknown disabled.
- Confirmation phrase.
- Elevation denied.
- Running.
- Cancel.
- Failure.
- Completed HDD wording.
- Completed SSD wording.
- High DPI.
- Keyboard navigation.
- Vietnamese resource strings không bị cắt.

Có thể dùng UI automation sau MVP; trước mắt ViewModel tests phải bao phủ.

## 6. Static analysis và CI

Mỗi PR:

```powershell
dotnet restore --locked-mode
dotnet build -c Release
dotnet test -c Release --no-build
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
```

CI mặc định không chạy VHD integration tests có ghi đè.

Có workflow riêng, manual approval, self-hosted Windows test machine hoặc isolated ephemeral VM cho VHD tests.

## 7. Definition of Done

Một milestone chỉ hoàn thành khi:

- code build Release;
- tests tương ứng xanh;
- không warning mới;
- docs cập nhật;
- self-review security hoàn tất;
- `PROGRESS.md` ghi bằng chứng command đã chạy;
- không có TODO ảnh hưởng an toàn trong đường chạy production.


---

# KẾ HOẠCH CODEX

# Kế hoạch để Codex triển khai và tự kiểm thử

## Nguyên tắc

Không yêu cầu Codex tạo toàn bộ ứng dụng trong một thay đổi khổng lồ. Mỗi milestone phải build được, test được và review được.

## Milestone 0 — Khởi tạo repository

Tạo:

- solution và project tree;
- `global.json`;
- central package management;
- analyzers;
- `.editorconfig`;
- `.gitignore`;
- `README.md`;
- `PROGRESS.md`;
- CI unit-test workflow.

Tiêu chí:

- `dotnet build -c Release` xanh.
- test placeholder xanh.
- không có thao tác disk nào.

## Milestone 1 — Core domain và SafetyPolicy

Triển khai:

- enums/records;
- volume eligibility;
- confirmation phrase;
- state machine;
- error taxonomy;
- redaction interfaces.

Test đầy đủ bảng policy.

Tiêu chí:

- Core không phụ thuộc Windows/WPF.
- 90%+ coverage cho SafetyPolicy và command validation.

## Milestone 2 — Windows inventory read-only

Triển khai:

- CIM/WMI storage discovery;
- volume/partition/disk mapping;
- media type/bus type;
- system/boot/read-only;
- BitLocker state nếu API có;
- fallback và conflict handling;
- diagnostic snapshot đã redacted.

Tiêu chí:

- chỉ đọc;
- không gọi lệnh xóa;
- conflict → Unknown;
- manual diagnostic screen hiển thị đúng trên ít nhất HDD, SSD/NVMe và virtual disk nếu có.

## Milestone 3 — Worker protocol

Triển khai:

- contracts;
- named pipe;
- SID ACL;
- nonce;
- protocol version;
- elevation launch;
- one-shot worker;
- heartbeat;
- cancellation protocol.

Dùng fake operation executor.

Tiêu chí:

- chưa gọi Cipher/ReTrim thật.
- tests cho authentication, malformed message và disconnect.

## Milestone 4 — HDD executor

Triển khai:

- validate target trong worker;
- rescan Volume GUID;
- direct `cipher.exe` execution;
- stdout/stderr capture;
- exit code;
- cancellation;
- result redaction.

Tiêu chí:

- unit tests process spec.
- VHD integration test sentinel integrity.
- VHD deleted-pattern test.
- không chạy trên ổ thật.

## Milestone 5 — SSD/NVMe ReTrim executor

Triển khai:

- exact policy;
- PowerShell ReTrim execution;
- exit code;
- wording/disclaimer;
- tests với fake runner.

Tiêu chí:

- không có Secure Erase.
- UI/result không overclaim.

## Milestone 6 — WPF UI

Triển khai:

- dashboard;
- volume details;
- confirmation;
- operation progress;
- cancel;
- result;
- history;
- settings/about;
- Vietnamese resources.

Tiêu chí:

- keyboard usable.
- Unknown disabled.
- phrase confirmation bắt buộc.
- scan lại trước execute.
- system volume advanced warning.

## Milestone 7 — History, crash recovery và privacy

Triển khai:

- JSONL journal;
- redaction;
- retention;
- abandoned detection;
- export diagnostics đã lọc.

Tiêu chí:

- không có filename/path/serial đầy đủ.
- malformed history không crash.

## Milestone 8 — Hardening

- threat-model review.
- dependency vulnerability scan.
- fuzz parser worker messages.
- race tests.
- cancellation stress tests.
- multiple launch lock.
- code signing placeholders.
- privacy review.
- localized-output review.

## Milestone 9 — Packaging

- self-contained x64 publish.
- installer hoặc portable package.
- worker/UI manifest đúng.
- build metadata.
- checksum.
- release checklist.

## Milestone 10 — Auto-update tùy chọn

Chỉ làm khi đã có code signing.

- signed manifest;
- signature/hash verification;
- user approval;
- rollback;
- no script execution;
- disabled by default trong dev.

## Cơ chế tự sửa lỗi của Codex

Sau mỗi milestone, Codex phải:

1. Chạy build/test/format.
2. Nếu lỗi, phân loại:
   - compile;
   - unit test;
   - integration;
   - permissions;
   - environment;
   - flaky.
3. Sửa nguyên nhân gốc, không skip test trừ khi test vốn là opt-in VHD.
4. Chạy lại test hẹp.
5. Chạy lại toàn bộ suite.
6. Review diff.
7. Cập nhật `PROGRESS.md`.
8. Tiếp tục milestone kế tiếp.

Không được “fix” bằng cách:

- giảm validation;
- đổi Unknown thành HDD/SSD;
- bỏ confirmation;
- bỏ test;
- nuốt exception;
- luôn trả exit code thành công;
- ghi lệnh shell dạng string;
- chạy integration test trên ổ thật.

## Mẫu PROGRESS.md

```markdown
# Progress

## Milestone N — Tên

Status: Complete | In progress | Blocked

### Implemented
- ...

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test ...`: PASS, N tests
- `dotnet format --verify-no-changes`: PASS

### Security review
- ...

### Known limitations
- ...

### Next
- ...
```


---

# MASTER PROMPT

```text
Bạn đang triển khai ứng dụng Windows `SafeFreeSpace`.

Hãy đọc toàn bộ các file sau trước khi sửa mã:

1. `AGENTS.md`
2. `README_START_HERE.md`
3. `docs/PRODUCT_SPEC.md`
4. `docs/SECURITY_MODEL.md`
5. `docs/ARCHITECTURE.md`
6. `docs/TEST_PLAN.md`
7. `docs/CODEX_EXECUTION_PLAN.md`

Mục tiêu là tạo ứng dụng C#/.NET 10 WPF giúp:

- HDD/NTFS: ghi đè vùng trống bằng `cipher.exe /w:<drive>:\`.
- SSD/NVMe: gửi lại TRIM bằng `Optimize-Volume -ReTrim`.
- Giữ nguyên dữ liệu đang tồn tại.
- Không format, không xóa phân vùng, không Secure Erase toàn ổ.
- Không tuyên bố không thể khôi phục 100%.

Thực hiện theo đúng milestone trong `docs/CODEX_EXECUTION_PLAN.md`.

Quy trình bắt buộc:

- Bắt đầu từ Milestone 0.
- Mỗi milestone phải có test.
- Sau mỗi milestone chạy restore, Release build, tests và format verification.
- Tự review các rủi ro mất dữ liệu, command injection, privilege escalation, race condition, cancellation và log privacy.
- Sửa đến khi các test tương ứng xanh.
- Cập nhật `PROGRESS.md` với bằng chứng đã chạy.
- Sau khi một milestone hoàn thành, tiếp tục milestone kế tiếp.
- Không chạy thao tác wipe trên ổ thật.
- Integration test chỉ chạy trên VHD được tạo trong test và chỉ khi guard trong tài liệu được thỏa mãn.
- Khi thiếu dữ liệu hoặc Windows trả kết quả mâu thuẫn, fail closed và hiển thị Unknown.
- Không thêm lệnh destructive ngoài allow-list trong tài liệu.

Trước tiên hãy:
1. Tóm tắt các ràng buộc an toàn bạn sẽ tuân thủ.
2. Khởi tạo Milestone 0.
3. Chạy build/test.
4. Báo cáo files changed, kết quả và rủi ro còn lại.
5. Tiếp tục triển khai có kiểm soát theo tài liệu.
```


---

# NGUỒN CHÍNH THỨC

# Nguồn kỹ thuật chính thức đã dùng

Kiểm tra ngày: 2026-07-17

## Microsoft

1. Cipher.exe — overwrite deleted data  
   https://learn.microsoft.com/en-us/troubleshoot/windows-server/certificates-and-public-key-infrastructure-pki/use-cipher-to-overwrite-deleted-data

2. Cipher command reference  
   https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/cipher

3. Optimize-Volume / ReTrim  
   https://learn.microsoft.com/en-us/powershell/module/storage/optimize-volume

4. Get-PhysicalDisk  
   https://learn.microsoft.com/en-us/powershell/module/storage/get-physicaldisk

5. MSFT_PhysicalDisk class  
   https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-physicaldisk

6. .NET 10 LTS downloads/support  
   https://dotnet.microsoft.com/en-us/download/dotnet/10.0  
   https://dotnet.microsoft.com/en-us/download/dotnet

## NIST

7. Guidelines for Media Sanitization, SP 800-88  
   https://csrc.nist.gov/pubs/sp/800/88/r2/final

Nguyên tắc được rút ra: ghi đè vùng trống phù hợp hơn với HDD; overwrite trên flash không cho phần mềm kiểm soát chắc chắn mọi vùng vật lý do wear-leveling và vùng dự phòng. Vì vậy ứng dụng chỉ mô tả SSD action là ReTrim, không phải chứng nhận sanitize.

## OpenAI Codex

8. AGENTS.md  
   https://developers.openai.com/codex/agent-configuration/agents-md

9. Codex best practices  
   https://developers.openai.com/codex/learn/best-practices

10. Codex CLI  
    https://developers.openai.com/codex/cli

