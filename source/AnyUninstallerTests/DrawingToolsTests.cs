using System.Drawing;
using System.IO;
using Klocman.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnyUninstallerTests
{
    [TestClass]
    public class DrawingToolsTests
    {
        [TestMethod]
        public void CreateOwnedIconFromHandle_ReturnsUsableClone()
        {
            using var sourceIcon = SystemIcons.Application;
            var handle = sourceIcon.Handle;

            using var ownedIcon = DrawingTools.CreateOwnedIconFromHandle(handle);
            using var stream = new MemoryStream();

            ownedIcon.Save(stream);

            Assert.IsTrue(stream.Length > 0);
            Assert.AreEqual(sourceIcon.Size, ownedIcon.Size);
        }
    }
}
