using NUnit.Framework;
using Autodesk.Revit.DB;
using Antigravity.WallMepClash.Services;

namespace Antigravity.WallMepClash.Tests
{
    [TestFixture]
    public class BoundingBoxHelperTests
    {
        [Test]
        public void TestIntersects_True()
        {
            var boxA = new BoundingBoxXYZ
            {
                Min = new XYZ(0, 0, 0),
                Max = new XYZ(10, 10, 10)
            };

            var boxB = new BoundingBoxXYZ
            {
                Min = new XYZ(5, 5, 5),
                Max = new XYZ(15, 15, 15)
            };

            Assert.IsTrue(BoundingBoxHelper.Intersects(boxA, boxB));
        }

        [Test]
        public void TestIntersects_False()
        {
            var boxA = new BoundingBoxXYZ
            {
                Min = new XYZ(0, 0, 0),
                Max = new XYZ(10, 10, 10)
            };

            var boxB = new BoundingBoxXYZ
            {
                Min = new XYZ(11, 0, 0),
                Max = new XYZ(20, 10, 10)
            };

            Assert.IsFalse(BoundingBoxHelper.Intersects(boxA, boxB));
        }

        [Test]
        public void TestTransformToHost_Translation()
        {
            var box = new BoundingBoxXYZ
            {
                Min = new XYZ(0, 0, 0),
                Max = new XYZ(2, 2, 2)
            };

            // Dịch chuyển (10, 20, 30) bằng cách gán Origin trên Transform.Identity
            Transform transform = Transform.Identity;
            transform.Origin = new XYZ(10, 20, 30);

            BoundingBoxXYZ transformed = BoundingBoxHelper.TransformToHost(box, transform);

            Assert.AreEqual(10.0, transformed.Min.X, 0.001);
            Assert.AreEqual(20.0, transformed.Min.Y, 0.001);
            Assert.AreEqual(30.0, transformed.Min.Z, 0.001);

            Assert.AreEqual(12.0, transformed.Max.X, 0.001);
            Assert.AreEqual(22.0, transformed.Max.Y, 0.001);
            Assert.AreEqual(32.0, transformed.Max.Z, 0.001);
        }
    }
}
