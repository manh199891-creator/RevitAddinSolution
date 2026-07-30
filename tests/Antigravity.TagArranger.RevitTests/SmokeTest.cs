using System;
using Autodesk.Revit.UI;
using NUnit.Framework;

namespace Antigravity.TagArranger.RevitTests
{
    [TestFixture]
    public class SmokeTests
    {
        [Test]
        public void Application_IsNotNull(UIApplication app)
        {
            Assert.IsNotNull(app);
            Assert.IsNotNull(app.Application);
            Console.WriteLine("Smoke test passed! Revit UIApplication is available.");
        }
    }
}
