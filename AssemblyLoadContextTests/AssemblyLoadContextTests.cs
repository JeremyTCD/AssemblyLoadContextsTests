using JeremyTCD.DotNetCore.Utils;
using Moq;
using StubProject2;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace AssemblyLoadContextTests
{
    /// <summary>
    /// A battery of tests to determine how <see cref="AssemblyLoadContext"/> behaves. 
    /// 
    /// How <see cref="AssemblyLoadContext"/> resolves <see cref="Assembly"/>s.
    /// - Calls <see cref="AssemblyLoadContext.Load(AssemblyName)"/> on the requesting <see cref="AssemblyLoadContext"/>
    /// - If that does not resolve the assembly, checks if the assembly has been loaded by <see cref="AssemblyLoadContext.Default"/>
    /// - If it hasn't been loaded, calls <see cref="AssemblyLoadContext.Resolving"/> on the requesting <see cref="AssemblyLoadContext"/>
    /// - If that does not resovle th assembly, calls <see cref="AssemblyLoadContext.Resolving"/> on <see cref="AssemblyLoadContext.Default"/>
    /// </summary>
    public class AssemblyLoadContextTests
    {
        private MSBuildService _msBuildService { get; }
        private ProcessService _processService { get; }
        private MockRepository _mockRepository { get; }

        public AssemblyLoadContextTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Loose) { DefaultValue = DefaultValue.Mock };
            _processService = new ProcessService(_mockRepository.Create<ILoggingService<ProcessService>>().Object);
            _msBuildService = new MSBuildService(_processService, _mockRepository.Create<ILoggingService<MSBuildService>>().Object);
        }

        /// <summary>
        /// Does not allow loading of different versions of the same assembly in a context
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_DoesNotAllowLoadingOfDifferentVersionsOfTheSameAssemblyInAContext()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../");
            string projectAndAssemblyName = "StubProject1";
            string projectFile = $"{solutionDir}{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyV1Path = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts/{projectAndAssemblyName}.dll";

            _msBuildService.Build(projectFile, "/t:build /p:OutDir=artifacts2,AssemblyVersion=2.0.0.0");
            string assemblyV2Path = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts2/{projectAndAssemblyName}.dll";
            Assembly v1 = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyV1Path);

            // Act and Assert
            Assert.Throws<FileLoadException>(() => AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyV2Path));
        }

        /// <summary>
        /// Allows different versions of the same assembly to be loaded in different contexts
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_AllowsDifferentVersionsOfTheSameAssemblyToBeLoadedInDifferentContexts()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../");
            string projectAndAssemblyName = "StubProject1";
            string projectFile = $"{solutionDir}{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyV1Path = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts/{projectAndAssemblyName}.dll";
            string testVersion = "2.0.0.0";

            _msBuildService.Build(projectFile, $"/t:build /p:OutDir=bin/artifacts2,AssemblyVersion={testVersion}");
            string assemblyV2Path = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts2/{projectAndAssemblyName}.dll";
            AssemblyLoadContext loadContext = new BasicAssemblyLoadContext();

            // Act 
            Assembly v1 = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyV1Path);
            Assembly v2 = loadContext.LoadFromAssemblyPath(assemblyV2Path);

            // Assert
            Assert.Equal("1.0.0.0", v1.GetName().Version.ToString());
            Assert.Equal(testVersion, v2.GetName().Version.ToString());
        }

        /// <summary>
        /// Instances of a type from the same assembly but loaded in different contexts cannot be used interchangeably
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_InstancesOfATypeFromTheSameAssemblyInDifferentContextsCannotBeUsedInterchangeably()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../");
            string projectAndAssemblyName = "StubProject2";
            string projectFile = $"{solutionDir}{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyPath = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts/{projectAndAssemblyName}.dll";

            AssemblyLoadContext loadContext = new BasicAssemblyLoadContext();
            Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            Type stubClass2Type = assembly.GetTypes().First();

            // Act and Assert 
            Assert.Throws<InvalidCastException>(() => (StubClass2) Activator.CreateInstance(stubClass2Type));
        }

        /// <summary>
        /// An instance of a type from an assembly loaded dynamically can have its methods called using reflection
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_AnInstanceofATypeFromAnAssemblyLoadedDynamicallyCanHaveItsMethodsCalledusingReflection()
        {
            //https://github.com/dotnet/corefx/blob/c4fea4df2bbfed6df6f469ed4f9a550d561d9780/src/System.Runtime.Loader/tests/RefEmitLoadContext/RefEmitLoadContextTest.cs
        }

        [Fact]
        public void AssemblyLoadContext_StaticsOfTheSameTypeInDifferentLoadContextsAreNotShared()
        {

        }

        [Fact]
        public void AssemblyLoadContext_IfDefaultContextHasAssemblyThatACustomContextIsTryingToLoadItIsCopiedOver()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../");
            string projectAndAssemblyName = "StubProject1";
            string assemblyPath = Path.GetFullPath(solutionDir + $"{projectAndAssemblyName}/bin/artifacts/{projectAndAssemblyName}.dll");
            AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

            BasicAssemblyLoadContext loadContext = new BasicAssemblyLoadContext();

            // Act
            Assembly result = loadContext.LoadFromAssemblyPath(assemblyPath);

            // Assert
            Assert.NotNull(result);
        }

        /// <summary>
        /// If an <see cref="Assembly"/> isn't already loaded by the Default <see cref="AssemblyLoadContext"/>, does nothing.
        /// </summary>
        private class BasicAssemblyLoadContext : AssemblyLoadContext
        {
            protected override Assembly Load(AssemblyName assemblyName)
            {
                return null;
            }
        }

        /// <summary>
        /// If an <see cref="Assembly"/> isn't already loaded by the Default <see cref="AssemblyLoadContext"/>, attempts to load it
        /// from a specified directory.
        /// </summary>
        private class DirectoryAssemblyLoadContext : AssemblyLoadContext
        {
            private string _directory { get; }

            public DirectoryAssemblyLoadContext(string directory) : base()
            {
                _directory = directory;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                return null;
            }
        }
    }
}
