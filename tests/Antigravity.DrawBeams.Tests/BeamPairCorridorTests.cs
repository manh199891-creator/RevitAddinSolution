using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    [Collection("Sequential")]
    public class BeamPairCorridorTests
    {
        private CadLineSegment CreateLine(string id, double startX, double startY, double endX, double endY, string layer = "BEAM")
        {
            return new CadLineSegment
            {
                Id = id,
                StartPoint = new double[] { startX, startY, 0 },
                EndPoint = new double[] { endX, endY, 0 },
                Layer = layer
            };
        }

        // --- TEST 1: Current E2E Bug Case ---
        [Fact]
        public void Test1_Case_E2E_FourLines_TwoBeams_NoCrossPairing()
        {
            // Beam 1 (Top): 400x700, boundaries Y=700 (A1) and Y=300 (A2)
            var A1 = CreateLine("A1", 0, 700, 5000, 700);
            var A2 = CreateLine("A2", 0, 300, 5000, 300);

            // Beam 2 (Bottom): 250x400, boundaries Y=0 (B1) and Y=-250 (B2)
            var B1 = CreateLine("B1", 0, 0, 5000, 0);
            var B2 = CreateLine("B2", 0, -250, 5000, -250);

            var allSegments = new List<CadLineSegment> { A1, A2, B1, B2 };

            // Text 400x700 inside Beam 1 corridor (Y=500)
            dynamic text1 = new
            {
                TextString = "400x700",
                InsertionPoint = new double[] { 2500, 500, 0 },
                Rotation = 0.0
            };

            // Text 250x400 inside Beam 2 corridor (Y=-125)
            dynamic text2 = new
            {
                TextString = "250x400",
                InsertionPoint = new double[] { 2500, -125, 0 },
                Rotation = 0.0
            };

            var allTexts = new List<dynamic> { text1, text2 };
            var options = new BeamPairOptions();

            // Evaluate Cross-Pair (A2, B1)
            var crossCorridor = BeamPairCorridorService.EvaluatePairCorridor(A2, B1, allSegments, allTexts, options);

            // Cross pair A2 and B1 must be rejected!
            Assert.True(
                crossCorridor.Decision == BeamPairDecisionReason.RejectedInterveningParallelLine ||
                crossCorridor.Decision == BeamPairDecisionReason.RejectedNonMutualPair ||
                crossCorridor.Decision == BeamPairDecisionReason.RejectedWidthMismatch,
                $"Cross pair A2-B1 must be rejected. Got {crossCorridor.Decision}: {crossCorridor.DecisionReasonText}");

            // Evaluate Beam 1 pair (A1, A2)
            var beam1Corridor = BeamPairCorridorService.EvaluatePairCorridor(A1, A2, allSegments, allTexts, options);
            Assert.Equal(BeamPairDecisionReason.AcceptedCorridorText, beam1Corridor.Decision);
            Assert.Equal(400, beam1Corridor.MeasuredWidth);

            // Evaluate Beam 2 pair (B1, B2)
            var beam2Corridor = BeamPairCorridorService.EvaluatePairCorridor(B1, B2, allSegments, allTexts, options);
            Assert.Equal(BeamPairDecisionReason.AcceptedCorridorText, beam2Corridor.Decision);
            Assert.Equal(250, beam2Corridor.MeasuredWidth);
        }

        // --- TEST 2: Intervening Line ---
        [Fact]
        public void Test2_InterveningLine_RejectsFarPartner()
        {
            var lineA = CreateLine("L_A", 0, 0, 4000, 0);
            var lineB = CreateLine("L_B", 0, 200, 4000, 200); // Intervening line
            var lineC = CreateLine("L_C", 0, 500, 4000, 500); // Far partner

            var allSegments = new List<CadLineSegment> { lineA, lineB, lineC };
            var allTexts = new List<dynamic>();

            var farCorridor = BeamPairCorridorService.EvaluatePairCorridor(lineA, lineC, allSegments, allTexts);

            Assert.Equal(BeamPairDecisionReason.RejectedInterveningParallelLine, farCorridor.Decision);
            Assert.True(farCorridor.InterveningParallelLineCount >= 1);
        }

        // --- TEST 3: Mutual Nearest ---
        [Fact]
        public void Test3_MutualNearest_RejectsNonMutualFarPair()
        {
            var A = CreateLine("A", 0, 0, 3000, 0);
            var B = CreateLine("B", 0, 200, 3000, 200);
            var C = CreateLine("C", 0, 205, 3000, 205); // B and C are very close (5mm apart)

            var allSegments = new List<CadLineSegment> { A, B, C };
            var allTexts = new List<dynamic>();

            var neighbors = new Dictionary<string, List<(CadLineSegment Partner, double Distance)>>
            {
                { "A", new List<(CadLineSegment, double)> { (B, 200), (C, 205) } },
                { "B", new List<(CadLineSegment, double)> { (C, 5), (A, 200) } },
                { "C", new List<(CadLineSegment, double)> { (B, 5), (A, 205) } }
            };

            var pairAB = BeamPairCorridorService.EvaluatePairCorridor(A, B, allSegments, allTexts, nearestNeighbors: neighbors);

            // B's nearest partner is C (5mm), not A (200mm), so A+B is non-mutual nearest without text
            Assert.False(pairAB.IsMutualNearest);
            Assert.Equal(BeamPairDecisionReason.RejectedNonMutualPair, pairAB.Decision);
        }

        // --- TEST 4: Text Outside Corridor ---
        [Fact]
        public void Test4_TextOutsideCorridor_DoesNotConfirmPair()
        {
            var lineA = CreateLine("L_A", 0, 0, 4000, 0);
            var lineB = CreateLine("L_B", 0, 300, 4000, 300);

            // Text "300x500" located far outside corridor (Y=800)
            dynamic outsideText = new
            {
                TextString = "300x500",
                InsertionPoint = new double[] { 2000, 800, 0 },
                Rotation = 0.0
            };

            var (accepted, rejected) = BeamPairCorridorService.FindDimensionTextsInsidePairCorridor(lineA, lineB, new[] { outsideText });

            Assert.Empty(accepted);
            Assert.Single(rejected);
        }

        // --- TEST 5: Text Inside Corridor ---
        [Fact]
        public void Test5_TextInsideCorridor_HighConfidenceCandidate()
        {
            var lineA = CreateLine("L_A", 0, 0, 4000, 0);
            var lineB = CreateLine("L_B", 0, 400, 4000, 400);

            // Text "400x600" located inside corridor (Y=200)
            dynamic insideText = new
            {
                TextString = "400x600",
                InsertionPoint = new double[] { 2000, 200, 0 },
                Rotation = 0.0
            };

            var corridor = BeamPairCorridorService.EvaluatePairCorridor(lineA, lineB, new List<CadLineSegment> { lineA, lineB }, new[] { insideText });

            Assert.Equal(BeamPairDecisionReason.AcceptedCorridorText, corridor.Decision);
            Assert.True(corridor.HasStrongCorridorText);
            Assert.True(corridor.Score >= 1500, $"Score should be high for corridor text. Got {corridor.Score}");
        }

        // --- TEST 6: No Text But Clean Geometry ---
        [Fact]
        public void Test6_NoText_CleanGeometry_AcceptedLowerConfidence()
        {
            var lineA = CreateLine("L_A", 0, 0, 4000, 0);
            var lineB = CreateLine("L_B", 0, 300, 4000, 300);

            var corridor = BeamPairCorridorService.EvaluatePairCorridor(lineA, lineB, new List<CadLineSegment> { lineA, lineB }, new dynamic[0]);

            Assert.Equal(BeamPairDecisionReason.AcceptedMutualNearest, corridor.Decision);
            Assert.False(corridor.HasStrongCorridorText);
            Assert.True(corridor.Score > 0 && corridor.Score < 1000);
        }

        // --- TEST 7: Different Physical Beams ---
        [Fact]
        public void Test7_DifferentPhysicalBeams_PreservedWithoutCrossPairing()
        {
            // Beam 1
            var A1 = CreateLine("A1", 0, 500, 4000, 500);
            var A2 = CreateLine("A2", 0, 200, 4000, 200);

            // Beam 2
            var B1 = CreateLine("B1", 0, -100, 4000, -100);
            var B2 = CreateLine("B2", 0, -350, 4000, -350);

            var allSegments = new List<CadLineSegment> { A1, A2, B1, B2 };

            dynamic textA = new { TextString = "300x500", InsertionPoint = new double[] { 2000, 350, 0 }, Rotation = 0.0 };
            dynamic textB = new { TextString = "250x400", InsertionPoint = new double[] { 2000, -225, 0 }, Rotation = 0.0 };

            var corridorA = BeamPairCorridorService.EvaluatePairCorridor(A1, A2, allSegments, new[] { textA, textB });
            var corridorB = BeamPairCorridorService.EvaluatePairCorridor(B1, B2, allSegments, new[] { textA, textB });
            var corridorCross = BeamPairCorridorService.EvaluatePairCorridor(A2, B1, allSegments, new[] { textA, textB });

            Assert.Equal(BeamPairDecisionReason.AcceptedCorridorText, corridorA.Decision);
            Assert.Equal(BeamPairDecisionReason.AcceptedCorridorText, corridorB.Decision);
            Assert.NotEqual(BeamPairDecisionReason.AcceptedCorridorText, corridorCross.Decision);
        }

        // --- TEST 8: Shuffled Input Determinism ---
        [Fact]
        public void Test8_ShuffledInput_DeterministicPairing()
        {
            var lineA = CreateLine("L_A", 0, 0, 4000, 0);
            var lineB = CreateLine("L_B", 0, 300, 4000, 300);

            var list = new List<CadLineSegment> { lineA, lineB };
            var baseCorridor = BeamPairCorridorService.EvaluatePairCorridor(lineA, lineB, list, new dynamic[0]);

            var rng = new Random(1234);
            for (int i = 0; i < 10; i++)
            {
                var shuffled = list.OrderBy(_ => rng.Next()).ToList();
                var resultCorridor = BeamPairCorridorService.EvaluatePairCorridor(lineA, lineB, shuffled, new dynamic[0]);
                Assert.Equal(baseCorridor.Decision, resultCorridor.Decision);
                Assert.Equal(baseCorridor.Score, resultCorridor.Score);
            }
        }
    }
}
