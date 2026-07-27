using Antigravity.DrawBeams.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Antigravity.DrawBeams.Services
{
    public class BeamDiagnosticCollector
    {
        private static readonly Lazy<BeamDiagnosticCollector> _instance =
            new Lazy<BeamDiagnosticCollector>(() => new BeamDiagnosticCollector());
        public static BeamDiagnosticCollector Instance => _instance.Value;

        private readonly object _lock = new object();
        private BeamDiagnosticSession _currentSession;
        private BeamDiagnosticOptions _options;

        public BeamDiagnosticCollector(BeamDiagnosticOptions options = null)
        {
            _options = options ?? new BeamDiagnosticOptions();
        }

        public BeamDiagnosticOptions Options
        {
            get
            {
                lock (_lock) return _options;
            }
            set
            {
                lock (_lock) _options = value ?? new BeamDiagnosticOptions();
            }
        }

        public BeamDiagnosticSession CurrentSession
        {
            get
            {
                lock (_lock) return _currentSession;
            }
        }

        public BeamDiagnosticSession StartSession(string sessionId = null, BeamDiagnosticOptions options = null)
        {
            lock (_lock)
            {
                if (options != null) _options = options;

                string id = !string.IsNullOrEmpty(sessionId) ? sessionId : $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}".Substring(0, 23);
                _currentSession = new BeamDiagnosticSession
                {
                    SessionId = id,
                    StartTime = DateTime.Now
                };

                return _currentSession;
            }
        }

        public void Record(BeamDiagnosticEntry entry)
        {
            if (entry == null) return;

            lock (_lock)
            {
                if (_options == null || !_options.Enabled) return;
                if (_currentSession == null) StartSession();

                if (string.IsNullOrEmpty(entry.SessionId))
                {
                    entry.SessionId = _currentSession.SessionId;
                }

                // Automatic geometry length/angle computation if missing
                if (entry.Length <= 0 && (entry.StartX != 0 || entry.EndX != 0 || entry.StartY != 0 || entry.EndY != 0))
                {
                    double dx = entry.EndX - entry.StartX;
                    double dy = entry.EndY - entry.StartY;
                    entry.Length = Math.Sqrt(dx * dx + dy * dy);
                    if (entry.Length > 1e-9)
                    {
                        double a = Math.Atan2(dy, dx);
                        while (a < 0) a += Math.PI;
                        while (a >= Math.PI) a -= Math.PI;
                        entry.AngleDegrees = a * 180.0 / Math.PI;
                    }
                }

                _currentSession.Entries.Add(entry);
            }
        }

        public void RecordWarning(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            lock (_lock)
            {
                if (_options == null || !_options.Enabled) return;
                if (_currentSession == null) StartSession();

                _currentSession.Warnings.Add(message);
            }
        }

        public void CompleteSession()
        {
            lock (_lock)
            {
                if (_currentSession == null) return;

                if (!_currentSession.EndTime.HasValue)
                {
                    _currentSession.EndTime = DateTime.Now;
                }

                ValidateCounterInvariants();

                if (_options != null && _options.Enabled && _options.AutoExport)
                {
                    ExportJson();
                    ExportCsv();
                    ExportLineageCsv();
                }
            }
        }

        public void ValidateCounterInvariants()
        {
            if (_currentSession == null) return;

            var session = _currentSession;
            var pipe = session.PipelineSummary;
            var rev = session.RevitSummary;

            List<BeamDiagnosticEntry> entriesSnapshot;
            lock (_lock)
            {
                entriesSnapshot = session.Entries.ToList();
            }

            int actualRawCount = entriesSnapshot.Count(e => e.Stage == BeamDiagnosticStage.RawBeamCandidate && (e.Action == BeamDiagnosticAction.Created || e.Action == BeamDiagnosticAction.Kept));
            int actualChainCount = entriesSnapshot.Count(e => e.Stage == BeamDiagnosticStage.ContinuityChainCreated);
            int actualJunctionSplitCount = entriesSnapshot.Count(e => e.Stage == BeamDiagnosticStage.JunctionSplit);
            int actualDimSplitCount = entriesSnapshot.Count(e => e.Stage == BeamDiagnosticStage.DimensionSplit);
            int actualSuppressedCount = entriesSnapshot.Where(e => e.Stage == BeamDiagnosticStage.OverlapDecision && e.Action == BeamDiagnosticAction.Suppressed).Select(e => e.LoserDiagnosticId ?? e.CandidateId).Distinct().Count();
            int actualFinalCount = entriesSnapshot.Count(e => e.Stage == BeamDiagnosticStage.FinalCandidate && e.Action == BeamDiagnosticAction.Kept);

            int actualRevitCreated = entriesSnapshot.Count(e => e.Stage == BeamDiagnosticStage.RevitCreateResult && e.Action == BeamDiagnosticAction.Created);
            int actualRevitSkipped = entriesSnapshot.Count(e => e.Stage == BeamDiagnosticStage.RevitGuardDecision && e.Action == BeamDiagnosticAction.SkippedDuplicate);
            int actualRevitFailed = entriesSnapshot.Count(e => e.Stage == BeamDiagnosticStage.RevitCreateResult && e.Action == BeamDiagnosticAction.Failed);

            var mismatchList = new List<string>();

            if (pipe.RawCandidatesCount > 0 && actualRawCount > 0 && pipe.RawCandidatesCount != actualRawCount)
                mismatchList.Add($"RawCandidates (Summary: {pipe.RawCandidatesCount}, Entries: {actualRawCount})");

            if (actualChainCount > 0 && pipe.ContinuityChainsCount != actualChainCount)
                mismatchList.Add($"ContinuityChains (Summary: {pipe.ContinuityChainsCount}, Entries: {actualChainCount})");

            if (actualJunctionSplitCount > 0 && pipe.JunctionSplitsCount != actualJunctionSplitCount)
                mismatchList.Add($"JunctionSplits (Summary: {pipe.JunctionSplitsCount}, Entries: {actualJunctionSplitCount})");

            if (actualDimSplitCount > 0 && pipe.DimensionSplitsCount != actualDimSplitCount)
                mismatchList.Add($"DimensionSplits (Summary: {pipe.DimensionSplitsCount}, Entries: {actualDimSplitCount})");

            if (actualSuppressedCount > 0 && pipe.SuppressedOverlapsCount != actualSuppressedCount)
                mismatchList.Add($"SuppressedOverlaps (Summary: {pipe.SuppressedOverlapsCount}, Entries: {actualSuppressedCount})");

            if (actualFinalCount > 0 && pipe.AfterOverlapCount != actualFinalCount)
                mismatchList.Add($"AfterOverlap/Final (Summary: {pipe.AfterOverlapCount}, Entries: {actualFinalCount})");

            if (actualRevitCreated > 0 && rev.CreatedCount != actualRevitCreated)
                mismatchList.Add($"RevitCreated (Summary: {rev.CreatedCount}, Entries: {actualRevitCreated})");

            if (actualRevitSkipped > 0 && rev.SkippedCount != actualRevitSkipped)
                mismatchList.Add($"RevitSkipped (Summary: {rev.SkippedCount}, Entries: {actualRevitSkipped})");

            if (mismatchList.Count > 0)
            {
                string warnMsg = $"DiagnosticCounterInvariantFailed: Mismatches detected in {string.Join("; ", mismatchList)}";
                Record(new BeamDiagnosticEntry
                {
                    Stage = BeamDiagnosticStage.FinalCandidate,
                    Action = BeamDiagnosticAction.Warning,
                    Reason = warnMsg
                });
                RecordWarning(warnMsg);
            }
        }

        public (bool Success, string FilePath, string Warning) ExportJson(string directory = null)
        {
            lock (_lock)
            {
                try
                {
                    if (_currentSession == null) return (false, null, "No active diagnostic session to export.");

                    if (!_currentSession.EndTime.HasValue)
                    {
                        _currentSession.EndTime = DateTime.Now;
                    }

                    string targetDir = !string.IsNullOrEmpty(directory)
                        ? directory
                        : _options.ExportDirectory;

                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    string timeStampStr = _currentSession.StartTime.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    string fileName = $"DrawBeams_{timeStampStr}_{_currentSession.SessionId}.json";
                    string filePath = Path.Combine(targetDir, fileName);

                    string jsonContent = SerializeSessionToJson(_currentSession);
                    File.WriteAllText(filePath, jsonContent, Encoding.UTF8);

                    return (true, filePath, null);
                }
                catch (Exception ex)
                {
                    string warnMsg = $"Exporting JSON diagnostic failed: {ex.Message}";
                    RecordWarning(warnMsg);
                    return (false, null, warnMsg);
                }
            }
        }

        public (bool Success, string FilePath, string Warning) ExportCsv(string directory = null)
        {
            lock (_lock)
            {
                try
                {
                    if (_currentSession == null) return (false, null, "No active diagnostic session to export.");

                    if (!_currentSession.EndTime.HasValue)
                    {
                        _currentSession.EndTime = DateTime.Now;
                    }

                    string targetDir = !string.IsNullOrEmpty(directory)
                        ? directory
                        : _options.ExportDirectory;

                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    string timeStampStr = _currentSession.StartTime.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    string fileName = $"DrawBeams_{timeStampStr}_{_currentSession.SessionId}.csv";
                    string filePath = Path.Combine(targetDir, fileName);

                    string csvContent = SerializeSessionToCsv(_currentSession);
                    File.WriteAllText(filePath, csvContent, Encoding.UTF8);

                    return (true, filePath, null);
                }
                catch (Exception ex)
                {
                    string warnMsg = $"Exporting CSV diagnostic failed: {ex.Message}";
                    RecordWarning(warnMsg);
                    return (false, null, warnMsg);
                }
            }
        }

        public (bool Success, string FilePath, string Warning) ExportLineageCsv(string directory = null)
        {
            lock (_lock)
            {
                try
                {
                    if (_currentSession == null) return (false, null, "No active diagnostic session to export.");

                    if (!_currentSession.EndTime.HasValue)
                    {
                        _currentSession.EndTime = DateTime.Now;
                    }

                    string targetDir = !string.IsNullOrEmpty(directory)
                        ? directory
                        : _options.ExportDirectory;

                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    string timeStampStr = _currentSession.StartTime.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    string fileName = $"DrawBeams_{timeStampStr}_{_currentSession.SessionId}.lineage.csv";
                    string filePath = Path.Combine(targetDir, fileName);

                    string csvContent = SerializeSessionToLineageCsv(_currentSession);
                    File.WriteAllText(filePath, csvContent, Encoding.UTF8);

                    return (true, filePath, null);
                }
                catch (Exception ex)
                {
                    string warnMsg = $"Exporting Lineage CSV diagnostic failed: {ex.Message}";
                    RecordWarning(warnMsg);
                    return (false, null, warnMsg);
                }
            }
        }

        public string BuildSummary()
        {
            lock (_lock)
            {
                if (_currentSession == null) return "No diagnostic session active.";

                var session = _currentSession;
                var cad = session.CadSelectionSummary;
                var pipe = session.PipelineSummary;
                var rev = session.RevitSummary;

                var (jsonSuccess, jsonPath, _) = ExportJson();
                var (csvSuccess, csvPath, _) = ExportCsv();
                var (linSuccess, linPath, _) = ExportLineageCsv();

                var sb = new StringBuilder();
                sb.AppendLine("===================================");
                sb.AppendLine("DRAW BEAMS DIAGNOSTIC SUMMARY");
                sb.AppendLine("===================================");
                sb.AppendLine($"Session ID: {session.SessionId}");
                sb.AppendLine($"Start Time: {session.StartTime:yyyy-MM-dd HH:mm:ss}");
                if (session.EndTime.HasValue)
                {
                    sb.AppendLine($"End Time:   {session.EndTime.Value:yyyy-MM-dd HH:mm:ss}");
                }
                sb.AppendLine();

                if (session.Entries.Count == 0 && (pipe.RawCandidatesCount > 0 || cad.TotalEntities > 0))
                {
                    sb.AppendLine("WARNING: Beam diagnostic instrumentation failed: candidates were processed but no diagnostic entries were recorded.");
                    sb.AppendLine();
                }

                if (pipe.ContinuityChainsCount > pipe.RawCandidatesCount && pipe.JunctionSplitsCount == 0 && pipe.DimensionSplitsCount == 0)
                {
                    sb.AppendLine("WARNING: Continuity output exceeds raw candidate count without recorded split events.");
                    sb.AppendLine();
                }

                sb.AppendLine($"Diagnostic Entries: {session.Entries.Count}");
                sb.AppendLine($"Counter Warnings:   {session.Entries.Count(e => e.Action == BeamDiagnosticAction.Warning)}");
                sb.AppendLine();
                sb.AppendLine("CAD Entities:");
                sb.AppendLine($"  - Total: {cad.TotalEntities}");
                sb.AppendLine($"  - Lines: {cad.LineCount}");
                sb.AppendLine($"  - Polylines: {cad.PolylineCount}");
                sb.AppendLine($"  - Texts: {cad.TextCount}");
                sb.AppendLine($"  - MTexts: {cad.MTextCount}");
                sb.AppendLine($"  - Skipped: {cad.SkippedCount}");
                sb.AppendLine();
                sb.AppendLine("Beam Pipeline:");
                sb.AppendLine($"  - Raw Candidates: {pipe.RawCandidatesCount}");
                sb.AppendLine($"  - Continuity Chains: {pipe.ContinuityChainsCount}");
                sb.AppendLine($"  - Junction Splits: {pipe.JunctionSplitsCount}");
                sb.AppendLine($"  - Dimension Splits: {pipe.DimensionSplitsCount}");
                sb.AppendLine($"  - Before Overlap Resolver: {pipe.BeforeOverlapCount}");
                sb.AppendLine($"  - Suppressed Overlaps: {pipe.SuppressedOverlapsCount}");
                sb.AppendLine($"  - After Overlap Resolver: {pipe.AfterOverlapCount}");
                sb.AppendLine();
                sb.AppendLine("Revit:");
                sb.AppendLine($"  - Existing Duplicates: {rev.ExistingDuplicatesCount}");
                sb.AppendLine($"  - Created: {rev.CreatedCount}");
                sb.AppendLine($"  - Skipped: {rev.SkippedCount}");
                sb.AppendLine($"  - Failed: {rev.FailedCount}");
                sb.AppendLine();
                sb.AppendLine("Diagnostic Files:");
                sb.AppendLine($"  - JSON:    {(jsonSuccess ? jsonPath : "Not exported")}");
                sb.AppendLine($"  - CSV:     {(csvSuccess ? csvPath : "Not exported")}");
                sb.AppendLine($"  - Lineage: {(linSuccess ? linPath : "Not exported")}");
                if (session.Warnings.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Warnings ({session.Warnings.Count}):");
                    foreach (var w in session.Warnings.Take(5))
                    {
                        sb.AppendLine($"  - {w}");
                    }
                }
                sb.AppendLine("===================================");

                return sb.ToString();
            }
        }

        private string SerializeSessionToJson(BeamDiagnosticSession s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"SessionId\": \"{EscapeJson(s.SessionId)}\",");
            sb.AppendLine($"  \"StartTime\": \"{s.StartTime:o}\",");
            sb.AppendLine($"  \"EndTime\": {(s.EndTime.HasValue ? $"\"{s.EndTime.Value:o}\"" : "null")},");

            // CadSelectionSummary
            sb.AppendLine("  \"CadSelectionSummary\": {");
            sb.AppendLine($"    \"TotalEntities\": {s.CadSelectionSummary.TotalEntities},");
            sb.AppendLine($"    \"LineCount\": {s.CadSelectionSummary.LineCount},");
            sb.AppendLine($"    \"PolylineCount\": {s.CadSelectionSummary.PolylineCount},");
            sb.AppendLine($"    \"TextCount\": {s.CadSelectionSummary.TextCount},");
            sb.AppendLine($"    \"MTextCount\": {s.CadSelectionSummary.MTextCount},");
            sb.AppendLine($"    \"SkippedCount\": {s.CadSelectionSummary.SkippedCount}");
            sb.AppendLine("  },");

            // PipelineSummary
            sb.AppendLine("  \"PipelineSummary\": {");
            sb.AppendLine($"    \"RawCandidatesCount\": {s.PipelineSummary.RawCandidatesCount},");
            sb.AppendLine($"    \"ContinuityChainsCount\": {s.PipelineSummary.ContinuityChainsCount},");
            sb.AppendLine($"    \"JunctionSplitsCount\": {s.PipelineSummary.JunctionSplitsCount},");
            sb.AppendLine($"    \"DimensionSplitsCount\": {s.PipelineSummary.DimensionSplitsCount},");
            sb.AppendLine($"    \"BeforeOverlapCount\": {s.PipelineSummary.BeforeOverlapCount},");
            sb.AppendLine($"    \"SuppressedOverlapsCount\": {s.PipelineSummary.SuppressedOverlapsCount},");
            sb.AppendLine($"    \"AfterOverlapCount\": {s.PipelineSummary.AfterOverlapCount}");
            sb.AppendLine("  },");

            // RevitSummary
            sb.AppendLine("  \"RevitSummary\": {");
            sb.AppendLine($"    \"ExistingDuplicatesCount\": {s.RevitSummary.ExistingDuplicatesCount},");
            sb.AppendLine($"    \"CreatedCount\": {s.RevitSummary.CreatedCount},");
            sb.AppendLine($"    \"SkippedCount\": {s.RevitSummary.SkippedCount},");
            sb.AppendLine($"    \"FailedCount\": {s.RevitSummary.FailedCount}");
            sb.AppendLine("  },");

            // Entries
            sb.AppendLine("  \"Entries\": [");
            for (int i = 0; i < s.Entries.Count; i++)
            {
                var e = s.Entries[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"SessionId\": \"{EscapeJson(e.SessionId)}\",");
                sb.AppendLine($"      \"CandidateId\": \"{EscapeJson(e.CandidateId)}\",");
                sb.AppendLine($"      \"DiagnosticId\": \"{EscapeJson(e.DiagnosticId)}\",");
                sb.AppendLine($"      \"ObjectType\": \"{EscapeJson(e.ObjectType)}\",");
                sb.AppendLine($"      \"ParentCandidateIds\": [{string.Join(", ", (e.ParentCandidateIds ?? new List<string>()).Select(id => $"\"{EscapeJson(id)}\""))}],");
                sb.AppendLine($"      \"ParentDiagnosticIds\": [{string.Join(", ", (e.ParentDiagnosticIds ?? new List<string>()).Select(id => $"\"{EscapeJson(id)}\""))}],");
                sb.AppendLine($"      \"RootRawCandidateIds\": [{string.Join(", ", (e.RootRawCandidateIds ?? new List<string>()).Select(id => $"\"{EscapeJson(id)}\""))}],");
                sb.AppendLine($"      \"Stage\": \"{e.Stage}\",");
                sb.AppendLine($"      \"Action\": \"{e.Action}\",");
                sb.AppendLine($"      \"Reason\": \"{EscapeJson(e.Reason)}\",");
                sb.AppendLine($"      \"Timestamp\": \"{e.Timestamp:o}\",");
                sb.AppendLine($"      \"DetectionMethod\": {(e.DetectionMethod.HasValue ? $"\"{e.DetectionMethod}\"" : "null")},");
                sb.AppendLine($"      \"Confidence\": {e.Confidence.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"StartX\": {e.StartX.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"StartY\": {e.StartY.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"EndX\": {e.EndX.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"EndY\": {e.EndY.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"Length\": {e.Length.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"AngleDegrees\": {e.AngleDegrees.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"Width\": {e.Width.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"Height\": {e.Height.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"MeasuredWidth\": {e.MeasuredWidth.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"Mark\": \"{EscapeJson(e.Mark)}\",");
                sb.AppendLine($"      \"TextContent\": \"{EscapeJson(e.TextContent)}\",");
                sb.AppendLine($"      \"HasDimensionText\": {(e.HasDimensionText ? "true" : "false")},");
                sb.AppendLine($"      \"SourceLayer\": \"{EscapeJson(e.SourceLayer)}\",");
                sb.AppendLine($"      \"SourceLineIds\": [{string.Join(", ", (e.SourceLineIds ?? new List<string>()).Select(id => $"\"{EscapeJson(id)}\""))}],");
                sb.AppendLine($"      \"IsPaired\": {(e.IsPaired ? "true" : "false")},");
                sb.AppendLine($"      \"RelatedCandidateId\": \"{EscapeJson(e.RelatedCandidateId)}\",");
                sb.AppendLine($"      \"WinnerDiagnosticId\": \"{EscapeJson(e.WinnerDiagnosticId)}\",");
                sb.AppendLine($"      \"LoserDiagnosticId\": \"{EscapeJson(e.LoserDiagnosticId)}\",");
                sb.AppendLine($"      \"InputDiagnosticIds\": [{string.Join(", ", (e.InputDiagnosticIds ?? new List<string>()).Select(id => $"\"{EscapeJson(id)}\""))}],");
                sb.AppendLine($"      \"OutputDiagnosticIds\": [{string.Join(", ", (e.OutputDiagnosticIds ?? new List<string>()).Select(id => $"\"{EscapeJson(id)}\""))}],");
                sb.AppendLine($"      \"AngularDifferenceDegrees\": {e.AngularDifferenceDegrees.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"CenterlineDistanceMm\": {e.CenterlineDistanceMm.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"EndpointDistanceMm\": {e.EndpointDistanceMm.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"GapMm\": {e.GapMm.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"LateralOffsetMm\": {e.LateralOffsetMm.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"OverlapLengthMm\": {e.OverlapLengthMm.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"OverlapRatio\": {e.OverlapRatio.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"IsContained\": {(e.IsContained ? "true" : "false")},");
                sb.AppendLine($"      \"SharedSourceLineCount\": {e.SharedSourceLineCount},");
                sb.AppendLine($"      \"PriorityScore\": {e.PriorityScore.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"CompetingPriorityScore\": {e.CompetingPriorityScore.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"DimensionTextId\": \"{EscapeJson(e.DimensionTextId)}\",");
                sb.AppendLine($"      \"TextProjection\": {e.TextProjection.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"TextLateralDistance\": {e.TextLateralDistance.ToString(CultureInfo.InvariantCulture)},");
                sb.AppendLine($"      \"AssignedChainIds\": [{string.Join(", ", (e.AssignedChainIds ?? new List<string>()).Select(id => $"\"{EscapeJson(id)}\""))}],");
                sb.AppendLine($"      \"ExistingRevitElementId\": \"{EscapeJson(e.ExistingRevitElementId)}\",");
                sb.AppendLine($"      \"ExceptionType\": \"{EscapeJson(e.ExceptionType)}\",");
                sb.AppendLine($"      \"ExceptionMessage\": \"{EscapeJson(e.ExceptionMessage)}\"");
                sb.Append("    }");
                if (i < s.Entries.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string SerializeSessionToCsv(BeamDiagnosticSession s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SessionId,CandidateId,DiagnosticId,ObjectType,ParentDiagnosticIds,RootRawCandidateIds,Stage,Action,Reason,Timestamp,DetectionMethod,Confidence,StartX,StartY,EndX,EndY,Length,AngleDegrees,Width,Height,MeasuredWidth,Mark,TextContent,HasDimensionText,SourceLayer,SourceLineIds,IsPaired,RelatedCandidateId,WinnerDiagnosticId,LoserDiagnosticId,InputDiagnosticIds,OutputDiagnosticIds,AngularDifferenceDegrees,CenterlineDistanceMm,EndpointDistanceMm,GapMm,LateralOffsetMm,OverlapLengthMm,OverlapRatio,IsContained,SharedSourceLineCount,PriorityScore,CompetingPriorityScore,DimensionTextId,TextProjection,TextLateralDistance,AssignedChainIds,ExistingRevitElementId,ExceptionType,ExceptionMessage");

            foreach (var e in s.Entries)
            {
                var parentIds = e.ParentDiagnosticIds != null && e.ParentDiagnosticIds.Count > 0 ? string.Join(";", e.ParentDiagnosticIds) : (e.ParentCandidateIds != null ? string.Join(";", e.ParentCandidateIds) : "");
                var rootIds = e.RootRawCandidateIds != null ? string.Join(";", e.RootRawCandidateIds) : "";
                var sourceIds = e.SourceLineIds != null ? string.Join(";", e.SourceLineIds) : "";
                var inIds = e.InputDiagnosticIds != null ? string.Join(";", e.InputDiagnosticIds) : "";
                var outIds = e.OutputDiagnosticIds != null ? string.Join(";", e.OutputDiagnosticIds) : "";
                var assignedChains = e.AssignedChainIds != null ? string.Join(";", e.AssignedChainIds) : "";

                sb.AppendLine(string.Join(",", new[]
                {
                    EscapeCsv(e.SessionId),
                    EscapeCsv(e.CandidateId),
                    EscapeCsv(e.DiagnosticId),
                    EscapeCsv(e.ObjectType),
                    EscapeCsv(parentIds),
                    EscapeCsv(rootIds),
                    EscapeCsv(e.Stage.ToString()),
                    EscapeCsv(e.Action.ToString()),
                    EscapeCsv(e.Reason),
                    EscapeCsv(e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)),
                    EscapeCsv(e.DetectionMethod?.ToString() ?? ""),
                    e.Confidence.ToString(CultureInfo.InvariantCulture),
                    e.StartX.ToString(CultureInfo.InvariantCulture),
                    e.StartY.ToString(CultureInfo.InvariantCulture),
                    e.EndX.ToString(CultureInfo.InvariantCulture),
                    e.EndY.ToString(CultureInfo.InvariantCulture),
                    e.Length.ToString(CultureInfo.InvariantCulture),
                    e.AngleDegrees.ToString(CultureInfo.InvariantCulture),
                    e.Width.ToString(CultureInfo.InvariantCulture),
                    e.Height.ToString(CultureInfo.InvariantCulture),
                    e.MeasuredWidth.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(e.Mark),
                    EscapeCsv(e.TextContent),
                    e.HasDimensionText.ToString(),
                    EscapeCsv(e.SourceLayer),
                    EscapeCsv(sourceIds),
                    e.IsPaired.ToString(),
                    EscapeCsv(e.RelatedCandidateId),
                    EscapeCsv(e.WinnerDiagnosticId),
                    EscapeCsv(e.LoserDiagnosticId),
                    EscapeCsv(inIds),
                    EscapeCsv(outIds),
                    e.AngularDifferenceDegrees.ToString(CultureInfo.InvariantCulture),
                    e.CenterlineDistanceMm.ToString(CultureInfo.InvariantCulture),
                    e.EndpointDistanceMm.ToString(CultureInfo.InvariantCulture),
                    e.GapMm.ToString(CultureInfo.InvariantCulture),
                    e.LateralOffsetMm.ToString(CultureInfo.InvariantCulture),
                    e.OverlapLengthMm.ToString(CultureInfo.InvariantCulture),
                    e.OverlapRatio.ToString(CultureInfo.InvariantCulture),
                    e.IsContained.ToString(),
                    e.SharedSourceLineCount.ToString(),
                    e.PriorityScore.ToString(CultureInfo.InvariantCulture),
                    e.CompetingPriorityScore.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(e.DimensionTextId),
                    e.TextProjection.ToString(CultureInfo.InvariantCulture),
                    e.TextLateralDistance.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(assignedChains),
                    EscapeCsv(e.ExistingRevitElementId),
                    EscapeCsv(e.ExceptionType),
                    EscapeCsv(e.ExceptionMessage)
                }));
            }

            return sb.ToString();
        }

        private string SerializeSessionToLineageCsv(BeamDiagnosticSession s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("DiagnosticId,ObjectType,ParentDiagnosticIds,RootRawCandidateIds,Stage,Action,Reason,WinnerDiagnosticId,LoserDiagnosticId,StartX,StartY,EndX,EndY,Width,Height,Confidence");

            foreach (var e in s.Entries)
            {
                var parentIds = e.ParentDiagnosticIds != null && e.ParentDiagnosticIds.Count > 0 ? string.Join(";", e.ParentDiagnosticIds) : (e.ParentCandidateIds != null ? string.Join(";", e.ParentCandidateIds) : "");
                var rootIds = e.RootRawCandidateIds != null ? string.Join(";", e.RootRawCandidateIds) : "";

                sb.AppendLine(string.Join(",", new[]
                {
                    EscapeCsv(e.DiagnosticId ?? e.CandidateId),
                    EscapeCsv(e.ObjectType ?? e.Stage.ToString()),
                    EscapeCsv(parentIds),
                    EscapeCsv(rootIds),
                    EscapeCsv(e.Stage.ToString()),
                    EscapeCsv(e.Action.ToString()),
                    EscapeCsv(e.Reason),
                    EscapeCsv(e.WinnerDiagnosticId),
                    EscapeCsv(e.LoserDiagnosticId),
                    e.StartX.ToString(CultureInfo.InvariantCulture),
                    e.StartY.ToString(CultureInfo.InvariantCulture),
                    e.EndX.ToString(CultureInfo.InvariantCulture),
                    e.EndY.ToString(CultureInfo.InvariantCulture),
                    e.Width.ToString(CultureInfo.InvariantCulture),
                    e.Height.ToString(CultureInfo.InvariantCulture),
                    e.Confidence.ToString(CultureInfo.InvariantCulture)
                }));
            }

            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
            {
                return $"\"{s.Replace("\"", "\"\"")}\"";
            }
            return s;
        }
    }
}
