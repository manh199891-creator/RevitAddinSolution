using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class BeamCandidateGeneratorTests
    {
        private readonly BeamCandidateGenerator _generator = new BeamCandidateGenerator();

        [Fact]
        public void Test1_StableCandidateId_SameForReversedSegmentOrder()
        {
            var scene1 = new CadScene();
            scene1.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene1.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200 });
            scene1.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 2000, Y = 100 });

            var scene2 = new CadScene();
            scene2.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200 });
            scene2.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene2.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 2000, Y = 100 });

            var cand1 = _generator.GenerateCandidates(scene1).FirstOrDefault(c => c.Kind == BeamCandidateKind.PairedEdges);
            var cand2 = _generator.GenerateCandidates(scene2).FirstOrDefault(c => c.Kind == BeamCandidateKind.PairedEdges);

            Assert.NotNull(cand1);
            Assert.NotNull(cand2);
            Assert.Equal("PAIR:SEG_A:SEG_B:TXT1", cand1.Id);
            Assert.Equal(cand1.Id, cand2.Id);
        }

        [Fact]
        public void Test2_OneValidPair_GeneratesPairedEdgesCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0, Layer = "BEAM" });
            scene.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200, Layer = "BEAM" });
            scene.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 2000, Y = 100, Layer = "TEXT" });

            var candidates = _generator.GenerateCandidates(scene, "BEAM", "TEXT");

            var paired = candidates.FirstOrDefault(c => c.Kind == BeamCandidateKind.PairedEdges);
            Assert.NotNull(paired);
            Assert.Equal(200, paired.MeasuredWidth);
            Assert.Equal(200, paired.ParsedWidth);
            Assert.Equal(500, paired.ParsedHeight);
        }

        [Fact]
        public void Test3_KeepMultiplePartners_RetainsAllValidCandidatesForSameAnchor()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0, Layer = "BEAM_LAYER" });
            scene.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200, Layer = "BEAM_LAYER" });
            scene.Segments.Add(new CadSegment { Id = "SEG_C", StartX = 0, StartY = 300, EndX = 4000, EndY = 300, Layer = "BEAM_LAYER" });

            scene.Texts.Add(new CadText { Id = "TXT_200", TextString = "200x500", X = 2000, Y = 100, Layer = "TEXT_LAYER" });
            scene.Texts.Add(new CadText { Id = "TXT_300", TextString = "300x500", X = 2000, Y = 150, Layer = "TEXT_LAYER" });

            List<BeamCandidate> candidates = _generator.GenerateCandidates(scene, "BEAM_LAYER", "TEXT_LAYER");

            Assert.NotNull(candidates);
            bool hasPairAB = candidates.Any(c => c.Kind == BeamCandidateKind.PairedEdges &&
                ((c.MainSegment.Id == "SEG_A" && c.PartnerSegment.Id == "SEG_B") ||
                 (c.MainSegment.Id == "SEG_B" && c.PartnerSegment.Id == "SEG_A")));
            Assert.True(hasPairAB, "Candidate A+B must exist");

            bool hasPairAC = candidates.Any(c => c.Kind == BeamCandidateKind.PairedEdges &&
                ((c.MainSegment.Id == "SEG_A" && c.PartnerSegment.Id == "SEG_C") ||
                 (c.MainSegment.Id == "SEG_C" && c.PartnerSegment.Id == "SEG_A")));
            Assert.True(hasPairAC, "Candidate A+C must exist");
        }

        [Fact]
        public void Test4_ReversedPartner_GeneratesPairedEdgesCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 4000, StartY = 200, EndX = 0, EndY = 200 });
            scene.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 2000, Y = 100 });

            var candidates = _generator.GenerateCandidates(scene);
            var paired = candidates.FirstOrDefault(c => c.Kind == BeamCandidateKind.PairedEdges);

            Assert.NotNull(paired);
            Assert.Equal(200, paired.MeasuredWidth);
        }

        [Fact]
        public void Test5_NonparallelLines_DoesNotGeneratePairedEdgesCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 0, EndX = 0, EndY = 4000 });
            scene.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 1000, Y = 1000 });

            var candidates = _generator.GenerateCandidates(scene);
            bool hasPair = candidates.Any(c => c.Kind == BeamCandidateKind.PairedEdges);

            Assert.False(hasPair);
        }

        [Fact]
        public void Test6_NoOverlap_DoesNotGeneratePairedEdgesCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 2000, EndY = 0 });
            scene.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 3000, StartY = 200, EndX = 5000, EndY = 200 });
            scene.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 1000, Y = 100 });

            var candidates = _generator.GenerateCandidates(scene);
            bool hasPair = candidates.Any(c => c.Kind == BeamCandidateKind.PairedEdges);

            Assert.False(hasPair);
        }

        [Fact]
        public void Test7_WidthMismatch_DoesNotGeneratePairedEdgesCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 600, EndX = 4000, EndY = 600 });
            scene.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 2000, Y = 300 });

            var candidates = _generator.GenerateCandidates(scene);
            bool hasPair = candidates.Any(c => c.Kind == BeamCandidateKind.PairedEdges);

            Assert.False(hasPair);
        }

        [Fact]
        public void Test8_ClosedPolyline_PairsOppositeEdgesWithoutDuplication()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "PL_0", StartX = 0, StartY = 0, EndX = 5000, EndY = 0, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_1", StartX = 5000, StartY = 0, EndX = 5000, EndY = 300, GroupId = "RECT_1_pairB" });
            scene.Segments.Add(new CadSegment { Id = "PL_2", StartX = 5000, StartY = 300, EndX = 0, EndY = 300, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_3", StartX = 0, StartY = 300, EndX = 0, EndY = 0, GroupId = "RECT_1_pairB" });

            scene.Texts.Add(new CadText { Id = "TXT_RECT", TextString = "300x600 DB1", X = 2500, Y = 150 });

            var candidates = _generator.GenerateCandidates(scene);
            var groupCandidates = candidates.Where(c => c.Kind == BeamCandidateKind.ClosedPolylinePair).ToList();

            Assert.Single(groupCandidates);
            var cand = groupCandidates[0];
            Assert.Equal("PL_0", cand.MainSegment.Id);
            Assert.Equal("PL_2", cand.PartnerSegment.Id);
            Assert.Equal(300, cand.MeasuredWidth);
            Assert.Equal(300, cand.ParsedWidth);
            Assert.Equal(600, cand.ParsedHeight);
            Assert.Equal("DB1", cand.Mark);
            Assert.Equal("TXT_RECT", cand.TextMatch.Text.Id);
        }

        [Fact]
        public void ClosedPolylineWithoutValidText_DoesNotGenerateCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "PL_0", StartX = 0, StartY = 0, EndX = 5000, EndY = 0, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_1", StartX = 5000, StartY = 0, EndX = 5000, EndY = 300, GroupId = "RECT_1_pairB" });
            scene.Segments.Add(new CadSegment { Id = "PL_2", StartX = 5000, StartY = 300, EndX = 0, EndY = 300, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_3", StartX = 0, StartY = 300, EndX = 0, EndY = 0, GroupId = "RECT_1_pairB" });

            // No texts added in scene

            var candidates = _generator.GenerateCandidates(scene);
            var groupCandidates = candidates.Where(c => c.Kind == BeamCandidateKind.ClosedPolylinePair).ToList();

            Assert.Empty(groupCandidates);
        }

        [Fact]
        public void ClosedPolyline_TextNotParallel_DoesNotGenerateCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "PL_0", StartX = 0, StartY = 0, EndX = 5000, EndY = 0, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_1", StartX = 5000, StartY = 0, EndX = 5000, EndY = 300, GroupId = "RECT_1_pairB" });
            scene.Segments.Add(new CadSegment { Id = "PL_2", StartX = 5000, StartY = 300, EndX = 0, EndY = 300, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_3", StartX = 0, StartY = 300, EndX = 0, EndY = 0, GroupId = "RECT_1_pairB" });

            // Text rotated perpendicular (PI / 2)
            scene.Texts.Add(new CadText { Id = "TXT_RECT", TextString = "300x600 DB1", X = 2500, Y = 150, Rotation = System.Math.PI / 2.0 });

            var candidates = _generator.GenerateCandidates(scene);
            var groupCandidates = candidates.Where(c => c.Kind == BeamCandidateKind.ClosedPolylinePair).ToList();

            Assert.Empty(groupCandidates);
        }

        [Fact]
        public void ClosedPolyline_WidthMismatch_DoesNotGenerateCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "PL_0", StartX = 0, StartY = 0, EndX = 5000, EndY = 0, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_1", StartX = 5000, StartY = 0, EndX = 5000, EndY = 300, GroupId = "RECT_1_pairB" });
            scene.Segments.Add(new CadSegment { Id = "PL_2", StartX = 5000, StartY = 300, EndX = 0, EndY = 300, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_3", StartX = 0, StartY = 300, EndX = 0, EndY = 0, GroupId = "RECT_1_pairB" });

            // Text specifies width 600, but measured width is 300
            scene.Texts.Add(new CadText { Id = "TXT_RECT", TextString = "600x600 DB1", X = 2500, Y = 150 });

            var candidates = _generator.GenerateCandidates(scene);
            var groupCandidates = candidates.Where(c => c.Kind == BeamCandidateKind.ClosedPolylinePair).ToList();

            Assert.Empty(groupCandidates);
        }

        [Fact]
        public void FullDeterminism_ReversedInputOrder_ProducesIdenticalCandidateAndEvidence()
        {
            var scene1 = new CadScene();
            scene1.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene1.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200 });
            scene1.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 2000, Y = 100 });

            var scene2 = new CadScene();
            scene2.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200 });
            scene2.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene2.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 2000, Y = 100 });

            var cand1 = _generator.GenerateCandidates(scene1).FirstOrDefault(c => c.Kind == BeamCandidateKind.PairedEdges);
            var cand2 = _generator.GenerateCandidates(scene2).FirstOrDefault(c => c.Kind == BeamCandidateKind.PairedEdges);

            Assert.NotNull(cand1);
            Assert.NotNull(cand2);
            Assert.Equal(cand1.Id, cand2.Id);
            Assert.Equal(cand1.Kind, cand2.Kind);
            Assert.Equal("SEG_A", cand1.MainSegment.Id);
            Assert.Equal("SEG_A", cand2.MainSegment.Id);
            Assert.Equal("SEG_B", cand1.PartnerSegment.Id);
            Assert.Equal("SEG_B", cand2.PartnerSegment.Id);
            Assert.Equal(cand1.TextMatch.Text.Id, cand2.TextMatch.Text.Id);
            Assert.Equal(cand1.MeasuredWidth, cand2.MeasuredWidth, 4);
            Assert.Equal(cand1.ParsedWidth, cand2.ParsedWidth, 4);
            Assert.Equal(cand1.ParsedHeight, cand2.ParsedHeight, 4);
            Assert.Equal(cand1.OverlapLength, cand2.OverlapLength, 4);
            Assert.Equal(cand1.OverlapRatio, cand2.OverlapRatio, 4);
            Assert.Equal(cand1.AngleDifference, cand2.AngleDifference, 4);
            Assert.Equal(cand1.TextContent, cand2.TextContent);
            Assert.Equal(cand1.Mark, cand2.Mark);
        }

        [Fact]
        public void CommonWidthFallback_CanonicalOrder()
        {
            var scene1 = new CadScene();
            scene1.Segments.Add(new CadSegment { Id = "LINE_REF1", StartX = 0, StartY = 20000, EndX = 4000, EndY = 20000 });
            scene1.Segments.Add(new CadSegment { Id = "LINE_REF2", StartX = 0, StartY = 20200, EndX = 4000, EndY = 20200 });
            scene1.Texts.Add(new CadText { Id = "TXT_REF", TextString = "200x500", X = 2000, Y = 20100 });
            scene1.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200 });
            scene1.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });

            var candidates = _generator.GenerateCandidates(scene1);
            var fallbacks = candidates.Where(c => c.Kind == BeamCandidateKind.CommonWidthFallback).ToList();

            Assert.Single(fallbacks);
            Assert.Equal("SEG_A", fallbacks[0].MainSegment.Id);
            Assert.Equal("SEG_B", fallbacks[0].PartnerSegment.Id);
        }

        [Fact]
        public void ClosedPolylinePair_CanonicalOrder()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "PL_2", StartX = 5000, StartY = 300, EndX = 0, EndY = 300, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_3", StartX = 0, StartY = 300, EndX = 0, EndY = 0, GroupId = "RECT_1_pairB" });
            scene.Segments.Add(new CadSegment { Id = "PL_0", StartX = 0, StartY = 0, EndX = 5000, EndY = 0, GroupId = "RECT_1_pairA" });
            scene.Segments.Add(new CadSegment { Id = "PL_1", StartX = 5000, StartY = 0, EndX = 5000, EndY = 300, GroupId = "RECT_1_pairB" });

            scene.Texts.Add(new CadText { Id = "TXT_RECT", TextString = "300x600 DB1", X = 2500, Y = 150 });

            var candidates = _generator.GenerateCandidates(scene);
            var groupCandidates = candidates.Where(c => c.Kind == BeamCandidateKind.ClosedPolylinePair).ToList();

            Assert.Single(groupCandidates);
            Assert.Equal("PL_0", groupCandidates[0].MainSegment.Id);
            Assert.Equal("PL_2", groupCandidates[0].PartnerSegment.Id);
        }

        [Fact]
        public void Test9_PolylineWidth_GeneratesPolylineWidthCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "PLINE1", StartX = 0, StartY = 0, EndX = 5000, EndY = 0, PolylineWidth = 300 });

            var candidates = _generator.GenerateCandidates(scene);
            var plineCand = candidates.FirstOrDefault(c => c.Kind == BeamCandidateKind.PolylineWidth);

            Assert.NotNull(plineCand);
            Assert.Equal(300, plineCand.MeasuredWidth);
            Assert.Equal(300, plineCand.ParsedWidth);
        }

        [Fact]
        public void Test10_SingleLineCandidate_GeneratesSingleLineCandidate()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "LINE1", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500 B1", X = 2000, Y = 50 });

            var candidates = _generator.GenerateCandidates(scene);
            var single = candidates.FirstOrDefault(c => c.Kind == BeamCandidateKind.SingleLineWithText);

            Assert.NotNull(single);
            Assert.Equal(200, single.ParsedWidth);
            Assert.Equal(500, single.ParsedHeight);
            Assert.Equal("B1", single.Mark);
        }

        [Fact]
        public void Test11_TextLayerFiltering_ExcludesTextsOnOtherLayers()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "LINE1", StartX = 0, StartY = 0, EndX = 4000, EndY = 0, Layer = "BEAM" });
            scene.Segments.Add(new CadSegment { Id = "LINE2", StartX = 0, StartY = 200, EndX = 4000, EndY = 200, Layer = "BEAM" });

            scene.Texts.Add(new CadText { Id = "T1", TextString = "200x500", X = 2000, Y = 100, Layer = "BEAM_TEXT" });
            scene.Texts.Add(new CadText { Id = "T2", TextString = "400x800", X = 2000, Y = 100, Layer = "OTHER_TEXT" });

            var candidates = _generator.GenerateCandidates(scene, "BEAM", "BEAM_TEXT");

            Assert.All(candidates, c =>
            {
                if (c.HasText)
                {
                    Assert.Equal("T1", c.TextMatch.Text.Id);
                }
            });
        }

        [Fact]
        public void Test12_CommonWidthFallback_GeneratesFallbackCandidateWithoutDirectText()
        {
            var scene = new CadScene();

            // Text far away establishing 200 as common width for scene
            scene.Segments.Add(new CadSegment { Id = "LINE_REF1", StartX = 0, StartY = 20000, EndX = 4000, EndY = 20000 });
            scene.Segments.Add(new CadSegment { Id = "LINE_REF2", StartX = 0, StartY = 20200, EndX = 4000, EndY = 20200 });
            scene.Texts.Add(new CadText { Id = "TXT_REF", TextString = "200x500", X = 2000, Y = 20100 });

            // Anchor without nearby text
            scene.Segments.Add(new CadSegment { Id = "ANCHOR_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene.Segments.Add(new CadSegment { Id = "PARTNER_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200 });

            var candidates = _generator.GenerateCandidates(scene);
            bool hasFallback = candidates.Any(c => c.Kind == BeamCandidateKind.CommonWidthFallback &&
                ((c.MainSegment.Id == "ANCHOR_A" && c.PartnerSegment.Id == "PARTNER_B") ||
                 (c.MainSegment.Id == "PARTNER_B" && c.PartnerSegment.Id == "ANCHOR_A")));

            Assert.True(hasFallback, "CommonWidthFallback candidate must be generated");
        }

        [Fact]
        public void Test13_Deduplication_RemovesDuplicateCandidates()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200 });
            scene.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 2000, Y = 100 });

            var candidates = _generator.GenerateCandidates(scene);
            var pairedList = candidates.Where(c => c.Kind == BeamCandidateKind.PairedEdges).ToList();

            var uniqueIds = pairedList.Select(c => c.Id).Distinct().ToList();
            Assert.True(pairedList.Count == uniqueIds.Count);
        }

        [Fact]
        public void Test14_DeterministicOrdering_ReturnsSameIDsAndOrder()
        {
            var scene = new CadScene();
            scene.Segments.Add(new CadSegment { Id = "SEG_B", StartX = 0, StartY = 200, EndX = 4000, EndY = 200 });
            scene.Segments.Add(new CadSegment { Id = "SEG_A", StartX = 0, StartY = 0, EndX = 4000, EndY = 0 });
            scene.Texts.Add(new CadText { Id = "TXT1", TextString = "200x500", X = 2000, Y = 100 });

            var run1 = _generator.GenerateCandidates(scene);
            var run2 = _generator.GenerateCandidates(scene);

            Assert.Equal(run1.Count, run2.Count);
            for (int i = 0; i < run1.Count; i++)
            {
                Assert.Equal(run1[i].Id, run2[i].Id);
            }

            // Verify order is strictly sorted by Id
            var sortedIds = run1.Select(c => c.Id).OrderBy(id => id, System.StringComparer.Ordinal).ToList();
            var actualIds = run1.Select(c => c.Id).ToList();
            Assert.NotEmpty(actualIds);
            Assert.True(sortedIds.SequenceEqual(actualIds));
        }

        [Fact]
        public void Test15_InputNotMutated_PreservesOriginalCadSceneState()
        {
            var scene = new CadScene();
            var seg1 = new CadSegment { Id = "S1", StartX = 0, StartY = 0, EndX = 4000, EndY = 0, Layer = "L1", GroupId = "G1", PolylineWidth = 150 };
            var seg2 = new CadSegment { Id = "S2", StartX = 0, StartY = 200, EndX = 4000, EndY = 200, Layer = "L1", GroupId = "G1", PolylineWidth = 150 };
            var txt = new CadText { Id = "T1", TextString = "200x500", X = 2000, Y = 100, Layer = "TL1", Rotation = 0.5, TextHeight = 250 };

            scene.Segments.Add(seg1);
            scene.Segments.Add(seg2);
            scene.Texts.Add(txt);

            _generator.GenerateCandidates(scene, "L1", "TL1");

            Assert.Equal(2, scene.Segments.Count);
            Assert.Single(scene.Texts);
            Assert.Equal("S1", scene.Segments[0].Id);
            Assert.Equal(0, scene.Segments[0].StartX);
            Assert.Equal(4000, scene.Segments[0].EndX);
            Assert.Equal("L1", scene.Segments[0].Layer);
            Assert.Equal("G1", scene.Segments[0].GroupId);
            Assert.Equal(150, scene.Segments[0].PolylineWidth);

            Assert.Equal("T1", scene.Texts[0].Id);
            Assert.Equal("200x500", scene.Texts[0].TextString);
            Assert.Equal(2000, scene.Texts[0].X);
            Assert.Equal(100, scene.Texts[0].Y);
            Assert.Equal(0.5, scene.Texts[0].Rotation);
            Assert.Equal(250, scene.Texts[0].TextHeight);
        }
    }
}
