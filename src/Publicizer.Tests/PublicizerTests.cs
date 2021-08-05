using Microsoft.Build.Utilities.ProjectCreation;
using NUnit.Framework;

namespace Publicizer.Tests
{
    public class PublicizerTests : MSBuildTestBase
    {
        [Test]
        public void Build_AccessPrivateFieldWhenPublicizeAll_ShouldSucceed()
        {
            // Arrange, Act
            ProjectCreator project = ProjectCreator.Templates.PublicizerCsproj()
                .ItemReference(typeof(NonPublic).Assembly.Location)
                .ItemCompile(CsFilePaths.AccessPrivateField)
                .TryBuild(restore: true, out bool nonPublicizedCompileSuccess)
                .Property(PropertyConstants.PublicizeAll, bool.TrueString)
                .TryBuild(restore: true, out bool publicizedCompileSuccess);

            // Assert
            Assert.IsFalse(nonPublicizedCompileSuccess);
            Assert.IsTrue(publicizedCompileSuccess);
        }

        [Test]
        public void Build_AccessPrivatePublicizedField_ShouldSucceed()
        {
            // Arrange, Act
            ProjectCreator project = ProjectCreator.Templates.PublicizerCsproj()
                .ItemReference(typeof(NonPublic).Assembly.Location)
                .ItemCompile(CsFilePaths.AccessPrivateField)
                .TryBuild(restore: true, out bool nonPublicizedCompileSuccess)
                .ItemInclude(ItemConstants.Publicize.ItemName, "Publicizer.Tests:Publicizer.Tests.NonPublic.s_privateField")
                .TryBuild(restore: true, out bool publicizedCompileSuccess);

            // Assert
            Assert.IsFalse(nonPublicizedCompileSuccess);
            Assert.IsTrue(publicizedCompileSuccess);
        }
    }
}
