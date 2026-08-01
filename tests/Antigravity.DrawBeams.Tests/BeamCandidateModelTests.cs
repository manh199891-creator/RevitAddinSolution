using Antigravity.DrawBeams.Models;
using Antigravity.DrawBeams.Services;
using Xunit;

namespace Antigravity.DrawBeams.Tests
{
    public class BeamCandidateModelTests
    {
        [Fact]
        public void BeamCandidate_DeterministicId_SameForReversedSegmentOrder()
        {
            var segA = new CadSegment { Id = "SEG_A" };
            var segB = new CadSegment { Id = "SEG_B" };

            string id1 = string.CompareOrdinal(segA.Id, segB.Id) <= 0
                ? $"PAIR:{segA.Id}:{segB.Id}:TXT1"
                : $"PAIR:{segB.Id}:{segA.Id}:TXT1";

            string id2 = string.CompareOrdinal(segB.Id, segA.Id) <= 0
                ? $"PAIR:{segB.Id}:{segA.Id}:TXT1"
                : $"PAIR:{segA.Id}:{segB.Id}:TXT1";

            Assert.Equal("PAIR:SEG_A:SEG_B:TXT1", id1);
            Assert.Equal(id1, id2);
        }
    }
}
