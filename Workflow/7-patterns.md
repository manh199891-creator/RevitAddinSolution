# Workflow: Advanced Revit API Patterns
Mục tiêu: Các pattern nâng cao — dùng đúng chỗ, tránh anti-pattern phổ biến.

---

## Pattern 1: ExternalEvent (async operations)

Dùng khi: cần trigger Revit action từ modeless dialog, WPF window, hoặc background thread.

```csharp
// Handler
public class CreateOpeningHandler : IExternalEventHandler
{
    public OpeningParams Params { get; set; }

    public void Execute(UIApplication app)
    {
        var doc = app.ActiveUIDocument.Document;
        using (var tx = new Transaction(doc, "Create Opening"))
        {
            tx.Start();
            // tạo opening với this.Params
            tx.Commit();
        }
    }

    public string GetName() => "Create Opening";
}

// Khởi tạo (1 lần, khi load add-in)
public static class EventRegister
{
    public static ExternalEvent CreateOpeningEvent;
    private static CreateOpeningHandler _handler;

    public static void Register()
    {
        _handler = new CreateOpeningHandler();
        CreateOpeningEvent = ExternalEvent.Create(_handler);
    }

    public static void Raise(OpeningParams p)
    {
        _handler.Params = p;
        CreateOpeningEvent.Raise();
    }
}

// Từ WPF button click (background thread OK)
private void BtnCreate_Click(object sender, RoutedEventArgs e)
{
    EventRegister.Raise(new OpeningParams { Width = 800, Height = 2100 });
}
```

---

## Pattern 2: Extensible Storage (lưu data vào element)

Dùng khi: cần gắn dữ liệu tùy chỉnh vào Revit element (persist qua save/load).

```csharp
public static class OpeningDataSchema
{
    private static readonly Guid SchemaGuid = new Guid("A1B2C3D4-...");
    private const string SchemaName = "AntigravityOpeningData";

    public static Schema GetOrCreateSchema()
    {
        var schema = Schema.Lookup(SchemaGuid);
        if (schema != null) return schema;

        var builder = new SchemaBuilder(SchemaGuid);
        builder.SetSchemaName(SchemaName);
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField("SourceDwgGuid", typeof(string));
        builder.AddSimpleField("CreatedDate", typeof(string));
        return builder.Finish();
    }

    public static void WriteData(Element element, string dwgGuid)
    {
        var schema = GetOrCreateSchema();
        var entity = new Entity(schema);
        entity.Set<string>(schema.GetField("SourceDwgGuid"), dwgGuid);
        entity.Set<string>(schema.GetField("CreatedDate"), DateTime.Now.ToString("O"));
        element.SetEntity(entity);
    }

    public static string ReadDwgGuid(Element element)
    {
        var schema = Schema.Lookup(SchemaGuid);
        if (schema == null) return null;
        var entity = element.GetEntity(schema);
        return entity.IsValid() ? entity.Get<string>("SourceDwgGuid") : null;
    }
}
```

---

## Pattern 3: DocumentChanged Event (track model changes)

```csharp
public class ChangeTracker
{
    public void Register(ControlledApplication app)
    {
        app.DocumentChanged += OnDocumentChanged;
    }

    private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
    {
        // Chỉ quan tâm element type cụ thể
        var addedWalls = e.GetAddedElementIds()
            .Select(id => e.GetDocument().GetElement(id))
            .OfType<Wall>()
            .ToList();

        if (!addedWalls.Any()) return;

        // Không modify model trong event này → dùng ExternalEvent
        foreach (var wall in addedWalls)
        {
            Logger.Info($"Wall added: {wall.Id}");
        }
    }
}
```

> ⚠️ Không dùng Transaction trong `DocumentChanged` handler — chỉ đọc data.

---

## Pattern 4: Failure Handling (suppress/handle warnings)

```csharp
public class SilentFailureHandler : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
    {
        var failures = accessor.GetFailureMessages();

        foreach (var failure in failures)
        {
            if (failure.GetSeverity() == FailureSeverity.Warning)
            {
                // Dismiss warnings tự động (e.g. overlapping elements)
                accessor.DeleteWarning(failure);
            }
            else if (failure.GetSeverity() == FailureSeverity.Error)
            {
                // Log error — để Revit xử lý
                Logger.Error($"Revit Error: {failure.GetDescriptionText()}");
            }
        }

        return FailureProcessingResult.Continue;
    }
}

// Dùng trong transaction
var opts = tx.GetFailureHandlingOptions();
opts.SetFailuresPreprocessor(new SilentFailureHandler());
tx.SetFailureHandlingOptions(opts);
```

---

## Pattern 5: DynamicModel Update (DMU)

Dùng khi: cần tự động update elements khi element khác thay đổi.

```csharp
public class WallOpeningUpdater : IUpdater
{
    private static readonly UpdaterId _updaterId = new UpdaterId(
        new AddInId(new Guid("...")),
        new Guid("B1C2D3..."));

    public void Execute(UpdaterData data)
    {
        var doc = data.GetDocument();
        foreach (var id in data.GetModifiedElementIds())
        {
            var wall = doc.GetElement(id) as Wall;
            if (wall == null) continue;
            // Recalculate openings in this wall
            ReSyncOpenings(doc, wall);
        }
    }

    public static void Register(Document doc)
    {
        UpdaterRegistry.RegisterUpdater(new WallOpeningUpdater(), doc);
        var filter = new ElementCategoryFilter(BuiltInCategory.OST_Walls);
        UpdaterRegistry.AddTrigger(_updaterId, filter, Element.GetChangeTypeGeometry());
    }

    public UpdaterId GetUpdaterId() => _updaterId;
    public ChangePriority GetChangePriority() => ChangePriority.Structure;
    public string GetUpdaterName() => "Wall Opening Updater";
    public string GetAdditionalInformation() => "Antigravity – sync openings to wall changes";
}
```

> ⚠️ DMU chạy rất nhiều lần — giữ `Execute()` cực kỳ nhẹ, dùng early-return tích cực.

---

## Pattern 6: Pick Object với filter

```csharp
public static Element PickStructuralColumn(UIDocument uidoc)
{
    var filter = new SelectionFilter(e =>
        e.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns);

    try
    {
        var reference = uidoc.Selection.PickObject(
            ObjectType.Element,
            filter,
            "Chọn cột kết cấu");
        return uidoc.Document.GetElement(reference);
    }
    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
    {
        return null; // User nhấn Escape — không phải lỗi
    }
}

private class SelectionFilter : ISelectionFilter
{
    private readonly Func<Element, bool> _predicate;
    public SelectionFilter(Func<Element, bool> predicate) => _predicate = predicate;
    public bool AllowElement(Element elem) => _predicate(elem);
    public bool AllowReference(Reference reference, XYZ position) => false;
}
```

---

## Anti-patterns phải tránh

| Anti-pattern | Vấn đề | Thay bằng |
|---|---|---|
| `doc.Regenerate()` trong loop | Cực kỳ chậm | Gọi 1 lần sau loop |
| `new Transaction()` không `using` | Leak nếu exception | Luôn dùng `using` |
| Cache `Document` object | Invalid sau close | Lấy từ `UIApplication` mỗi lần |
| Gọi API từ `Task.Run()` | Crash Revit | Dùng `ExternalEvent` |
| `GetElementById` trong loop N lần | O(N) DB lookups | Bulk collect trước |
| `element.Parameters["Name"]` | Fragile, locale-dependent | Dùng `BuiltInParameter` |
