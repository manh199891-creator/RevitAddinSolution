# Workflow: Logging & Error Handling
Mục tiêu: Trace được mọi lỗi trong production mà không cần debug session.

---

## Setup: Serilog (khuyến nghị)

```xml
<!-- Antigravity.Main.csproj -->
<PackageReference Include="Serilog" Version="4.*" />
<PackageReference Include="Serilog.Sinks.File" Version="6.*" />
<PackageReference Include="Serilog.Sinks.Debug" Version="3.*" />
```

```csharp
// App.cs — khởi tạo khi load add-in
public class App : ExternalApplication
{
    public override Result OnStartup(UIControlledApplication app)
    {
        ConfigureLogging();
        // ...
    }

    private static void ConfigureLogging()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Antigravity", "Logs", "antigravity-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("AddInVersion", typeof(App).Assembly.GetName().Version)
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
            .WriteTo.Debug()
            .CreateLogger();

        Log.Information("Antigravity loaded on Revit {RevitVersion}", app.ControlledApplication.VersionNumber);
    }

    public override Result OnShutdown(UIControlledApplication app)
    {
        Log.CloseAndFlush();
        return Result.Succeeded;
    }
}
```

---

## Log path convention

```
%LOCALAPPDATA%\Antigravity\Logs\
  antigravity-20250115.log
  antigravity-20250116.log
  ...  (giữ 7 ngày)
```

---

## Logging levels — dùng đúng level

| Level | Dùng khi | Ví dụ |
|---|---|---|
| `Debug` | Dev info, không có trong prod release | `Log.Debug("Collecting walls, count={Count}", walls.Count)` |
| `Information` | Milestone, user action | `Log.Information("Command {Cmd} executed by user", nameof(CreateColumnCommand))` |
| `Warning` | Recoverable issue | `Log.Warning("Wall {Id} has no location curve, skipped", wall.Id)` |
| `Error` | Exception caught, operation failed | `Log.Error(ex, "Failed to create opening at {Point}", pt)` |
| `Fatal` | Add-in không load được | `Log.Fatal(ex, "Startup failed")` |

---

## Pattern: Command wrapper với logging

```csharp
public abstract class LoggedCommand : ExternalCommand
{
    protected abstract Result ExecuteCore(ExternalCommandData data, ref string message);

    public sealed override Result Execute(ExternalCommandData data,
        ref string message, ElementSet elements)
    {
        var commandName = GetType().Name;
        Log.Information("Command started: {Command}", commandName);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = ExecuteCore(data, ref message);
            sw.Stop();
            Log.Information("Command finished: {Command} → {Result} in {Ms}ms",
                commandName, result, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            Log.Information("Command cancelled: {Command}", commandName);
            return Result.Cancelled;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error(ex, "Command failed: {Command} after {Ms}ms", commandName, sw.ElapsedMilliseconds);
            message = $"Lỗi: {ex.Message}\nXem log tại: %LOCALAPPDATA%\\Antigravity\\Logs\\";
            ShowErrorDialog(ex);
            return Result.Failed;
        }
    }

    private void ShowErrorDialog(Exception ex)
    {
        var dialog = new TaskDialog("Lỗi — Antigravity")
        {
            MainIcon = TaskDialogIcon.TaskDialogIconError,
            MainInstruction = "Đã xảy ra lỗi không mong muốn.",
            MainContent = ex.Message,
            ExpandedContent = ex.StackTrace,
            FooterText = $"Log: %LOCALAPPDATA%\\Antigravity\\Logs\\"
        };
        dialog.Show();
    }
}

// Sử dụng
public class CreateColumnCommand : LoggedCommand
{
    protected override Result ExecuteCore(ExternalCommandData data, ref string message)
    {
        // chỉ viết business logic ở đây
        // logging và error handling đã có ở base class
    }
}
```

---

## Structured logging — log đủ context

```csharp
// ✅ Đúng — có context đủ để debug không cần reproduce
Log.Error(ex, 
    "Cannot create opening: WallId={WallId}, Width={Width}mm, Height={Height}mm, Level={Level}",
    wall.Id.IntegerValue, widthMm, heightMm, level.Name);

// ❌ Sai — không biết gì ngoài exception message
Log.Error("Error: " + ex.Message);
```

---

## Log viewer tip

```powershell
# Xem log realtime trong PowerShell
Get-Content "$env:LOCALAPPDATA\Antigravity\Logs\antigravity-$(Get-Date -f yyyyMMdd).log" -Wait -Tail 50

# Lọc chỉ Error
Select-String -Path "$env:LOCALAPPDATA\Antigravity\Logs\*.log" -Pattern "\[ERR\]"
```

---

## Revit Journal — đọc khi cần debug crash

Khi Revit crash, journal file ghi lại toàn bộ sequence:
```
%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit {year}\Journals\
  journal.YYYY-MM-DD-HH-MM-SS.txt
```

Pattern tìm lỗi của add-in:
```
Tìm: "Antigravity" hoặc "Jrn.Command" → xem dòng ngay trước crash
```
