# 🎨 VILAI VIET - Revit Addin UI Design System (Redesigned)

> **Canonical update 2026-07-17:** Tài liệu áp dụng chính thức hiện nằm tại
> `docs/ui/VilaiViet_UI_Guidelines_v2.md`. Các ví dụ header tối, chữ phụ
> `#9BA3AF` và chữ ký 16 px ở phần lịch sử bên dưới đã bị supersede; không dùng
> để tạo hoặc sửa XAML mới.

Tài liệu này quy định các chuẩn mực thiết kế giao diện (UI) và trải nghiệm người dùng (UX) chuyên nghiệp cho toàn bộ các công cụ (Add-in) thuộc hệ sinh thái Revit của Vilai Viet. Mọi file XAML được tạo mới hoặc chỉnh sửa đều PHẢI tuân thủ các quy tắc dưới đây để đảm bảo tính đồng nhất thương hiệu và giảm mỏi mắt cho người dùng.

---

## 1. Hệ màu sắc chủ đạo (Professional Color Palette)

Để hòa hợp với giao diện tối mặc định của Autodesk Revit và giảm thiểu mỏi mắt khi sử dụng phần mềm trong nhiều giờ liên tục, hệ màu sắc được chuẩn hóa như sau:

| Loại phần tử | Mã màu Hex | Mô tả & Ứng dụng |
| :--- | :--- | :--- |
| **Window Background** | `#FFFFFF` | Màu nền trắng tinh khiết, sạch sẽ và tối giản. |
| **Card / Group Box Background** | `#F5F6F8` | Lớp nền thứ hai phân cấp cho các nhóm tùy chọn (Elevation). |
| **Input Background (TextBox/Combo)** | `#FFFFFF` | Nền trắng cho các ô nhập liệu. |
| **Primary Action Button** | `#007ACC` | Xanh dương công nghệ (VS Blue). Dùng cho: Draw, Select, Run. |
| **Secondary Button** | `#E5E7EB` | Màu xám nhạt trung tính. Dùng cho: Cancel, Close, settings phụ. Chữ bên trong màu đen. |
| **Destructive / Cancel Action** | `#D8262C` | Đỏ thương hiệu. Chỉ dùng khi Unjoin, Delete hoặc hành động nguy hiểm. |
| **Border / Divider** | `#D1D5DB` | Đường phân cách xám nhạt, tinh tế. |
| **Text Primary (Foreground)** | `#000000` | Màu đen tuyệt đối cho toàn bộ văn bản chính để đảm bảo độ tương phản cao nhất. |
| **Text Secondary (Labels)** | `#4B5563` | Màu xám đậm cho nhãn mô tả, phụ đề. |

---

## 2. Typography (Kiểu chữ & Phân cấp)

*   **Font Family mặc định:** `Segoe UI` (Font hệ thống tiêu chuẩn của Windows).
*   **Font Size & Weight:**
    *   **Tên Add-in trong Header:** `14px`, `Bold`, viết hoa.
    *   **Nhãn mô tả (Labels):** `12px`, `Regular`.
    *   **Chữ trong ô nhập liệu / Button:** `12px`, `SemiBold`.
    *   **Subtitle (Chú thích chức năng):** `11px`, `Regular`, màu `#9BA3AF`.

---

## 3. Cấu trúc Layout tiêu chuẩn (Responsive Grid)

Tất cả các cửa sổ (Window) sử dụng `<Grid Margin="16">` và phân chia Layout rõ ràng. Tuyệt đối không sử dụng các khối màu chói phân cách, thay vào đó hãy sử dụng Spacing hợp lý và đường phân cách mỏng `<Separator>`:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/> <!-- 0: Header Component -->
    <RowDefinition Height="Auto"/> <!-- 1: Content Area (Grid/StackPanel) -->
    <RowDefinition Height="16"/>   <!-- 2: Spacing -->
    <RowDefinition Height="Auto"/> <!-- 3: Bottom Action Buttons -->
</Grid.RowDefinitions>
```

---

## 4. Header Component chuẩn thương hiệu (Brand Header)

Mỗi công cụ Revit Add-in **BẮT BUỘC** phải sử dụng chung một bố cục Header chuẩn. Bố cục này tích hợp **Logo Vector sắc nét** cùng **Tên Add-in** (Không hiển thị chữ Vilai Viet bằng text để tránh trùng lặp). Nghiêm cấm dùng các kiểu tiêu đề cũ (không có logo, lạm dụng emoji, hoặc dùng hậu tố `@Vilai Viet` màu vàng kiểu cũ).

```xml
<Border Background="#1A1D21" Margin="-16,-16,-16,16" Padding="16,12">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/> <!-- Cột chứa Logo -->
            <ColumnDefinition Width="*"/>    <!-- Cột chứa Text -->
        </Grid.ColumnDefinitions>
        
        <!-- 1. Logo Vector chữ V chuẩn xác của Vilai Viet -->
        <Viewbox Grid.Column="0" Width="28" Height="28" Margin="0,0,12,0" VerticalAlignment="Center">
            <Canvas Width="100" Height="80">
                <!-- Tam giác xanh tím than ở trên -->
                <Path Fill="#1B3679" Data="M 30,10 L 70,10 L 50,45 Z"/>
                <!-- Cánh chữ V màu đỏ ôm bên ngoài -->
                <Path Fill="#D8262C" Data="M 10,10 L 32,10 L 50,55 L 68,10 L 90,10 L 50,80 Z"/>
            </Canvas>
        </Viewbox>
        
        <!-- 2. Tên Add-in -->
        <StackPanel Grid.Column="1" VerticalAlignment="Center">
            <TextBlock FontSize="14" FontWeight="Bold" Foreground="#F5F6F8">
                <Run Text="VILA" Foreground="#1B3679"/><Run Text="I"><Run.Foreground><LinearGradientBrush StartPoint="0,0" EndPoint="1,0"><GradientStop Color="#1B3679" Offset="0.7"/><GradientStop Color="#D8262C" Offset="0.7"/></LinearGradientBrush></Run.Foreground></Run><Run Text="VIET" Foreground="#D8262C"/><Run Text=" · RFI FILLER" Foreground="#F5F6F8" FontWeight="SemiBold"/>
            </TextBlock>
            
            <!-- Dòng chú thích chức năng (Subtitle) -->
            <TextBlock Text="Điền Request For Information nhanh từ template" 
                       FontSize="11" 
                       Foreground="#9BA3AF" 
                       Margin="0,2,0,0"/>
        </StackPanel>
    </Grid>
</Border>
```

---

## 5. Thiết kế các Control trong Resource Dictionary

Tránh hardcode trực tiếp màu sắc vào từng Element. Khai báo các Style chuẩn trong `<Window.Resources>`:

### 5.1 Style cho Button chính (Primary & Secondary Button)
```xml
<!-- Nút chính (Ví dụ: Draw, Select) -->
<Style x:Key="PrimaryBtn" TargetType="Button">
    <Setter Property="Height" Value="32"/>
    <Setter Property="Background" Value="#007ACC"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="FontFamily" Value="Segoe UI"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border CornerRadius="4" Background="{TemplateBinding Background}" Padding="12,0">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

### 5.2 Style cho TextBox nhập liệu phẳng (Modern TextBox)
```xml
<Style x:Key="ModernTextBox" TargetType="TextBox">
    <Setter Property="Height" Value="28"/>
    <Setter Property="Background" Value="#FFFFFF"/>
    <Setter Property="Foreground" Value="#000000"/>
    <Setter Property="BorderBrush" Value="#D1D5DB"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="6,2"/>
    <Setter Property="VerticalContentAlignment" Value="Center"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="TextBox">
                <Border Name="Border" CornerRadius="4" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                    <ScrollViewer x:Name="PART_ContentHost"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsFocused" Value="True">
                        <Setter TargetName="Border" Property="BorderBrush" Value="#007ACC"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

### 5.3 Style cho ComboBox (Dropdown)
Để đảm bảo tất cả chữ đều là màu đen trên nền trắng, hãy thiết lập:

```xml
<Style TargetType="ComboBoxItem">
    <Setter Property="Background" Value="#FFFFFF"/>
    <Setter Property="Foreground" Value="#000000"/>
    <Setter Property="Padding" Value="6,4"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="ComboBoxItem">
                <Border Name="Border" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
                    <ContentPresenter/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsHighlighted" Value="True">
                        <Setter TargetName="Border" Property="Background" Value="#E5E7EB"/>
                        <Setter Property="Foreground" Value="#000000"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

---

## 6. Tiêu đề cửa sổ & Watermark chữ ký (Window Title & Footer Watermark)

Để giữ giao diện sạch sẽ, chuyên nghiệp và ghi nhận bản quyền tác giả:

### 6.1 Quy tắc Tiêu đề Cửa sổ (Window Title):
*   **KHÔNG** thêm tiền tố/từ khóa `Antigravity` vào thuộc tính `Title` của cửa sổ Window XAML (Ví dụ: Tránh đặt `Title="Antigravity Issue Manager"`).
*   **Cú pháp chuẩn:** `Title="[Tên Add-in] - build [Phiên bản/Ngày]"` hoặc `Title="Vilai Viet [Tên Add-in]"` (Ví dụ: `Title="Issue Manager - build 2026-06-19.10"`).

### 6.2 Watermark chữ ký tác giả (Developer Signature):
*   Tại góc dưới cùng bên phải của mỗi cửa sổ (thường nằm ở dòng trạng thái/Footer hoặc góc trống cạnh các nút hành động), bắt buộc phải có dòng chữ hiển thị tên tác giả `@manhns`.
*   **Thông số chuẩn:** `FontSize="16"`, `FontWeight="Bold"`, `Foreground="#9BA3AF"`, `Opacity="0.8"`, `HorizontalAlignment="Right"`.

#### Ví dụ XAML Footer:
```xml
<Grid Grid.Row="3" Margin="0,10,0,0">
    <!-- Nút hành động bên trái / giữa -->
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
        <Button Content="Draw" Style="{StaticResource PrimaryBtn}"/>
        <Button Content="Cancel" Style="{StaticResource PrimaryBtn}" Background="#35393E" Margin="8,0,0,0"/>
    </StackPanel>
    
    <!-- Chữ ký tác giả căn lề phải, kích thước to rõ -->
    <TextBlock Text="@manhns" 
               FontSize="16" 
               FontWeight="Bold"
               Foreground="#9BA3AF" 
               Opacity="0.8" 
               HorizontalAlignment="Right" 
               VerticalAlignment="Bottom"/>
</Grid>
```

---

**💡 Hướng dẫn lập trình cùng AI:**
*Mỗi khi thiết kế giao diện Revit Add-in cho hệ sinh thái Vilai Viet, hãy copy phần mã nguồn Header Component này để nhúng trực tiếp vào cửa sổ XAML. Giao diện thực tế sẽ hiển thị sắc nét 100% dạng vector mà không cần dùng ảnh bitmap. Hãy luôn nhớ loại bỏ chữ "Antigravity" trong tiêu đề cửa sổ và đính kèm chữ ký "@manhns" ở góc dưới bên phải.*
