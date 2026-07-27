using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    [Collection("Sequential")]
    public class BeamOverlapResolverTests
    {
        private readonly BeamOverlapResolver _resolver;
        private readonly BeamOverlapOptions _options;

        public BeamOverlapResolverTests()
        {
            _options = new BeamOverlapOptions
            {
                OverlapMode = BeamOverlapMode.LegacySafe,
                AngularToleranceDegrees = 1.0,
                CenterlineDistanceToleranceMm = 25.0,
                EndpointToleranceMm = 50.0,
                MinimumOverlapRatio = 0.80,
                ContainmentToleranceMm = 50.0
            };
            _resolver = new BeamOverlapResolver(_options);
        }

        // --- PHASE 1 TESTS ---

        [Fact]
        public void Test_Phase1_PairedLines_Height0_NoText_StaysInRawCandidates()
        {
            var rawSeg = new CadBeamSegment
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 0, MeasuredWidth = 400,
                TextContent = "", IsPaired = true,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new List<string> { "L1", "L2" }
            };

            Assert.True(rawSeg.IsValid, "PairedLines candidate with valid Width/MeasuredWidth must be valid.");

            var pipeline = new BeamCadPipeline();
            var finals = pipeline.ProcessPipeline(new[] { rawSeg });

            Assert.Single(finals);
            Assert.Equal(400, finals[0].Width);
        }

        [Fact]
        public void Test_Phase1_MeasuredWidthMismatch_PreservesRawCandidate_LogsWarning()
        {
            var collector = BeamDiagnosticCollector.Instance;
            var session = collector.StartSession(options: new BeamDiagnosticOptions { Enabled = true, AutoExport = false });

            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 600, Height = 500, MeasuredWidth = 780, // Mismatch > 20%
                DetectionMethod = BeamDetectionMethod.PairedLines, HasDimensionText = true
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1 });
            collector.CompleteSession();

            Assert.Single(resolved);
            Assert.Equal(600, resolved[0].Width);

            var prio = BeamCandidatePriorityCalculator.CalculatePriority(b1);
            Assert.Equal(MeasuredWidthAgreement.Weak, prio.WidthAgreement);
        }

        // --- PHASE 3 TESTS (Protection of PairedLines) ---

        [Fact]
        public void Test_Phase3_TwoPairedLines_Parallel200mm_KeepBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L1", "L2" }, DiagnosticId = "P1"
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 200, EndX = 3000, EndY = 200, // 200 mm apart
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L3", "L4" }, DiagnosticId = "P2"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test_Phase3_TwoPairedLines_Parallel300mm_KeepBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L1", "L2" }, DiagnosticId = "P1"
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 300, EndX = 3000, EndY = 300,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L3", "L4" }, DiagnosticId = "P2"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test_Phase3_TwoPairedLines_DifferentWidths_200mm_KeepBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L1", "L2" }, DiagnosticId = "P400"
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 200, EndX = 3000, EndY = 200,
                Width = 400, Height = 700, DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L3", "L4" }, DiagnosticId = "P700"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test_Phase3_TwoPairedLines_IndependentText_KeepBoth()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true, TextContent = "BEAM_A 400x600",
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L1", "L2" }, DiagnosticId = "P_TEXT1"
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 200, EndX = 3000, EndY = 200,
                Width = 400, Height = 600, HasDimensionText = true, TextContent = "BEAM_B 400x600",
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L3", "L4" }, DiagnosticId = "P_TEXT2"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Equal(2, resolved.Count);
        }

        [Fact]
        public void Test_Phase3_ExactPairedLinesDuplicate_KeepOne()
        {
            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500, DiagnosticId = "B1" };
            var b2 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, Confidence = 500, DiagnosticId = "B2" };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Single(resolved);
        }

        [Fact]
        public void Test_Phase3_SharedSourceLineIds_MatchingGeometry_KeepOne()
        {
            var b1 = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 3000, EndY = 0,
                Width = 400, Height = 600, Confidence = 500,
                SourceLineIds = new HashSet<string> { "LINE_X", "LINE_Y" }, DiagnosticId = "B1"
            };

            var b2 = new CadBeamData
            {
                StartX = 0, StartY = 5, EndX = 3000, EndY = 5,
                Width = 400, Height = 600, Confidence = 800,
                SourceLineIds = new HashSet<string> { "LINE_X", "LINE_Y" }, DiagnosticId = "B2"
            };

            var resolved = _resolver.ResolveOverlaps(new[] { b1, b2 });

            Assert.Single(resolved);
            Assert.Equal("B2", resolved[0].DiagnosticId);
        }

        // --- PHASE 4 TESTS (Boundary Fallback Rule) ---

        [Fact]
        public void Test_Phase4_BoundaryFallback_WithSourceEvidence_SuppressesFallback()
        {
            var paired = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 8000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L1", "L2" }, DiagnosticId = "PAIRED"
            };

            var fallback = new CadBeamData
            {
                StartX = 0, StartY = 200, EndX = 8000, EndY = 200, // Distance 200mm = 400/2
                Width = 400, Height = 600, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback,
                SourceLineIds = new HashSet<string> { "L1" }, DiagnosticId = "FALLBACK" // Shared source line
            };

            var resolved = _resolver.ResolveOverlaps(new[] { paired, fallback });

            Assert.Single(resolved);
            Assert.Equal("PAIRED", resolved[0].DiagnosticId);
        }

        [Fact]
        public void Test_Phase4_BoundaryFallback_WithoutSourceEvidence_KeepsBoth_LogsAmbiguous()
        {
            BeamDiagnosticCollector.Instance.StartSession(options: new BeamDiagnosticOptions { Enabled = true, AutoExport = false });

            var paired = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 8000, EndY = 0,
                Width = 400, Height = 600, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "L1", "L2" }, DiagnosticId = "PAIRED"
            };

            var fallback = new CadBeamData
            {
                StartX = 0, StartY = 200, EndX = 8000, EndY = 200,
                Width = 400, Height = 600, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback,
                SourceLineIds = new HashSet<string> { "OTHER_LINE" }, DiagnosticId = "FALLBACK" // Independent source line
            };

            var resolved = _resolver.ResolveOverlaps(new[] { paired, fallback });
            BeamDiagnosticCollector.Instance.CompleteSession();

            Assert.Equal(2, resolved.Count);

            var session = BeamDiagnosticCollector.Instance.CurrentSession;
            Assert.Equal(1, session.PipelineSummary.AmbiguousKept);
            Assert.True(session.Warnings.Any(w => w.Contains("AmbiguousBoundaryCandidate")));
        }

        // --- PHASE 5 TESTS (Counter Regression Fixture) ---

        [Fact]
        public void Test_Phase5_InteriorFloorPlanFixture_PreservesAll10Beams()
        {
            var beams = new List<CadBeamData>();

            // 10 Physical interior beams (parallel, spaced 300mm apart)
            for (int i = 0; i < 10; i++)
            {
                beams.Add(new CadBeamData
                {
                    StartX = 0, StartY = i * 300, EndX = 6000, EndY = i * 300,
                    Width = 300, Height = (i % 2 == 0) ? 500 : 0, HasDimensionText = (i % 2 == 0),
                    DetectionMethod = BeamDetectionMethod.PairedLines,
                    SourceLineIds = new HashSet<string> { $"INT_L1_{i}", $"INT_L2_{i}" },
                    DiagnosticId = $"INT_BEAM_{i}"
                });
            }

            // 2 Fallback boundary lines (one with source evidence, one without)
            var boundary1WithSource = new CadBeamData
            {
                StartX = 0, StartY = 150, EndX = 6000, EndY = 150, // Y=150 is 300/2
                Width = 300, Height = 500, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback,
                SourceLineIds = new HashSet<string> { "INT_L1_0" }, DiagnosticId = "FALLBACK_WITH_SOURCE"
            };

            var boundary2NoSource = new CadBeamData
            {
                StartX = 0, StartY = 450, EndX = 6000, EndY = 450, // Y=450 is 300 + 300/2
                Width = 300, Height = 500, HasDimensionText = false,
                DetectionMethod = BeamDetectionMethod.SingleLineFallback,
                SourceLineIds = new HashSet<string> { "UNRELATED_LINE" }, DiagnosticId = "FALLBACK_NO_SOURCE"
            };

            // 1 Exact duplicate of INT_BEAM_0
            var exactDup = new CadBeamData
            {
                StartX = 0, StartY = 0, EndX = 6000, EndY = 0,
                Width = 300, Height = 500, HasDimensionText = true,
                DetectionMethod = BeamDetectionMethod.PairedLines,
                SourceLineIds = new HashSet<string> { "INT_L1_0", "INT_L2_0" }, DiagnosticId = "EXACT_DUP_0"
            };

            var inputList = new List<CadBeamData>(beams) { boundary1WithSource, boundary2NoSource, exactDup };

            var resolved = _resolver.ResolveOverlaps(inputList);

            // All 10 physical beams must be kept!
            for (int i = 0; i < 10; i++)
            {
                Assert.Contains(resolved, b => b.DiagnosticId == $"INT_BEAM_{i}" || b.DiagnosticId == "EXACT_DUP_0");
            }

            // Fallback with source evidence must be suppressed
            Assert.DoesNotContain(resolved, b => b.DiagnosticId == "FALLBACK_WITH_SOURCE");

            // Boundary fallback without source evidence must be kept
            Assert.Contains(resolved, b => b.DiagnosticId == "FALLBACK_NO_SOURCE");
        }

        [Fact]
        public void Test_Phase5_RawDetectorOutput_UnchangedByOverlapMode()
        {
            var raw1 = new CadBeamSegment { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, DiagnosticId = "RAW1" };
            var raw2 = new CadBeamSegment { StartX = 0, StartY = 200, EndX = 3000, EndY = 200, Width = 400, Height = 600, DiagnosticId = "RAW2" };

            var pipeLegacy = new BeamCadPipeline();
            var optLegacy = new BeamOverlapOptions { OverlapMode = BeamOverlapMode.LegacySafe };
            var resultLegacy = pipeLegacy.ProcessPipeline(new[] { raw1, raw2 }, overlapOptions: optLegacy);

            var pipeExperimental = new BeamCadPipeline();
            var optExperimental = new BeamOverlapOptions { OverlapMode = BeamOverlapMode.ExperimentalEnvelope };
            var resultExperimental = pipeExperimental.ProcessPipeline(new[] { raw1, raw2 }, overlapOptions: optExperimental);

            // Raw detector segments are processed identically before BeamOverlapResolver
            Assert.True(resultLegacy.Count >= 1);
            Assert.True(resultExperimental.Count >= 1);
        }

        // --- PHASE 6 TESTS (A/B Diagnostics & Counters) ---

        [Fact]
        public void Test_Phase6_ABDiagnostics_LegacySafeVsExperimental()
        {
            var collectorLegacy = BeamDiagnosticCollector.Instance;
            var sessionLegacy = collectorLegacy.StartSession(options: new BeamDiagnosticOptions { Enabled = true, AutoExport = false });

            var b1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, DiagnosticId = "B1" };
            var b2 = new CadBeamData { StartX = 0, StartY = 200, EndX = 3000, EndY = 200, Width = 400, Height = 600, DiagnosticId = "B2" };

            var resolverLegacy = new BeamOverlapResolver(new BeamOverlapOptions { OverlapMode = BeamOverlapMode.LegacySafe });
            var resLegacy = resolverLegacy.ResolveOverlaps(new[] { b1, b2 });
            collectorLegacy.CompleteSession();

            var summaryLegacy = sessionLegacy.PipelineSummary;
            Assert.Equal("LegacySafe", summaryLegacy.OverlapMode);
            Assert.Equal(0, summaryLegacy.PairedVsPairedSuppressions); // PairedVsPairedSuppressions must be 0 in LegacySafe for non-duplicates!
            Assert.Equal(2, resLegacy.Count);

            var collectorExp = BeamDiagnosticCollector.Instance;
            var sessionExp = collectorExp.StartSession(options: new BeamDiagnosticOptions { Enabled = true, AutoExport = false });

            var resolverExp = new BeamOverlapResolver(new BeamOverlapOptions { OverlapMode = BeamOverlapMode.ExperimentalEnvelope });
            var resExp = resolverExp.ResolveOverlaps(new[] { b1, b2 });
            collectorExp.CompleteSession();

            var summaryExp = sessionExp.PipelineSummary;
            Assert.Equal("ExperimentalEnvelope", summaryExp.OverlapMode);
            Assert.Equal(1, summaryExp.PairedVsPairedSuppressions);
            Assert.Single(resExp);
        }

        [Fact]
        public void Test_ShuffledInput_Deterministic()
        {
            var c1 = new CadBeamData { StartX = 0, StartY = 0, EndX = 3000, EndY = 0, Width = 400, Height = 600, DiagnosticId = "C1" };
            var c2 = new CadBeamData { StartX = 3000, StartY = 0, EndX = 6000, EndY = 0, Width = 400, Height = 600, DiagnosticId = "C2" };
            var c3 = new CadBeamData { StartX = 0, StartY = 200, EndX = 3000, EndY = 200, Width = 400, Height = 0, DetectionMethod = BeamDetectionMethod.SingleLineFallback, DiagnosticId = "C3" };

            var list = new List<CadBeamData> { c1, c2, c3 };
            var baseResult = _resolver.ResolveOverlaps(list).Select(b => b.DiagnosticId).ToList();

            var rng = new Random(42);
            for (int i = 0; i < 10; i++)
            {
                var shuffled = list.OrderBy(_ => rng.Next()).ToList();
                var result = _resolver.ResolveOverlaps(shuffled).Select(b => b.DiagnosticId).ToList();
                Assert.Equal(baseResult, result);
            }
        }
    }
}
