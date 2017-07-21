using JeremyTCD.DotNetCore.Utils;
using Moq;
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
        private string _tempDir { get; } = Path.Combine(Path.GetTempPath(), $"{nameof(AssemblyLoadContextTests)}Temp");

        public AssemblyLoadContextTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Loose) { DefaultValue = DefaultValue.Mock };
            _processService = new ProcessService(_mockRepository.Create<ILoggingService<ProcessService>>().Object);
            _msBuildService = new MSBuildService(_processService, _mockRepository.Create<ILoggingService<MSBuildService>>().Object);
        }

        /// <summary>
        /// Does not allow loading of different versions of an assembly into context - throws an exception, doesn't
        /// overwrite initial load.
        /// 
        /// No two assemblies with the same simple name can be loaded in a context.
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_DoesNotAllowLoadingOfDifferentVersionsOfTheSameAssemblyInAContext()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../..");
            string projectAndAssemblyName = "StubProject.Referencee";
            string projectFile = $"{solutionDir}/test/{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyV1Path = $"{solutionDir}/test/{projectAndAssemblyName}/bin/debug/netstandard1.1/{projectAndAssemblyName}.dll";

            _msBuildService.Build(projectFile, $"/t:build /p:OutDir={_tempDir},AssemblyVersion=2.0.0.0");  // TODO netstandard2.0 and earlier, AssemblyLoadContexts cannot be unloaded
            string assemblyV2Path = $"{_tempDir}/{projectAndAssemblyName}.dll";

            // Act and Assert
            Assembly assemblyV1 = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyV1Path);
            Assert.Equal("1.0.0.0", assemblyV1.GetName().Version.ToString());
            Assert.Throws<FileLoadException>(() => AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyV2Path));
        }

        /// <summary>
        /// Allows different versions of an assembly to be loaded into different contexts. 
        /// 
        /// It follows that the same version of an assembly can be loaded into different contexts. This allows a host to use 
        /// AssemblyLoadContexts to load plugins that reference different versions of third party libraries and frameworks.
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_AllowsDifferentVersionsOfTheSameAssemblyToBeLoadedInDifferentContexts()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../..");
            string projectAndAssemblyName = "StubProject.Referencee";
            string projectFile = $"{solutionDir}/test/{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyV1Path = $"{solutionDir}/test/{projectAndAssemblyName}/bin/debug/netstandard1.1/{projectAndAssemblyName}.dll";
            string testVersion = "2.0.0.0";

            _msBuildService.Build(projectFile, $"/t:build /p:OutDir={_tempDir},AssemblyVersion=2.0.0.0");  // TODO netstandard2.0 and earlier, AssemblyLoadContexts cannot be unloaded
            string assemblyV2Path = $"{_tempDir}/{projectAndAssemblyName}.dll";
            AssemblyLoadContext loadContext = new BasicAssemblyLoadContext();

            // Act 
            Assembly assemblyV1 = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyV1Path);
            Assembly assemblyV2 = loadContext.LoadFromAssemblyPath(assemblyV2Path);

            // Assert
            Assert.Equal("1.0.0.0", assemblyV1.GetName().Version.ToString());
            Assert.Equal(testVersion, assemblyV2.GetName().Version.ToString());
        }

        /// <summary>
        /// Instances of a type from the same assembly but different contexts cannot be used interchangeably
        /// 
        /// The standard way of passing data across context boundaries is through marshalling.
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_InstancesOfATypeFromTheSameAssemblyInDifferentContextsCannotBeUsedInterchangeably()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../..");
            string projectAndAssemblyName = "StubProject.Referencee";
            string projectFile = $"{solutionDir}/test/{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyPath = $"{solutionDir}/test/{projectAndAssemblyName}/bin/debug/netstandard1.1/{projectAndAssemblyName}.dll";

            Assembly defaultALCAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            Type defaultStubClass = defaultALCAssembly.GetTypes().First();

            AssemblyLoadContext customContext = new BasicAssemblyLoadContext();
            Assembly customALCAssembly = customContext.LoadFromAssemblyPath(assemblyPath);
            Type customStubClass = customALCAssembly.GetTypes().First();

            // Act and Assert 
            Assert.Throws<InvalidCastException>(() => Convert.ChangeType(Activator.CreateInstance(customStubClass), defaultStubClass));
        }

        /// <summary>
        /// An instance of a type from a dynamically loaded assembly can have its methods called using reflection.
        /// 
        /// This is the standard way of interacting with assemblies loaded in a custom context. Primitives can be passed
        /// across context boundaries. Therefore, non primitives must be marshalled.
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_AnInstanceofATypeFromAnAssemblyLoadedDynamicallyCanHaveItsMethodsCalledUsingReflection()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../..");
            string projectAndAssemblyName = "StubProject.InstanceMethod";
            string projectFile = $"{solutionDir}/test/{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyPath = $"{solutionDir}/test/{projectAndAssemblyName}/bin/debug/netstandard2.0/{projectAndAssemblyName}.dll";
            string testString = "testString";

            AssemblyLoadContext loadContext = new BasicAssemblyLoadContext();
            Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            Type instanceMethodStubClass = assembly.GetTypes().First();
            object instanceMethodStubClassInstance = Activator.CreateInstance(instanceMethodStubClass);
            MethodInfo method = instanceMethodStubClass.GetMethod("GetString", BindingFlags.Instance | BindingFlags.Public);

            // Act 
            String result = (string)method.Invoke(instanceMethodStubClassInstance, new object[] { testString });

            // Assert
            Assert.Equal(testString, result);
        }

        /// <summary>
        /// Statics of the same type in different load contexts are not shared
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_StaticsOfTheSameTypeInDifferentLoadContextsAreNotShared()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../..");
            string projectAndAssemblyName = "StubProject.Statics";
            string projectFile = $"{solutionDir}/test/{projectAndAssemblyName}/{projectAndAssemblyName}.csproj";
            string assemblyPath = $"{solutionDir}/test/{projectAndAssemblyName}/bin/debug/netstandard2.0/{projectAndAssemblyName}.dll";

            Assembly defaultALCAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            Type defaultStubClass = defaultALCAssembly.GetTypes().First();
            FieldInfo defaultField = defaultStubClass.GetField("StaticField");
            object defaultInstance = Activator.CreateInstance(defaultStubClass);

            AssemblyLoadContext customContext = new BasicAssemblyLoadContext();
            Assembly customALCAssembly = customContext.LoadFromAssemblyPath(assemblyPath);
            Type customStubClass = customALCAssembly.GetTypes().First();
            FieldInfo customField = customStubClass.GetField("StaticField");
            object customInstance = Activator.CreateInstance(customStubClass);

            // Act 
            defaultField.SetValue(defaultInstance, 1);
            customField.SetValue(customInstance, 2);

            // Assert
            Assert.Equal(1, defaultField.GetValue(defaultInstance));
        }

        /// <summary>
        /// If custom context's Load returns null and default context has different version of the same assembly, assembly from default context is bound
        /// 
        /// E.g plugin targets netstandard1.0 while host application targets netcoreapp2.0. When loading plugin, System.Runtime assembly
        /// from default context can be bound instead. 
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_IfCustomContextsLoadReturnsNullAndDefaultContextHasDifferentVersionOfTheSameAssemblyAssemblyFromDefaultContextIsBoundd()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../..");
            string projectName = "StubProject.OlderFramework";
            string assemblyPath = $"{solutionDir}/test/{projectName}/bin/debug/netstandard1.0/{projectName}.dll";

            BasicAssemblyLoadContext loadContext = new BasicAssemblyLoadContext();

            // Act
            Assembly projectAssembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            Type olderFrameworkStubClass = projectAssembly.GetTypes().First();
            object olderFrameworkStubClassInstance = Activator.CreateInstance(olderFrameworkStubClass);
            MethodInfo getString = olderFrameworkStubClass.GetMethod("GetString", BindingFlags.Instance | BindingFlags.Public);
            object result = getString.Invoke(olderFrameworkStubClassInstance, new object[]{ });

            // Assert
            Assert.NotNull(result); // Older framework project references an older system.runtime version. Newer version from host was bound instead.
        }

        /// <summary>
        /// If custom context's Load returns null and default context has assembly, assembly from default context is bound
        /// </summary>
        [Fact]
        public void AssemblyLoadContext_IfCustomContextsLoadReturnsNullAndDefaultContextHasAssemblyAssemblyFromDefaultContextIsBound()
        {
            // Arrange
            string solutionDir = Path.GetFullPath(typeof(AssemblyLoadContextTests).GetTypeInfo().Assembly.Location + "../../../../../../..");
            string referenceeProjectName = "StubProject.Referencee";
            string referenceeAssemblyPath = $"{solutionDir}/test/{referenceeProjectName}/bin/debug/netstandard1.1/{referenceeProjectName}.dll";
            string referencerProjectName = "StubProject.Referencer";
            string referencerAssemblyPath = $"{solutionDir}/test/{referencerProjectName}/bin/debug/netstandard1.1/{referencerProjectName}.dll";

            AssemblyLoadContext.Default.LoadFromAssemblyPath(referenceeAssemblyPath);

            BasicAssemblyLoadContext loadContext = new BasicAssemblyLoadContext();

            // Act
            Assembly referencerAssembly = loadContext.LoadFromAssemblyPath(referencerAssemblyPath);
            Type type = referencerAssembly.GetTypes().First();
            object test = Activator.CreateInstance(type);
            MethodInfo method = type.GetMethod("CreateReferenceeStubClassInstance", BindingFlags.Instance | BindingFlags.Public);
            object referenceeStubInstance = method.Invoke(test, new object[] { });
            Assembly referenceeAssembly = referenceeStubInstance.GetType().GetTypeInfo().Assembly;

            // Assert
            Assert.NotNull(referencerAssembly);
            AssemblyLoadContext referencerLoadContext = AssemblyLoadContext.GetLoadContext(referencerAssembly);
            Assert.Equal(loadContext, referencerLoadContext);
            AssemblyLoadContext referenceeLoadContext = AssemblyLoadContext.GetLoadContext(referenceeAssembly);
            Assert.Equal(AssemblyLoadContext.Default, referenceeLoadContext);
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