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
    /// A battery of tests that provide some insight into how <see cref="AssemblyLoadContext"/> behaves. 
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
        /// Does not allow loading of different versions of an assembly into context - throws an exception, doesn't
        /// overwrite initial load.
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_DoesNotAllowLoadingOfDifferentVersionsOfTheSameAssemblyInAContext()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../");
            string projectAndAssemblyName = "StubProject2";
            string projectFile = $"{solutionDir}{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyV1Path = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts/{projectAndAssemblyName}.dll";

            _msBuildService.Build(projectFile, "/t:build /p:OutDir=bin/artifacts2,AssemblyVersion=2.0.0.0");
            string assemblyV2Path = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts2/{projectAndAssemblyName}.dll";

            // Act and Assert
            Assembly assemblyV1 = Assembly.Load(projectAndAssemblyName);
            Assert.Equal("1.0.0.0", assemblyV1.GetName().Version.ToString());
            Assert.Throws<FileLoadException>(() => AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyV2Path));
        }

        /// <summary>
        /// Allows different versions of an assembly to be loaded into different contexts. It can be inferred that the same version 
        /// of an assembly can be loaded into different contexts.
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_AllowsDifferentVersionsOfTheSameAssemblyToBeLoadedInDifferentContexts()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../");
            string projectAndAssemblyName = "StubProject2";
            string projectFile = $"{solutionDir}{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string testVersion = "2.0.0.0";

            _msBuildService.Build(projectFile, $"/t:build /p:OutDir=bin/artifacts2,AssemblyVersion={testVersion}");
            string assemblyV2Path = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts2/{projectAndAssemblyName}.dll";
            AssemblyLoadContext loadContext = new BasicAssemblyLoadContext();

            // Act 
            Assembly assemblyV1 = Assembly.Load(projectAndAssemblyName);
            Assembly assemblyV2 = loadContext.LoadFromAssemblyPath(assemblyV2Path);

            // Assert
            Assert.Equal("1.0.0.0", assemblyV1.GetName().Version.ToString());
            Assert.Equal(testVersion, assemblyV2.GetName().Version.ToString());
        }

        /// <summary>
        /// Instances of a type from the same assembly but from different contexts cannot be used interchangeably
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
        /// An instance of a type from a dynamically loaded assembly can have its methods called using reflection
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_AnInstanceofATypeFromAnAssemblyLoadedDynamicallyCanHaveItsMethodsCalledUsingReflection()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../");
            string projectAndAssemblyName = "StubProject1";
            string projectFile = $"{solutionDir}{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyPath = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts/{projectAndAssemblyName}.dll";
            string testMessage = "testMessage";

            AssemblyLoadContext loadContext = new BasicAssemblyLoadContext();
            Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            Type stubClass1Type = assembly.GetTypes().First();
            object stubClass1Instance = Activator.CreateInstance(stubClass1Type);

            // Act 
            MethodInfo method = TypeExtensions.GetMethod(stubClass1Type, "ReturnString");
            String result = (string)method.Invoke(stubClass1Instance, new object[] { testMessage });

            // Assert
            Assert.Equal(testMessage, result);
        }

        /// <summary>
        /// Statics of the same type in different load contexts are not shared
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_StaticsOfTheSameTypeInDifferentLoadContextsAreNotShared()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../");
            string projectAndAssemblyName = "StubProject2";
            string projectFile = $"{solutionDir}{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyPath = $"{solutionDir}{projectAndAssemblyName}/bin/artifacts/{projectAndAssemblyName}.dll";

            AssemblyLoadContext loadContext = new BasicAssemblyLoadContext();

            // Act 
            StubClass2.StubStaticField2 = 1;
            Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            Type type = assembly.GetTypes().First();
            FieldInfo field = type.GetField(nameof(StubClass2.StubStaticField2));
            field.SetValue(null, 2);

            // Assert
            Assert.Equal(1, StubClass2.StubStaticField2);
            Assert.Equal(2, field.GetValue(null));
        }

        /// <summary>
        /// If default context has an assembly that a custom context is trying to load it is copied over - instead of being reloaded
        /// </summary>
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
    }
}
