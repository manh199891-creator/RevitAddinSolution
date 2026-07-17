using NUnit.Framework;
using Autodesk.Revit.DB;
using Antigravity.WallMepClash.Services;

namespace Antigravity.WallMepClash.Tests
{
    [TestFixture]
    public class AngleClassifierTests
    {
        [Test]
        public void TestGetAngleDeg_Parallel()
        {
            var v1 = new XYZ(1, 0, 0);
            var v2 = new XYZ(1, 0, 0);
            double angle = AngleClassifier.GetAngleDeg(v1, v2);
            Assert.AreEqual(0.0, angle, 0.001);

            var v3 = new XYZ(-1, 0, 0);
            angle = AngleClassifier.GetAngleDeg(v1, v3);
            Assert.AreEqual(0.0, angle, 0.001); // 180 độ được chuẩn hóa về 0 độ
        }

        [Test]
        public void TestGetAngleDeg_Perpendicular()
        {
            var v1 = new XYZ(1, 0, 0);
            var v2 = new XYZ(0, 1, 0);
            double angle = AngleClassifier.GetAngleDeg(v1, v2);
            Assert.AreEqual(90.0, angle, 0.001);
        }

        [Test]
        public void TestGetAngleDeg_Skew()
        {
            var v1 = new XYZ(1, 0, 0);
            var v2 = new XYZ(1, 1, 0); // 45 độ
            double angle = AngleClassifier.GetAngleDeg(v1, v2);
            Assert.AreEqual(45.0, angle, 0.001);
        }

        [Test]
        public void TestClassify_Parallel()
        {
            var v1 = new XYZ(1, 0, 0);
            var v2 = new XYZ(0.99, 0.05, 0); // Hướng rất gần song song (~2.9 độ)
            var result = AngleClassifier.Classify(v1, v2, 10.0);
            Assert.AreEqual(AngleClassifier.AngleClass.Parallel, result);
        }

        [Test]
        public void TestClassify_Perpendicular()
        {
            var v1 = new XYZ(1, 0, 0);
            var v2 = new XYZ(0.05, 0.99, 0); // Gần vuông góc
            var result = AngleClassifier.Classify(v1, v2, 10.0);
            Assert.AreEqual(AngleClassifier.AngleClass.Perpendicular, result);
        }

        [Test]
        public void TestClassify_Skew()
        {
            var v1 = new XYZ(1, 0, 0);
            var v2 = new XYZ(1, 1, 0); // 45 độ
            var result = AngleClassifier.Classify(v1, v2, 10.0);
            Assert.AreEqual(AngleClassifier.AngleClass.Skew, result);
        }

        [Test]
        public void TestClassify_NullInput()
        {
            var result = AngleClassifier.Classify(null, new XYZ(1, 0, 0));
            Assert.AreEqual(AngleClassifier.AngleClass.Undetermined, result);
        }
    }
}
