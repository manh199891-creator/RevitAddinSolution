[CmdletBinding()]
param([string]$RepositoryRoot)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$translations = [ordered]@{
    '(Chụp tự động từ Revit khi bấm Create Issue)' = '(Captured automatically from Revit when Create Issue is clicked)'
    '(Gộp các Tag cùng loại)' = '(Combine tags of the same type)'
    '(Quản lý đường dẫn)' = '(Leader management)'
    '(Tự động ghi chú)' = '(Automatic annotation)'
    '(Xếp chồng thông minh)' = '(Intelligent stacking)'
    '⬜ Trắng' = '⬜ White'
    '▶  XỬ LÝ ZONE' = '▶  PROCESS ZONES'
    '↺  Tải lại' = '↺  Reload'
    '✖ Huỷ' = '✖ Cancel'
    '✖ Xóa ảnh' = '✖ Remove Image'
    '1 · REGEX CẤU HÌNH NHẬN DIỆN' = '1 · IDENTIFICATION REGEX'
    '2 · CHỌN LAYER &amp; QUÉT VÙNG CAD' = '2 · SELECT LAYER &amp; CAD REGION'
    '3 · ĐỒNG NHẤT TOẠ ĐỘ (ORIGIN POINT)' = '3 · COORDINATE ALIGNMENT (ORIGIN POINT)'
    'ALIGN (Căn thẳng hàng)' = 'ALIGN'
    'ANTI-OVERLAP (Chống đè chữ)' = 'ANTI-OVERLAP'
    'Áp dụng cho các Room đang chọn (Pre-selection)' = 'Apply to preselected rooms'
    'Bật/Tắt cặp kết nối này' = 'Enable or disable this join pair'
    'Bẻ góc vuông tự động (ngang -> dọc)' = 'Create orthogonal leaders automatically'
    'Bị lỗi / Không Join được:' = 'Failed or unable to join:'
    'Biểu thức chính quy (Regex) để trích xuất đường kính (DN) và cao độ (COP) từ chuỗi Text AutoCAD.' = 'Regular expression used to extract diameter (DN) and elevation (COP) from AutoCAD text.'
    'Cắt Vật lý - Copy &amp; Adjust (Chỉ Tường/Dầm, Mất Rebar)' = 'Physical split by copy and adjust (walls and framing only; rebar is not retained)'
    'Cấu Kiện Xung Đột' = 'Clashing Element'
    'Cây phân tích Substrate/Ceiling sẽ hiển thị tại đây.' = 'The substrate and ceiling analysis tree will appear here.'
    'Check Phòng Trống' = 'Check Empty Rooms'
    'Chỉ tính Volume và gán BIM_Volume (Không cắt)' = 'Calculate volume and assign BIM_Volume without splitting'
    'Chiều dài (mm):' = 'Length (mm):'
    'Chọn cấu kiện tính toán khối lượng:' = 'Select element categories for quantity calculation:'
    'Chọn đối tượng mẫu trên CAD để tự lấy Layer' = 'Pick a sample CAD object to detect its layer'
    'Chọn Layer sleeve (hoặc bấm ... chọn đối tượng mẫu), sau đó quét chọn phạm vi làm việc trên CAD.' = 'Select the sleeve layer or pick a sample object, then select the CAD work region.'
    'Chọn Level gốc và Family Sleeve (Generic Model) để đặt.' = 'Select the base level and sleeve family (Generic Model).'
    'Chọn tất cả' = 'Select all'
    'Chưa có dữ liệu nào được chọn.' = 'No data has been selected.'
    'Chưa đồng bộ tọa độ.' = 'Coordinates have not been synchronized.'
    'Cột kết cấu (Structural Columns)' = 'Structural Columns'
    'Ctrl+V để dán ảnh, hoặc bấm 📁 để chọn file. Bấm ✏️ Markup để chỉnh sửa.' = 'Press Ctrl+V to paste an image, or click 📁 to choose a file. Click ✏️ Markup to edit.'
    'Đã được Join trước đó:' = 'Previously joined:'
    'Đã Join thành công:' = 'Joined successfully:'
    'Dầm kết cấu (Structural Framing)' = 'Structural Framing'
    'Đánh mã phòng tự động' = 'Assign room codes automatically'
    'DANH SÁCH GIAO CẮT CHƯA XỬ LÝ ĐƯỢC' = 'UNRESOLVED INTERSECTIONS'
    'Đảo chiều bản lề cho tất cả cửa 1 cánh' = 'Flip the hinge side for all single-leaf doors'
    'DISTRIBUTE (Giãn đều)' = 'DISTRIBUTE'
    'Đọc Leader, Text từ AutoCAD để đặt Family Sleeve tự động' = 'Read AutoCAD leaders and text to place sleeve families automatically'
    'ĐÓNG' = 'CLOSE'
    'Dùng Shared Parameter (AG_...) thay vì Mặc định' = 'Use shared parameters (AG_...) instead of defaults'
    'Entire Project (Toàn dự án)' = 'Entire Project'
    'Gộp nhiều tag thành 1 tag có nhiều đường dẫn' = 'Combine multiple tags into one tag with multiple leaders'
    'Group (Gộp nét)' = 'Group lines'
    'Hình ảnh 2D / Chú thích' = '2D Image / Annotation'
    'Hình ảnh 3D (Revit Snapshot)' = '3D Image (Revit Snapshot)'
    'Host structural columns (Bổ trụ/Vách)' = 'Host structural columns and wall piers'
    'Hủy' = 'Cancel'
    'ID Cấu Kiện' = 'Element ID'
    'ID Cửa' = 'Door ID'
    'JSON Rule Engine sẽ được cấu hình tại đây.' = 'The JSON rule engine will be configured here.'
    'KẾT QUẢ THỰC HIỆN' = 'EXECUTION RESULTS'
    'Khoảng cách cách xa reference (ví dụ nhập 200 để Tag cách Dimension 200mm)' = 'Distance from the reference; for example, enter 200 for a 200 mm offset'
    'Khoảng cách chữ đẩy ra so với tường' = 'Text offset from the wall'
    'Khoảng cách tối đa (mét) giữa các cấu kiện để gộp chung vào 1 Tag (ví dụ: 10m). Nếu để trống hoặc 0, gộp tất cả không giới hạn.' = 'Maximum distance in metres between elements to merge into one tag. Enter 0 for no limit.'
    'Khoảng cách tối đa để gộp chung 1 Tag (nhập 0 để gộp vô hạn)' = 'Maximum merge distance; enter 0 for no limit'
    'Khoảng cách từ Tag đến Elbow (mặc định 200mm)' = 'Distance from the tag to the elbow (default 200 mm)'
    'Kiểm Soát Xung Đột Khoảng Mở Cửa' = 'Door Clearance Clash Control'
    'Kiểm Tra Lỗ Mở Sàn' = 'Floor Opening Check'
    'Làm tất cả leader song song (theo tag đầu tiên)' = 'Make all leaders parallel to the first tag'
    'Làm thẳng landing line (nằm ngang)' = 'Make landing lines horizontal'
    'Loại trừ vách kính (Curtain Wall)' = 'Exclude curtain walls'
    'Lọc theo Room:' = 'Filter by room:'
    'Lớp Hoàn Thiện - build 2026-07-09' = 'Room Finishes - build 2026-07-09'
    'Mark Cửa' = 'Door Mark'
    'Mật khẩu Master:' = 'Master password:'
    'Máy tính này không nằm trong danh sách được phép' = 'This computer is not on the authorized list'
    'MỞ KHÓA' = 'UNLOCK'
    "Nhấn 'QUÉT XUNG ĐỘT' để bắt đầu phân tích mô hình..." = "Click 'SCAN CLASHES' to start model analysis..."
    'Offset lên trần (mm):' = 'Ceiling offset (mm):'
    'PHẠM VI ÁP DỤNG' = 'SCOPE'
    'Phần tử A' = 'Element A'
    'Phần tử B' = 'Element B'
    'Phát hiện và quản lý các giao cắt hình học giữa Khoảng mở cửa (Clearance Box) và Cấu kiện kết cấu.' = 'Detect and manage geometric clashes between door clearance boxes and structural elements.'
    'Phương thức xử lý:' = 'Processing method:'
    'Plan View (Mặt bằng)' = 'Plan View'
    'Quét chọn phạm vi làm việc trên CAD' = 'Select the CAD work region'
    'QUÉT XUNG ĐỘT' = 'SCAN CLASHES'
    'QUY TẮC JOIN THEO CẶP' = 'PAIR-BASED JOIN RULES'
    'Ready. Chọn các Tag/Dimension rồi bấm nút tương ứng.' = 'Ready. Select tags or dimensions, then choose an action.'
    'Revit Default (Mặc định)' = 'Revit Default'
    'Sẵn sàng.' = 'Ready.'
    'Section View (Mặt cắt)' = 'Section View'
    'Selected Elements (Đang quét chọn)' = 'Selected Elements'
    'Sử dụng' = 'Enabled'
    'Tạo các lỗ mở (void) trên sàn tự động từ đường nét AutoCAD' = 'Create floor openings automatically from AutoCAD geometry'
    'Tạo đường dẫn (Leader) cho Tag' = 'Create leaders for tags'
    'Tạo Hoàn Thiện' = 'Create Finishes'
    'Tạo Khối Khoảng Mở Cửa' = 'Create Door Clearance Boxes'
    'Tạo Lỗ Mở Dầm/Vách từ CAD' = 'Create Beam and Wall Openings from CAD'
    'Tạo lớp trát, ốp, sơn, sàn tự động theo Room' = 'Create plaster, cladding, paint, and floor finishes automatically by room'
    'Tạo Parts và gán Parameter tự động (Khuyên dùng cho 4D)' = 'Create Parts and assign parameters automatically (recommended for 4D)'
    'Tạo Sàn (Floor)' = 'Create Floors'
    'Tạo Vách (Wall)' = 'Create Walls'
    'Tên Cấu Kiện' = 'Element Name'
    'Tên Cửa / Window' = 'Door / Window Name'
    'Thể Tích Giao (m³)' = 'Clash Volume (m³)'
    'Thiết lập điểm gốc giống nhau giữa Revit và CAD để đồng bộ tọa độ.' = 'Set the same origin point in Revit and CAD to synchronize coordinates.'
    'Tiền tố (Prefix):' = 'Prefix:'
    'Tổng giao cắt tìm thấy:' = 'Intersections found:'
    'Tổng số xung đột phát hiện: ' = 'Total clashes detected: '
    'Tự động join khi vẽ mới hoặc chỉnh sửa' = 'Join automatically when elements are created or modified'
    'Tự động né nhau (Anti-overlap) sau khi Tag' = 'Run anti-overlap after tagging'
    'Tự động tạo Tag cho các đối tượng chưa được tag trong View' = 'Create tags automatically for untagged elements in the view'
    'Vách/Tường (Walls)' = 'Walls'
    'Vilai Viet - Kiểm Soát Xung Đột Khoảng Mở Cửa' = 'Vilai Viet - Door Clearance Clash Control'
    'Vilai Viet - Tạo Khối Khoảng Mở Cửa' = 'Vilai Viet - Create Door Clearance Boxes'
    'Vilai Viet Kiểm Tra Lỗ Mở Sàn' = 'Vilai Viet Floor Opening Check'
    'Vilai Viet Lớp Hoàn Thiện - build 2026-07-09' = 'Vilai Viet Room Finishes - build 2026-07-09'
    'Vilai Viet Tạo Lỗ Mở Dầm/Vách từ CAD' = 'Vilai Viet Create Beam and Wall Openings from CAD'
    'Vilai Viet ZoneSplit - Phân chia khối lượng' = 'Vilai Viet - Zone Split'
    'Xác thực' = 'Authorization'
    'Xếp chồng các tag và kéo leader sang bên phải' = 'Stack tags and route leaders to the right'
    'Xếp chồng các tag và kéo leader sang bên trái' = 'Stack tags and route leaders to the left'
    'XUẤT EXCEL (CSV)' = 'EXPORT CSV'
    'ZoneSplit - Phân chia khối lượng' = 'Zone Split'
    'ZOOM XEM CHỖ GIAO' = 'ZOOM TO CLASH'
    '💾  Lưu cấu hình' = '💾  Save Configuration'
    '💾 Lưu markup' = '💾 Save Markup'
    '📁 Chọn ảnh...' = '📁 Choose Image...'
    '🔁 Mirror Hinge (Đảo bản lề)' = '🔁 Mirror Hinge'
    '🔴 Đỏ' = '🔴 Red'
    '🔵 Xanh dương' = '🔵 Blue'
}

$attributePattern = '(?<prefix>\b(?:Title|Text|Content|Header|ToolTip|TitleText|SubtitleText)=")(?<value>[^"]*)(?<suffix>")'
$utf8NoBom = [Text.UTF8Encoding]::new($false)
$changed = [Collections.Generic.List[string]]::new()

Get-ChildItem (Join-Path $RepositoryRoot 'src') -Recurse -File -Filter '*.xaml' |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    ForEach-Object {
        $content = [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8)
        $updated = [regex]::Replace($content, $attributePattern, {
            param($match)
            $value = $match.Groups['value'].Value
            if ($translations.Contains($value)) { $value = $translations[$value] }
            return $match.Groups['prefix'].Value + $value + $match.Groups['suffix'].Value
        })
        if ($updated -ne $content) {
            [xml]$null = $updated
            [IO.File]::WriteAllText($_.FullName, $updated, $utf8NoBom)
            $changed.Add($_.FullName.Substring($RepositoryRoot.Length + 1))
        }
    }

Write-Host "Translated visible XAML text in $($changed.Count) files."
$changed | Sort-Object | ForEach-Object { Write-Host " - $_" }
