using Microsoft.Build.Utilities.ProjectCreation;
using NUnit.Framework;

namespace Publicizer.Tests
{
    public class PublicizerTests : MSBuildTestBase
    {
        [Test]
        public void Build_PrivateFieldWhenPublicizeAll_ShouldSucceed()
        {
            // Arrange
            ProjectCreator project = ProjectCreator.Templates.PublicizerCsproj()
                .ItemReference(typeof(NonPublic).Assembly.Location)
                .Property(PropertyConstants.PublicizeAll, bool.TrueString)
                .ItemCompile(CsFilePaths.AccessPrivateField)
                .Save();

            // Act
            project.TryBuild(restore: true, out bool success, out BuildOutput build);

            // Assert
            Assert.IsTrue(success);
        }

        [Test]
        public void Build_PrivateFieldInPublicizedAssembly_ShouldSucceed()
        {
            // Arrange
            ProjectCreator project = ProjectCreator.Templates.PublicizerCsproj()
                .ItemReference(typeof(NonPublic).Assembly.Location)
                .ItemInclude(ItemConstants.Publicize.ItemName, "Publicizer.Tests")
                .ItemCompile(CsFilePaths.AccessPrivateField)
                .Save();

            // Act
            project.TryBuild(restore: true, out bool success, out BuildOutput build);

            // Assert
            Assert.IsTrue(success);
        }

        [Test]
        public void Build_IgnoredPrivateFieldInPublicizedAssembly_ShouldFail()
        {
            // Arrange
            ProjectCreator project = ProjectCreator.Templates.PublicizerCsproj()
                .ItemReference(typeof(NonPublic).Assembly.Location)
                .ItemInclude(ItemConstants.Publicize.ItemName, "Publicizer.Tests")
                .ItemInclude(ItemConstants.DoNotPublicize.ItemName, "Publicizer.Tests:Publicizer.Tests.NonPublic.s_privateField")
                .ItemCompile(CsFilePaths.AccessPrivateField)
                .Save();

            // Act
            project.TryBuild(restore: true, out bool success, out BuildOutput build);

            // Assert
            Assert.IsFalse(success);
        }
    }
}
