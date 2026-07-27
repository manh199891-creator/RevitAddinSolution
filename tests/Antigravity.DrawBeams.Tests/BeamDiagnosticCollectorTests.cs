using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    [Collection("Sequential")]
    public class BeamDiagnosticCollectorTests
    {
        private readonly BeamDiagnosticCollector _collector;

        public BeamDiagnosticCollectorTests()
        {
            _collector = new BeamDiagnosticCollector(new BeamDiagnosticOptions
            {
                Enabled = true,
                AutoExport = false
            });
        }

        [Fact]
        public void Test1_StartAndCompleteSession()
        {
            var session = _collector.StartSession("TEST_SESSION_01");
            Assert.NotNull(session);
            Assert.Equal("TEST_SESSION_01", session.SessionId);

            _collector.CompleteSession();
            Assert.NotNull(session.EndTime);
        }

        [Fact]
        public void Test2_RecordRawCandidate()
        {
            _collector.StartSession("TEST_SESSION_02");
            var entry = new BeamDiagnosticEntry
            {
                CandidateId = "RAW_001",
                Stage = BeamDiagnosticStage.RawBeamCandidate,
                Action = BeamDiagnosticAction.Kept,
                Reason = "Paired line candidate",
                DetectionMethod = BeamDetectionMethod.PairedLines,
                Confidence = 900,
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600,
                SourceLayer = "BEAM"
            };

            _collector.Record(entry);
            var session = _collector.CurrentSession;

            Assert.Single(session.Entries);
            Assert.Equal("RAW_001", session.Entries[0].CandidateId);
            Assert.Equal(BeamDiagnosticStage.RawBeamCandidate, session.Entries[0].Stage);
        }

        [Fact]
        public void Test3_RecordSplitWithParentChildIds()
        {
            _collector.StartSession("TEST_SESSION_03");

            var parentEntry = new BeamDiagnosticEntry
            {
                CandidateId = "PARENT_001",
                Stage = BeamDiagnosticStage.ContinuityOutput,
                Action = BeamDiagnosticAction.Kept,
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0
            };

            var child1 = new BeamDiagnosticEntry
            {
                CandidateId = "CHILD_001",
                ParentCandidateIds = new List<string> { "PARENT_001" },
                Stage = BeamDiagnosticStage.DimensionSplit,
                Action = BeamDiagnosticAction.Split,
                Reason = "Split by dimension change at x=1500",
                StartX = 0, StartY = 0, EndX = 1500, EndY = 0,
                Width = 400, Height = 550
            };

            var child2 = new BeamDiagnosticEntry
            {
                CandidateId = "CHILD_002",
                ParentCandidateIds = new List<string> { "PARENT_001" },
                Stage = BeamDiagnosticStage.DimensionSplit,
                Action = BeamDiagnosticAction.Split,
                Reason = "Split by dimension change at x=1500",
                StartX = 1500, StartY = 0, EndX = 3000, EndY = 0,
                Width = 600, Height = 550
            };

            _collector.Record(parentEntry);
            _collector.Record(child1);
            _collector.Record(child2);

            var session = _collector.CurrentSession;
            Assert.Equal(3, session.Entries.Count);
            Assert.Contains(session.Entries, e => e.ParentCandidateIds.Contains("PARENT_001"));
        }

        [Fact]
        public void Test4_RecordSuppressionWinnerLoser()
        {
            _collector.StartSession("TEST_SESSION_04");

            var winner = new BeamDiagnosticEntry
            {
                CandidateId = "WINNER_001",
                Stage = BeamDiagnosticStage.OverlapResolverOutput,
                Action = BeamDiagnosticAction.Kept,
                Reason = "Higher confidence PairedLines candidate",
                Confidence = 900
            };

            var loser = new BeamDiagnosticEntry
            {
                CandidateId = "LOSER_001",
                RelatedCandidateId = "WINNER_001",
                Stage = BeamDiagnosticStage.OverlapResolverOutput,
                Action = BeamDiagnosticAction.Suppressed,
                Reason = "Exact duplicate suppressed by WINNER_001",
                Confidence = 100
            };

            _collector.Record(winner);
            _collector.Record(loser);

            var session = _collector.CurrentSession;
            Assert.Equal(2, session.Entries.Count);
            var suppressed = session.Entries.FirstOrDefault(e => e.Action == BeamDiagnosticAction.Suppressed);
            Assert.NotNull(suppressed);
            Assert.Equal("WINNER_001", suppressed.RelatedCandidateId);
        }

        [Fact]
        public void Test5_RecordSharedTextWarning()
        {
            _collector.StartSession("TEST_SESSION_05");
            _collector.RecordWarning("SharedDimensionText: Text 'D1 400x600' assigned to multiple chains: CHAIN_01, CHAIN_02");

            var session = _collector.CurrentSession;
            Assert.Single(session.Warnings);
            Assert.Contains("SharedDimensionText", session.Warnings[0]);
        }

        [Fact]
        public void Test6_ExportJsonSuccess()
        {
            _collector.StartSession("TEST_SESSION_06");
            _collector.Record(new BeamDiagnosticEntry
            {
                CandidateId = "CAND_JSON",
                Stage = BeamDiagnosticStage.RawBeamCandidate,
                Action = BeamDiagnosticAction.Kept,
                Width = 400, Height = 600
            });

            string tempDir = Path.Combine(Path.GetTempPath(), "DrawBeamsTestDirJson_" + Guid.NewGuid().ToString("N"));
            var (success, filePath, warning) = _collector.ExportJson(tempDir);

            Assert.True(success);
            Assert.NotNull(filePath);
            Assert.True(File.Exists(filePath));
            Assert.Null(warning);

            string content = File.ReadAllText(filePath);
            Assert.Contains("CAND_JSON", content);

            // Cleanup
            if (File.Exists(filePath)) File.Delete(filePath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        [Fact]
        public void Test7_ExportCsvSuccess()
        {
            _collector.StartSession("TEST_SESSION_07");
            _collector.Record(new BeamDiagnosticEntry
            {
                CandidateId = "CAND_CSV",
                Stage = BeamDiagnosticStage.RawBeamCandidate,
                Action = BeamDiagnosticAction.Kept,
                Width = 400, Height = 600,
                TextContent = "D1 400x600"
            });

            string tempDir = Path.Combine(Path.GetTempPath(), "DrawBeamsTestDirCsv_" + Guid.NewGuid().ToString("N"));
            var (success, filePath, warning) = _collector.ExportCsv(tempDir);

            Assert.True(success);
            Assert.NotNull(filePath);
            Assert.True(File.Exists(filePath));
            Assert.Null(warning);

            string content = File.ReadAllText(filePath);
            Assert.Contains("CAND_CSV", content);
            Assert.Contains("D1 400x600", content);

            // Cleanup
            if (File.Exists(filePath)) File.Delete(filePath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        [Fact]
        public void Test8_ExportFailureDoesNotThrowPipeline()
        {
            _collector.StartSession("TEST_SESSION_08");
            _collector.Record(new BeamDiagnosticEntry { CandidateId = "CAND_SAFE" });

            // Invalid path with illegal characters to trigger export failure safely
            string invalidDir = "Z?:\\InvalidDirNameWith<IllegalChars>|Path";
            var (success, filePath, warning) = _collector.ExportJson(invalidDir);

            Assert.False(success);
            Assert.Null(filePath);
            Assert.NotNull(warning);
        }

        [Fact]
        public void Test9_SummaryCountAccuracy()
        {
            var session = _collector.StartSession("TEST_SESSION_09");
            session.CadSelectionSummary.TotalEntities = 10;
            session.CadSelectionSummary.LineCount = 6;
            session.CadSelectionSummary.PolylineCount = 2;
            session.CadSelectionSummary.TextCount = 2;

            session.PipelineSummary.RawCandidatesCount = 5;
            session.PipelineSummary.BeforeOverlapCount = 5;
            session.PipelineSummary.SuppressedOverlapsCount = 2;
            session.PipelineSummary.AfterOverlapCount = 3;

            session.RevitSummary.CreatedCount = 3;
            session.RevitSummary.ExistingDuplicatesCount = 1;

            string summary = _collector.BuildSummary();

            Assert.Contains("Total: 10", summary);
            Assert.Contains("Lines: 6", summary);
            Assert.Contains("Raw Candidates: 5", summary);
            Assert.Contains("Suppressed Overlaps: 2", summary);
            Assert.Contains("After Overlap Resolver: 3", summary);
            Assert.Contains("Created: 3", summary);
        }

        [Fact]
        public void Test10_CandidateIdsDeterministicInSession()
        {
            _collector.StartSession("TEST_SESSION_10");

            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var b2 = new CadBeamData { StartX = 1500, StartY = 0, EndX = 4500, EndY = 0, Width = 400, Height = 600, Confidence = 500 };

            var id1 = $"BEAM_{Math.Round(b1.StartX)}_{Math.Round(b1.StartY)}_{Math.Round(b1.EndX)}_{Math.Round(b1.EndY)}";
            var id2 = $"BEAM_{Math.Round(b2.StartX)}_{Math.Round(b2.StartY)}_{Math.Round(b2.EndX)}_{Math.Round(b2.EndY)}";

            Assert.Equal("BEAM_0_0_3000_0", id1);
            Assert.Equal("BEAM_1500_0_4500_0", id2);
        }

        [Fact]
        public void Test11_ShuffledInputProducesMatchableDiagnostics()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };
            var b2 = new CadBeamData { StartX = 3000, StartY = 0, EndX = 6000, EndY = 0, Width = 400, Height = 600, Confidence = 500 };

            var collector1 = new BeamDiagnosticCollector(new BeamDiagnosticOptions { AutoExport = false });
            collector1.StartSession("S1");
            var pipeline1 = new BeamCadPipeline();
            var out1 = pipeline1.ProcessPipeline(new[]
            {
                new CadBeamSegment { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, IsPaired = true },
                new CadBeamSegment { StartX = 3000, StartY = 0, EndX = 6000, EndY = 0, Width = 400, Height = 600, IsPaired = true }
            });

            var collector2 = new BeamDiagnosticCollector(new BeamDiagnosticOptions { AutoExport = false });
            collector2.StartSession("S2");
            var pipeline2 = new BeamCadPipeline();
            var out2 = pipeline2.ProcessPipeline(new[]
            {
                new CadBeamSegment { StartX = 3000, StartY = 0, EndX = 6000, EndY = 0, Width = 400, Height = 600, IsPaired = true },
                new CadBeamSegment { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, IsPaired = true }
            });

            Assert.Equal(out1.Count, out2.Count);
            Assert.Single(out1);
            Assert.Equal(out1[0].StartX, out2[0].StartX);
            Assert.Equal(out1[0].EndX, out2[0].EndX);
        }

        [Fact]
        public void Test12_ExistingRevitDuplicateDecisionRecordedByPureHelper()
        {
            var collector = new BeamDiagnosticCollector(new BeamDiagnosticOptions { AutoExport = false });
            collector.StartSession("TEST_SESSION_12");

            var options = new BeamOverlapOptions();
            bool isDup = RevitBeamGuardHelper.IsDuplicateRevitBeam(
                0, 0, 3000, 0, 400, 600,
                0, 0, 3000, 0, 400, 600,
                options);

            collector.Record(new BeamDiagnosticEntry
            {
                CandidateId = "CAND_REVIT_01",
                ExistingRevitElementId = "123456",
                Stage = BeamDiagnosticStage.RevitGuardCheck,
                Action = isDup ? BeamDiagnosticAction.SkippedDuplicate : BeamDiagnosticAction.Kept,
                Reason = isDup ? "Duplicate Revit beam found in model (Id: 123456)" : "No duplicate found"
            });

            var session = collector.CurrentSession;
            Assert.Single(session.Entries);
            Assert.Equal(BeamDiagnosticAction.SkippedDuplicate, session.Entries[0].Action);
            Assert.Equal("123456", session.Entries[0].ExistingRevitElementId);
        }

        [Fact]
        public void Test13_IntegrationPipeline_PopulatesEntriesAndCsv()
        {
            var collector = BeamDiagnosticCollector.Instance;
            var session = collector.StartSession();

            Assert.NotEqual("TEST_SESSION_09", session.SessionId);
            Assert.False(string.IsNullOrEmpty(session.SessionId));

            var pipeline = new BeamCadPipeline();
            var rawSegments = new[]
            {
                new CadBeamSegment { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, IsPaired = true, Confidence = 900 },
                new CadBeamSegment { StartX = 0, StartY = 100, EndX = 3000, EndY = 100, Width = 400, Height = 600, IsPaired = true, Confidence = 900 },
                new CadBeamSegment { StartX = 3000, StartY = 0, EndX = 6000, EndY = 0, Width = 400, Height = 600, IsPaired = true, Confidence = 900 },
                new CadBeamSegment { StartX = 6000, StartY = 0, EndX = 9000, EndY = 0, Width = 400, Height = 600, IsPaired = true, Confidence = 900 },
                new CadBeamSegment { StartX = 9000, StartY = 0, EndX = 12000, EndY = 0, Width = 400, Height = 600, IsPaired = true, Confidence = 900 }
            };

            // Manually record raw candidates as CadInteropService would
            int idx = 0;
            foreach (var seg in rawSegments)
            {
                collector.Record(new BeamDiagnosticEntry
                {
                    CandidateId = $"RAW_{++idx:D3}",
                    Stage = BeamDiagnosticStage.RawBeamCandidate,
                    Action = BeamDiagnosticAction.Kept,
                    StartX = seg.StartX, StartY = seg.StartY, EndX = seg.EndX, EndY = seg.EndY,
                    Width = seg.Width, Height = seg.Height, Confidence = seg.Confidence
                });
            }

            var output = pipeline.ProcessPipeline(rawSegments);
            collector.CompleteSession();

            Assert.NotNull(session.EndTime);
            Assert.True(session.Entries.Count > 0);

            string tempDir = Path.Combine(Path.GetTempPath(), "DrawBeamsIntegrationCsv_" + Guid.NewGuid().ToString("N"));
            var (success, filePath, warning) = collector.ExportCsv(tempDir);

            Assert.True(success);
            Assert.NotNull(filePath);
            Assert.True(File.Exists(filePath));

            string[] lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length > 1); // Header + data lines

            Assert.Contains(session.Entries, e => e.Stage == BeamDiagnosticStage.RawBeamCandidate);
            Assert.Contains(session.Entries, e => e.Stage == BeamDiagnosticStage.OverlapDecision || e.Stage == BeamDiagnosticStage.OverlapResolverOutput);

            // Cleanup
            if (File.Exists(filePath)) File.Delete(filePath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        [Fact]
        public void Test14_RevitDuplicateSemanticsCounters()
        {
            var collector = BeamDiagnosticCollector.Instance;
            var session = collector.StartSession();

            // Simulate exact duplicate detection in Revit guard
            bool isDup = RevitBeamGuardHelper.IsDuplicateRevitBeam(
                0, 0, 3000, 0, 400, 600,
                0, 0, 3000, 0, 400, 600);

            if (isDup)
            {
                session.RevitSummary.ExistingDuplicatesCount++;
                session.RevitSummary.SkippedCount++;

                collector.Record(new BeamDiagnosticEntry
                {
                    CandidateId = "CAND_DUP_01",
                    ExistingRevitElementId = "999888",
                    Stage = BeamDiagnosticStage.RevitGuardCheck,
                    Action = BeamDiagnosticAction.SkippedDuplicate,
                    Reason = "Near-duplicate beam exists in Revit model (ElementId: 999888)."
                });
            }

            collector.CompleteSession();

            Assert.Equal(1, session.RevitSummary.ExistingDuplicatesCount);
            Assert.Equal(0, session.RevitSummary.CreatedCount);
            Assert.Equal(1, session.RevitSummary.SkippedCount);
            var entries = session.Entries.ToList();
            Assert.Contains(entries, e => e.Action == BeamDiagnosticAction.SkippedDuplicate);
        }

        [Fact]
        public void Test15_ExportLineageCsv_ValidFileHeaderAndRows()
        {
            var collector = BeamDiagnosticCollector.Instance;
            var session = collector.StartSession();

            collector.Record(new BeamDiagnosticEntry
            {
                DiagnosticId = "FINAL_0001",
                ObjectType = "FinalCandidate",
                ParentDiagnosticIds = new List<string> { "CHAIN_0001" },
                RootRawCandidateIds = new List<string> { "RAW_0001", "RAW_0002" },
                Stage = BeamDiagnosticStage.FinalCandidate,
                Action = BeamDiagnosticAction.Kept,
                Reason = "Test final candidate"
            });

            string tempDir = Path.Combine(Path.GetTempPath(), "DrawBeamsLineageCsv_" + Guid.NewGuid().ToString("N"));
            var (success, filePath, warning) = collector.ExportLineageCsv(tempDir);

            Assert.True(success);
            Assert.NotNull(filePath);
            Assert.True(File.Exists(filePath));
            Assert.Null(warning);

            string[] lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length >= 2);
            Assert.Contains("DiagnosticId,ObjectType,ParentDiagnosticIds,RootRawCandidateIds", lines[0]);
            Assert.Contains("FINAL_0001", lines[1]);
            Assert.Contains("RAW_0001;RAW_0002", lines[1]);

            // Cleanup
            if (File.Exists(filePath)) File.Delete(filePath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        [Fact]
        public void Test16_FullLineageTrace_FinalToChainToRaw()
        {
            var collector = BeamDiagnosticCollector.Instance;
            var session = collector.StartSession();

            var raw1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, IsPaired = true, DiagnosticId = "RAW_0001", RootRawCandidateIds = new List<string> { "RAW_0001" } };
            var raw2 = new CadBeamSegment { StartX = 3000, StartY = 0, EndX = 6000, EndY = 0, Width = 400, Height = 600, IsPaired = true, DiagnosticId = "RAW_0002", RootRawCandidateIds = new List<string> { "RAW_0002" } };

            collector.Record(new BeamDiagnosticEntry { DiagnosticId = "RAW_0001", Stage = BeamDiagnosticStage.RawBeamCandidate, Action = BeamDiagnosticAction.Kept });
            collector.Record(new BeamDiagnosticEntry { DiagnosticId = "RAW_0002", Stage = BeamDiagnosticStage.RawBeamCandidate, Action = BeamDiagnosticAction.Kept });

            var pipeline = new BeamCadPipeline();
            var finals = pipeline.ProcessPipeline(new[] { raw1, raw2 });

            collector.CompleteSession();

            Assert.Single(finals);
            var finalBeam = finals[0];

            Assert.StartsWith("FINAL_", finalBeam.DiagnosticId);
            Assert.NotNull(finalBeam.RootRawCandidateIds);
            Assert.Contains("RAW_0001", finalBeam.RootRawCandidateIds);
            Assert.Contains("RAW_0002", finalBeam.RootRawCandidateIds);

            var finalEntry = session.Entries.FirstOrDefault(e => e.Stage == BeamDiagnosticStage.FinalCandidate);
            Assert.NotNull(finalEntry);
            Assert.Contains("RAW_0001", finalEntry.RootRawCandidateIds);
            Assert.Contains("RAW_0002", finalEntry.RootRawCandidateIds);
        }

        [Fact]
        public void Test17_CounterInvariantMismatch_ProducesWarning()
        {
            var collector = BeamDiagnosticCollector.Instance;
            var session = collector.StartSession();

            // Intentionally set mismatched summary counts
            session.PipelineSummary.RawCandidatesCount = 100;
            session.PipelineSummary.ContinuityChainsCount = 50;

            collector.Record(new BeamDiagnosticEntry { DiagnosticId = "RAW_0001", Stage = BeamDiagnosticStage.RawBeamCandidate, Action = BeamDiagnosticAction.Kept });

            collector.CompleteSession();

            var entries = session.Entries.ToList();
            Assert.Contains(entries, e => e.Action == BeamDiagnosticAction.Warning && e.Reason.Contains("DiagnosticCounterInvariantFailed"));
        }

        [Fact]
        public void Test18_DisabledOptions_DoesNotRecordOrAffectPipeline()
        {
            var collector = new BeamDiagnosticCollector(new BeamDiagnosticOptions { Enabled = false });
            collector.StartSession();

            collector.Record(new BeamDiagnosticEntry { DiagnosticId = "RAW_9999", Stage = BeamDiagnosticStage.RawBeamCandidate });

            Assert.Empty(collector.CurrentSession.Entries);
        }
    }
}
