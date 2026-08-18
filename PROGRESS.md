# Progress

## Milestone 0 — Khởi tạo repository

Status: Complete

### Implemented
- Tạo solution và project tree theo kiến trúc đặc tả.
- Cấu hình `global.json` (SDK 10.0.300), `Directory.Build.props`, `Directory.Packages.props`.
- `.editorconfig`, `.gitignore`, `README.md`, `PROGRESS.md`.
- App manifest: UI `asInvoker`, Worker `requireAdministrator`.
- Placeholder project references và unit tests.

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test -c Release --no-build`: PASS (2 tests)
- `dotnet format --verify-no-changes`: PASS

### Security review
- Chưa có logic disk. Chỉ tạo cấu trúc project.
- Worker manifest đã yêu cầu admin; UI manifest chạy không đặc quyền.

### Known limitations
- Chỉ là placeholder; domain logic sẽ triển khai ở Milestone 1.

### Next
- Triển khai Core domain: enums, records, SafetyPolicy, confirmation phrase, command builder, tests.

---

## Milestone 1 — Core domain và SafetyPolicy

Status: Complete

### Implemented
- Domain models: `DriveMediaType`, `DriveBusType`, `BitLockerState`, `OperationErrorCategory`, `VolumeIdentity`, `VolumeSnapshot`, `EligibilityDecision`, `OperationPlan`.
- `SafetyPolicy` với fail-closed cho Unknown/network/optical/read-only/dirty/locked/RAID/Storage Spaces/Virtual.
- `ConfirmationPhraseService` tạo và kiểm tra phrase `WIPE FREE SPACE X:` / `RETRIM X:`.
- `CommandBuilder` tạo lệnh `cipher.exe` và `powershell.exe` an toàn, chỉ chấp nhận A-Z.
- Interfaces: `IClock`, `IHashService`, `ILogRedactor`, `IVolumeInventory`, `IPrivilegedOperationClient`, `IOperationHistory`.

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test -c Release --no-build`: PASS (39 tests)
- `dotnet format --verify-no-changes`: PASS

### Security review
- Không có lệnh destructive trong Core.
- Drive letter validate bằng regex `^[A-Z]$`.
- Không nối chuỗi command line trong Core.

### Known limitations
- Chưa có inventory thực tế; chỉ là logic domain.

### Next
- Triển khai `WindowsVolumeInventory` dùng CIM để đọc volume/physical disk.

---

## Milestone 2 — Windows inventory read-only

Status: Complete

### Implemented
- `WindowsVolumeInventory` dùng CIM namespace `root/Microsoft/Windows/Storage`.
- Fallback `Win32_LogicalDisk` khi không có quyền hoặc namespace không khả dụng.
- `VolumeMapper` chuyển đổi MediaType, BusType, BitLockerState.
- Phát hiện system/boot/read-only/dirty từ `MSFT_Volume`.
- Redact model name (`Sams...00GB`).
- `WindowsVolumeInventory` implement `IVolumeInventory`.

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test -c Release --no-build`: PASS (61 tests)
- `dotnet format --verify-no-changes`: PASS

### Security review
- Chỉ đọc, không gọi lệnh xóa.
- Fail-closed: conflict → Unknown.

### Known limitations
- BitLocker detection yêu cầu quyền admin; khi thiếu trả `Unknown`.
- Mapping volume → physical disk dựa trên partition access paths; có thể thiếu trong một số cấu hình.

### Next
- Triển khai worker protocol: named pipe, nonce, elevation, one-shot worker.

---

## Milestone 3 — Worker protocol

Status: Complete

### Implemented
- `NamedPipeProtocol` đóng khung message JSON với length prefix, giới hạn kích thước.
- `WorkerPipeServer` (UI) và `WorkerPipeClient` (worker) với handshake protocol version + nonce + operation id.
- `PipeSecurityHelper` giới hạn pipe ACL cho SID ngưởi dùng hiện tại và LocalSystem.
- `ElevatedWorkerLauncher` khởi chạy worker bằng `Verb=runas`, truyền pipe name/nonce/operation id qua base64.
- `WorkerHost` một lần (one-shot), kiểm tra allow-list operation, xác thực lại nonce.
- `FakeOperationExecutor` và `CancelOperationExecutor` cho milestone 3.

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test -c Release --no-build`: PASS (64 tests)
- `dotnet format --verify-no-changes`: PASS

### Security review
- Worker chỉ chấp nhận operation từ allow-list.
- Nonce một lần, protocol version cố định.
- Không truyền command line tùy ý cho worker; chỉ base64 của pipe name/nonce/op id.

### Known limitations
- Chưa có executor thật cho HDD/SSD; chỉ fake.
- Chưa test elevation thật trong CI.

### Next
- Triển khai HDD executor (`cipher.exe`) và VHD integration tests.

---

## Milestone 4 — HDD executor

Status: Complete

### Implemented
- `IProcessRunner` + `ProcessRunner` với stdout/stderr redirect, cancellation, kill-tree.
- `CipherExecutor` chạy `cipher.exe /w:X:\`, rescan Volume GUID, so sánh trước khi chạy.
- Output được sanitize trước khi report.
- `VhdTestHelper` tạo fixed VHD qua diskpart, có guard admin + env var.
- Integration tests sentinel integrity và deleted-pattern (opt-in, skipped mặc định).

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test -c Release --no-build`: PASS (69 unit, 1 integration, 2 skipped)
- `dotnet format --verify-no-changes`: PASS

### Security review
- Worker rescan và so sánh VolumeGuid trước khi chạy cipher.
- Drive letter validate A-Z, argument qua `ArgumentList`.
- VHD tests chỉ chạy khi admin + env var + label `SFS_TEST_`.

### Known limitations
- Deleted-pattern scan raw VHD chưa hoàn thiện (placeholder).
- VHD tests chưa chạy trong CI thông thường.

### Next
- Triển khai SSD/NVMe ReTrim executor.

---

## Milestone 5 — SSD/NVMe ReTrim executor

Status: Complete

### Implemented
- `RetrimExecutor` trong `SafeFreeSpace.ElevatedWorker` thực hiện `Optimize-Volume -ReTrim` qua PowerShell.
- Rescan volume GUID và file system trước khi chạy; từ chối nếu volume đã thay đổi.
- Chỉ cho phép khi media type là SSD/SCM hoặc bus type là NVMe.
- Kiểm tra read-only, xử lý cancellation, tool not found, process exit code.
- Output sanitize thay thế user profile path và chuyển `\` → `/`.
- Unit tests: HDD bị từ chối, NVMe cho phép, chạy thành công gọi đúng powershell.exe.

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test -c Release --no-build`: PASS (72 unit, 1 integration, 2 VHD skipped)
- `dotnet format --verify-no-changes`: PASS

### Security review
- Không gọi lệnh xóa; chỉ gửi yêu cầu TRIM hệ thống.
- Drive letter validate A-Z, command qua `CommandBuilder` không nối chuỗi.
- Rescan và so sánh VolumeGuid trước khi thực thi.

### Known limitations
- ReTrim không đảm bảo xóa vật lý; UI phải hiển thị disclaimer rõ ràng.
- Chưa test trên volume SSD/NVMe thật; chỉ có unit tests.

### Next
- Triển khai WPF UI: dashboard, volume cards, confirmation phrase, progress, result/history/settings.

---

## Milestone 6 — WPF UI

Status: Complete

### Implemented
- MVVM tối giản: `ObservableObject`, `RelayCommand`, `IUiDispatcher`/`WpfDispatcher`.
- `VolumeCardViewModel`, `ConfirmationViewModel`, `OperationViewModel`, `HistoryEntryViewModel`, `MainViewModel`.
- Dashboard hiển thị volume cards với eligibility, action text, status.
- Confirmation panel với disclaimer đúng loại ổ, expected phrase, countdown 3 giây trước khi Start.
- Progress panel với elapsed timer, output log (giới hạn 200 dòng), Cancel.
- Result panel với success/failure/interrupted message.
- History panel đọc từ `IOperationHistory`.
- Settings/about panel với toggle Advanced mode.
- Vietnamese resource dictionary (`Strings.vi.xaml`) và dev-build warning.
- `NamedPipePrivilegedOperationClient` kết nối UI với worker qua named pipe + elevation.
- `InMemoryOperationHistory` placeholder cho UI; sẽ thay bằng JSONL trong Milestone 7.
- Unit tests `MainViewModelTests`: refresh, select volume, wrong phrase, complete, volume GUID changed.

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test -c Release --no-build`: PASS (77 unit, 1 integration, 2 VHD skipped)
- `dotnet format --verify-no-changes`: PASS

### Security review
- UI không chạy destructive command trực tiếp; mọi operation qua worker đặc quyền.
- Rescan volume GUID ngay trước start; hủy nếu GUID thay đổi.
- Confirmation phrase bắt buộc, chứa drive letter và loại thao tác.
- Unknown volume bị khóa (eligibility = false).
- Disclaimer rõ ràng phạm vi và giới hạn, không overclaim.

### Known limitations
- Chưa implement JSONL history thật; đang dùng in-memory.
- Chưa test UI tương tác trực tiếp (chỉ test ViewModel).
- Chưa xử lý power/battery warning trong UI.

### Next
- Triển khai JSONL operation history, redaction, retention 30 ngày, crash recovery, export diagnostics.

---

## Milestone 7 — History, crash recovery và privacy

Status: Complete

### Implemented
- `LocalHashService` (SHA-256 + salt) để hash VolumeGuid.
- `LogRedactor` loại bỏ path, UNC, GUID, user profile khỏi output.
- `JsonlOperationHistory` lưu JSON Lines dưới `%LocalAppData%\SafeFreeSpace\Logs`:
  - file theo tháng `operations-yyyyMM.jsonl`;
  - append-only, khóa per-file;
  - VolumeGuid được hash với salt cục bộ trước khi ghi;
  - output được redact trước khi ghi.
- Crash recovery: `MarkAbandonedAsync` đánh dấu các operation chưa kết thúc là `Abandoned` khi app khởi động.
- Retention 30 ngày: `ApplyRetentionAsync` xóa file tháng cũ.
- `ClearHistoryAsync` cho phép ngưởi dùng xóa lịch sử.
- `ExportDiagnosticsAsync` tạo file JSON đã lọc (không chứa VolumeGuid/output).
- `IOperationHistory` mở rộng để App gọi retention/clear.
- App startup gọi `MarkAbandonedAsync` và `ApplyRetentionAsync(TimeSpan.FromDays(30))`.
- Unit tests `JsonlOperationHistoryTests`: round-trip, abandoned, malformed skip, retention, clear.

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test -c Release --no-build`: PASS (82 unit, 1 integration, 2 VHD skipped)
- `dotnet format --verify-no-changes`: PASS

### Security review
- Không lưu VolumeGuid thô; chỉ lưu hash với salt cục bộ.
- Output redact path, UNC, GUID, user profile trước khi persist.
- Malformed JSONL được bỏ qua, không crash.
- File journal nằm trong LocalAppData của user.

### Known limitations
- Salt lưu cùng thư mục log; bảo vệ dựa trên ACL LocalAppData.
- Chưa mã hóa file log.
- Export diagnostics chưa có UI button (chỉ có API).

### Next
- Triển khai packaging placeholders: publish self-contained x64, manifests, checksum.

---

## Milestone 8 — Packaging placeholders

Status: Complete

### Implemented
- `Directory.Build.props` cấu hình version prefix/suffix, informational version, source revision id.
- `publish.ps1`: publish self-contained x64 single-file cho App và Worker, verify manifests, tạo `SHA256SUMS.txt`, tùy chọn portable ZIP.
- `RELEASE_CHECKLIST.md` với các bước build, verify, signing, distribution.
- Unit tests `ManifestTests`: xác nhận App manifest `asInvoker`, Worker manifest `requireAdministrator`.

### Verification
- `dotnet build -c Release`: PASS
- `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
- `dotnet format --verify-no-changes`: PASS

### Security review
- UI chạy `asInvoker`, worker chạy `requireAdministrator`.
- Self-contained publish giảm dependency runtime.
- Checksum giúp xác minh package.

### Known limitations
- Chưa có installer MSI/MSIX.
- Chưa code-sign.
- Chưa publish thử trên CI sạch.

### Next
- Final review: diff, security, performance, documentation, overall build/test/format.

---

## Final Review

Status: Complete

### Summary
- MVP SafeFreeSpace v1.0.0-dev đã hoàn thành qua 8 milestone.
- 84 unit tests pass, 1 integration test pass, 2 VHD integration tests skip đúng theo guard.
- Build Release sạch (0 warning, 0 error).
- Format verification pass.

### Files changed (high-level)
- Core: domain models, SafetyPolicy, ConfirmationPhraseService, CommandBuilder, interfaces.
- Infrastructure.Windows: WindowsVolumeInventory, named pipe protocol/server/client/launcher, LocalHashService, LogRedactor, JsonlOperationHistory.
- ElevatedWorker: WorkerHost, CipherExecutor, RetrimExecutor, ProcessRunner.
- App: WPF MVVM dashboard, volume cards, confirmation/progress/result/history/settings, Vietnamese resources.
- Tests: unit tests cho Core, Infrastructure, ElevatedWorker, App ViewModels, JSONL history, packaging manifests; integration VHD tests opt-in.
- Packaging: publish.ps1, RELEASE_CHECKLIST.md, Directory.Build.props version metadata.

### Security review
- UI `asInvoker`, worker `requireAdministrator`.
- Worker allow-list typed operations; không nhận command string tùy ý.
- Named pipe với random name, ACL restricted, nonce, protocol version.
- Drive letter validate `^[A-Z]$`; process argument qua `ArgumentList`.
- Volume GUID rescan trước execute; hủy nếu thay đổi.
- Unknown/network/optical/read-only/dirty/locked/RAID/StorageSpaces/Virtual bị khóa.
- Không format, không xóa phân vùng, không Secure Erase toàn ổ.
- History lưu VolumeGuid hash + salt cục bộ; output redact path/UNC/GUID/user profile.
- Malformed JSONL ignored.

### Remaining risks
- Chưa chạy thử nghiệm thực tế trên ổ HDD/SSD thật (ngoại trừ unit tests).
- VHD integration tests chưa chạy trong CI thông thường.
- Cancellation chưa gửi explicit CancelOperation message đến worker (dựa trên pipe close + process kill).
- Chưa code-sign; bản dev hiển thị cảnh báo.
- Chưa có installer chính thức.

### Verification
- `dotnet restore`: PASS
- `dotnet build -c Release`: PASS (0 warning, 0 error)
- `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
- `dotnet format --verify-no-changes`: PASS
- `dotnet run -c Release --project src/SafeFreeSpace.App`: PASS (app khởi động thành công sau khi thêm `BooleanToVisibilityConverter`)

---

## Post-MVP Polish — UI overlap và WMI fallback

Status: Complete

### Problem
- Screenshot `D:/LapTrinhAI/ScreenShot/win-x64/Screenshots/screenshot_2026-07-18_074228.jpg` hiển thị:
  - Dashboard báo lỗi đỏ: **"Không thể đọc danh sách volume: Not found"**.
  - Màn hình Settings/About bị chồng text: "Chọn volume để thao tác" đè lên nội dung About.

### Root cause
- `MainWindow.xaml` đặt nhiều panel trong cùng một `Grid` cell; panel cũ không bị ẩn sạch khi chuyển view state.
- `WindowsVolumeInventory.TryQueryStorageVolumes` chỉ bắt `ManagementException`/`UnauthorizedAccessException`; khi CIM storage namespace báo lỗi khác (ví dụ "Not found") nó throw ra ngoài và không rơi vào fallback `Win32_LogicalDisk`.

### Fix
- `src/SafeFreeSpace.App/MainWindow.xaml`: thay thế bằng `ContentControl` + `MainViewTemplateSelector`, mỗi view state là một `DataTemplate` riêng có `Background="WindowBrush"`, loại bỏ chồng panel.
- `src/SafeFreeSpace.App/Selectors/MainViewTemplateSelector.cs`: selector mới chọn template theo `MainViewState`.
- `src/SafeFreeSpace.Infrastructure.Windows/Storage/WindowsVolumeInventory.cs`: thêm `catch (Exception)` trong `TryQueryStorageVolumes`, `TryQueryPhysicalDisks`, `TryQueryPartitions`, `TryQueryBitLockerStates`, `FallbackLogicalDisks` để đảm bảo fallback khi WMI/CIM lỗi bất kỳ.

### Verification
- `dotnet build -c Release`: PASS (0 warning, 0 error)
- `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
- `dotnet format --verify-no-changes`: PASS
- `dotnet run -c Release --project src/SafeFreeSpace.App/SafeFreeSpace.App.csproj --no-build`: app khởi động, chờ user chụp ảnh màn hình xác nhận UI.

### Follow-up: screenshot 2026-07-18_074925 vẫn lỗi "Not found"
- Root cause thực tế: `GetVolumesAsync` không bắt được exception phát sinh khi enumerator của `ManagementObjectCollection` di chuyển (dù `searcher.Get()` không throw).
- Fix bổ sung: bọc toàn bộ body `GetVolumesAsync` trong `try/catch`; nếu bất kỳ lỗi nào xảy ra thì xóa kết quả đã thu thập và buộc gọi `FallbackLogicalDisks`; fallback luôn trả về empty list thay vì throw.
- Verification (round 2): build/test/format PASS; app đang chạy, chờ ảnh màn hình mới.

### Follow-up: screenshot 2026-07-18_075950 trống, 2026-07-18_080209 vẫn trống
- Root cause thực tế: `App.xaml` khai báo `StartupUri="MainWindow.xaml"`. WPF tự tạo và hiển thị `MainWindow` **trước khi** `App.OnStartup` chạy, nên cửa sổ đầu tiên không có `DataContext`. `OnStartup` sau đó tạo một `MainWindow` khác và set `DataContext`, nhưng cửa sổ hiển thị là cửa sổ đầu tiên.
- Dấu hiệu: `DataContext.GetType().Name` trong toolbar trả về empty; tất cả binding `Visibility`/`ItemsSource` trong các panel đều lỗi và mặc định `Visible`, dẫn đến chồng panel hoặc trống.
- Fix:
  - Bỏ `StartupUri` khỏi `App.xaml`; `OnStartup` tự tạo `MainWindow`, set `DataContext`, rồi `Show()`.
  - Thay `ContentControl` + `DataTemplateSelector` bằng `Grid` với nhiều panel, mỗi panel `Visibility` binding đến `IsDashboardVisible`/`IsConfirmationVisible`/... qua `BoolToVisibilityConverter` custom.
  - Thêm `Converters/BoolToVisibilityConverter.cs`.
  - `MainViewModel.RefreshAsync` giữ nguyên (không còn debug message).
- Verification (round 3):
  - `dotnet build -c Release`: PASS
  - `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
  - `dotnet format --verify-no-changes`: PASS
  - Tự chụp màn hình bằng PowerShell `PrintWindow` xác nhận:
    - Dashboard hiển thị volume C: (Windows), D: (Data), G: (Google Drive) với đầy đủ thông tin và nút `Không khả dụng` (đúng SafetyPolicy fail-closed cho Unknown media/bus).
    - Màn hình **Lịch sử thao tác** và **Cài đặt & Giới thiệu** không còn chồng text.

### Follow-up: user báo ổ C: và D: là NVMe nhưng hiển thị Unknown / không khả dụng
- Root cause: `MSFT_PhysicalDisk`/`MSFT_Partition` không enumerate được trên máy user (throw `ManagementException: Not found` khi duyệt enumerator). `WindowsVolumeInventory` fallback sang `Win32_LogicalDisk` nhưng không có VolumeGuid, MediaType, BusType.
- Fix:
  - `FallbackLogicalDisks` chuyển sang dùng `Win32_Volume` để lấy `DriveLetter`, `DeviceID` (extract Volume GUID), `Label`, `FileSystem`, `Capacity`, `FreeSpace`, `SystemVolume`, `BootVolume`.
  - Thêm `TryQueryWin32DiskDrives` (query `Win32_DiskDrive`) và `TryQueryWin32LogicalDiskToPartition` (query `Win32_LogicalDiskToPartition`) để map logical disk → physical disk index.
  - Thêm `MapWin32MediaType`/`MapWin32BusType` dùng model name + interface type để suy ra SSD/HDD/NVMe/USB/SATA/SAS.
  - `ToFallbackVolumeIdentity` nhận `Win32_Volume` + `DriveType` + `CimPhysicalDiskInfo` để tạo `VolumeIdentity` đầy đủ.
- Verification (round 4):
  - `dotnet build -c Release`: PASS
  - `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
  - `dotnet format --verify-no-changes`: PASS
  - Tự chụp màn hình xác nhận:
    - Ổ D: (Data) hiển thị **Ssd**, **Sas**, **Sams... 2TB**, nút **"Gửi lại TRIM"** khả dụng.
    - Ổ C: (Windows) hiển thị **Ssd**, **Sas**, **Sams... 2TB**, nút **"Không khả dụng"** vì là volume hệ thống (cần chế độ nâng cao).
    - Ổ G: (Google Drive) vẫn Unknown (Google Drive là ổ ảo, đúng fail-closed).

### Follow-up: user yêu cầu không liệt kê Google Drive
- Fix: `GetVolumesAsync` (path `MSFT_Volume`) và `FallbackLogicalDisks` (path `Win32_Volume`) đều bỏ qua volume không có physical disk backing (`diskInfo == null`) trừ khi là system/boot volume. Điều này loại bỏ Google Drive và các ổ cloud/virtual khác không gắn với physical disk.
- Verification (round 5):
  - `dotnet build -c Release`: PASS
  - `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
  - `dotnet format --verify-no-changes`: PASS
  - Tự chụp màn hình xác nhận: chỉ còn **C: (Windows)** và **D: (Data)** trong danh sách; Google Drive (G:) không còn hiển thị.

### Follow-up: thêm tab Hướng dẫn
- Thêm `MainViewState.Help`, `ShowHelpCommand`, `IsHelpVisible` vào `MainViewModel`.
- Thêm nút **"Hướng dẫn"** vào toolbar và panel Help vào `MainWindow.xaml`.
- Thêm resource `HelpButton`, `HelpTitle`, `HelpText` vào `Strings.vi.xaml` với nội dung hướng dẫn: chọn volume, loại thao tác HDD/SSD, xác nhận phrase, lưu ý an toàn, lịch sử.
- Verification (round 6):
  - `dotnet build -c Release`: PASS
  - `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
  - `dotnet format --verify-no-changes`: PASS
  - Tự chụp màn hình xác nhận: nút **"Hướng dẫn"** xuất hiện trên toolbar; click mở màn hình **"Hướng dẫn sử dụng"** với nội dung đầy đủ, không chồng text.

### Follow-up: cải thiện hiển thị Hướng dẫn chuyên nghiệp hơn
- Thay `TextBlock` bằng `FlowDocumentScrollViewer` + `FlowDocument` trong panel Help.
- Nội dung được format với `Paragraph` heading đậm, `List` bullet points, `Bold`/`Italic` cho từ khóa và tên lệnh.
- Verification (round 7):
  - `dotnet build -c Release`: PASS
  - `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
  - `dotnet format --verify-no-changes`: PASS
  - Tự chụp màn hình xác nhận: màn hình **"Hướng dẫn sử dụng"** hiển thị rõ ràng, có cấu trúc, dễ đọc.

---

## Feature: Ước tính thởi gian

Status: Complete

### Implemented
- `VolumeCardViewModel.EstimatedDurationText`: tính thởi gian ước tính dựa trên `FreeBytes` và loại thao tác (HDD ~100 MB/s, SSD/NVMe ~500 MB/s).
- `ConfirmationViewModel.EstimatedDurationText`: hiển thị lại ước tính trước khi bắt đầu.
- `VolumeCard.xaml`: thêm dòng "Ước tính: ~X phút/giờ" dưới status text.
- `MainWindow.xaml` Confirmation panel: hiển thị ước tính bên cạnh volume summary.

### Verification
- `dotnet build -c Release`: PASS (0 warning, 0 error)
- `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
- `dotnet format --verify-no-changes`: PASS
- Tự chụp màn hình xác nhận: ổ D: hiển thị **"Ước tính: ~42.2 phút"** cho thao tác ReTrim.

---

## Feature: Báo cáo chi tiết, Progress bar, Toast notification, Cancel fix

Status: Complete

### Implemented
- **Báo cáo chi tiết sau thao tác**:
  - `MainViewModel` lưu `_currentSnapshot` và `_operationStartTime`.
  - `BuildCompletionReport()` hiển thị thởi gian thực tế, dung lượng vùng trống, tốc độ trung bình trong màn hình Result.
- **Progress bar**:
  - `OperationViewModel.IsProgressIndeterminate` binding đến `IsRunning`.
  - `MainWindow.xaml` Progress panel thêm `ProgressBar` indeterminate.
- **Toast notification**:
  - `SafeFreeSpace.App.csproj` thêm `UseWindowsForms=true`.
  - `MainViewModel.ShowCompletionToast` dùng `System.Windows.Forms.NotifyIcon` hiển thị balloon tip khi thao tác kết thúc.
- **Cancel operation fix**:
  - `IPrivilegedOperationClient` thêm `CancelOperationAsync`.
  - `NamedPipePrivilegedOperationClient.CancelOperationAsync` gửi `WorkerOperationType.CancelOperation` qua named pipe trước khi kill process.
  - `MainViewModel.OnCancelOperation` gọi `CancelOperationAsync` trước khi cancel local CTS.

### Verification
- `dotnet build -c Release`: PASS (0 warning, 0 error)
- `dotnet test -c Release --no-build`: PASS (84 unit, 1 integration, 2 VHD skipped)
- `dotnet format --verify-no-changes`: PASS
- Tự chụp màn hình xác nhận: Dashboard hiển thị ước tính; Confirmation panel hiển thị đúng. Progress bar, toast, cancel message cần test thực tế khi chạy operation (yêu cầu elevation).

---

## Post-Review Fix Round — Sửa lỗi từ đợt code review toàn solution

Status: Complete

### Fixed (Critical/High)
- **App — sai thread toàn bộ flow thao tác**: `StartSelectedOperationAsync` bỏ `ConfigureAwait(false)` ở `RefreshVolumeAsync` (continuation về UI thread, hết cross-thread exception trên `ObservableCollection`); `OnStartOperation` thành async void wrapper có try/catch hiện lỗi ra Result panel thay vì nuốt exception.
- **App — `IsBusy` khi operation chạy**: set `true` khi bắt đầu, `false` trong finally; Refresh/Back/Settings/Help khóa khi busy, Cancel vẫn bấm được (CanExecute theo `Operation.CanCancel`).
- **Core — SafetyPolicy fail-open**: block cả `BitLockerState.Unknown` (trước chỉ block `Locked`); `FileSystem` null dùng `string.Equals` static (null → block thay vì NRE); block khi `HealthStatus` có giá trị và khác `Healthy` (null/empty cho qua vì fallback WMI không thu thập được).
- **Infrastructure — `PipeSecurityHelper`**: bỏ fallback `WorldSid` (Everyone); throw `InvalidOperationException` khi không lấy được SID user — fail-closed.
- **Worker — progress fire-and-forget**: `WorkerHost` chuyển sang `Channel<OperationProgress>` với một writer task duy nhất được await trước mọi `SendResponseAsync` — response không bao giờ vượt progress.
- **Worker — dead-man switch**: pipe gãy (send throw) → `cts.Cancel()`, cipher/powershell không chạy mất kiểm soát khi app UI chết; `SendResponseAsync` lỗi trả exit 1 thay vì crash unhandled.
- **Integration — test tautology**: `DeletedPattern_IsOverwritten` implement scan raw VHD thật (đọc chunk 8 MB, giữ đuôi chunk bắt pattern ngang ranh giới); đổi thứ tự detach → scan → delete.

### Fixed (Medium/Low)
- `WindowsVolumeInventory`: thêm `catch (OperationCanceledException) { throw; }` trước catch-all; fallback đọc `DriveType` trực tiếp từ `Win32_Volume` (bỏ `TryQueryWin32LogicalDisks` → hết leak COM handle từ `ManagementObject.Clone()`); comment giới hạn isReadOnly/isDirty.
- `VolumeMapper.MapBitLockerState`: map đúng ngữ nghĩa WMI `ProtectionStatus` (0/1 → Unlocked, 2 → Unknown; không suy ra Locked từ ProtectionStatus).
- `VolumeMapper.MapHealthStatus` mới: map `MSFT_PhysicalDisk.HealthStatus` uint16 (0 → `Healthy`, khác → `Unhealthy`, null → null) — khớp với check mới trong SafetyPolicy.
- `IPrivilegedOperationClient.ConnectAsync` nhận `operationId` từ caller — một nguồn duy nhất cho handshake/launch args/request; nonce dùng `RandomNumberGenerator.GetBytes(16)` thay `Guid.NewGuid()`.
- `JsonlOperationHistory`: `ApplyRetentionAsync`/`ClearHistoryAsync` acquire per-file semaphore trước khi xóa file (helper `DeleteLogFileAsync`), hết race với `AppendAsync`.
- `LogRedactor`: sửa regex NT device path `\\?\C:\` (thứ tự redact và escape backslash), thêm unit tests.
- Worker: `WorkerHost` bọc `FromBase64` trong try (arg xấu → exit code sạch); `ProcessRunner.Kill` catch rộng trong nhánh cancel; `RetrimExecutor` thêm check `IsDirty` (nhất quán Cipher); exit code != 0 kèm tối đa 3 dòng stderr cuối đã sanitize; gom `ValidateDriveLetter`/`Fail`/`SanitizeOutput` vào `ExecutorCommon`.
- App: countdown có CTS riêng (hết race 2 vòng lặp song song); toast `NotifyIcon` sống đến khi balloon đóng (fallback dispose sau 1 phút); `OnCancelOperation` catch `Exception`; history lưu `TakeLast(20)`; `RaiseCanExecuteChanged` cho `ShowHelpCommand`; `App.xaml.cs` bọc `MarkAbandonedAsync`/`ApplyRetentionAsync` trong try/catch; ProgressBar tách row riêng khỏi elapsed text; sửa typo "Thởi gian" → "Thời gian" (3 chỗ — chỉ text hiển thị); xóa dead binding `VolumeSummary` trong `VolumeCard.xaml`.
- Integration: `VhdTestHelper` đọc stdout/stderr async song song + `WaitForExitAsync` (hết deadlock pattern), retry tìm drive letter (10 × 500ms), xóa `Verb="runas"` dead code, giới hạn `catch (Exception)`, tách rõ stdout/stderr trong message lỗi; tests dùng CTS timeout 5 phút; extract `BuildWipeRequest`; xóa `UnitTest1.cs` placeholder.
- Unit tests mới: `LogRedactorTests`, `MapHealthStatus`, SafetyPolicy (BitLocker Unknown/null FileSystem/HealthStatus), Retrim dirty, `ClearHistory_ThenAppend_RecreatesFile`, `Start_KeepsIsBusyWhileOperationRunning`.

### Verification
- `dotnet build -c Release`: PASS (0 warning, 0 error)
- `dotnet test -c Release --no-build`: PASS (97 unit, 2 VHD integration skipped đúng guard)
- `dotnet format --verify-no-changes`: PASS (sau khi chạy `dotnet format` chuẩn hóa CRLF cho file mới)

### Known limitations
- Worker sanitize output vẫn dùng profile của tài khoản admin (path user thật có thể lọt vào progress) — cần truyền user profile từ UI nếu muốn triệt để.
- TOCTOU giữa lúc re-validate volume và lúc cipher ghi theo drive letter chưa khóa volume (`FSCTL_LOCK_VOLUME`).
- FakeInventory trong unit tests worker bỏ qua tham số `driveLetter` (không bắt được bug lookup sai ổ).
- Fallback WMI path không đọc được isReadOnly/isDirty (giới hạn `Win32_Volume`).
