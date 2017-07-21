Note: This AssemblyLoadContextTests project has a reference to a project on my local file system. Therefore, it will not compile and is meant as a form of documentation. 

## AssemblyLoadContext Tests
A battery of tests that provide some insight into how AssemblyLoadContext (ALC) behaves. 
### What does ALC do?
ALC allows for greater flexibility when dynamically loading assemblies. This is especially useful for plugins.  

Consider the following situation:  
- Assembly A references assembly B version 2.0.0  
- Assembly C references assembly B version 1.0.0
- Assembly C attempts to load assembly A and its dependencies dynamically  
  - If Assembly C attempts to load assemblies A and B into the Default ALC, an exception will be thrown since assembly B already exists in the ALC.
  - If Assembly C attempts to load assemblies A and B into a custom ALC, it will succeed. Assembly A and B can then be utilized using reflection (see tests for example).
### ALC Notes
- Unlike AppDomains, passing objects over the boundary between two ALCs is trivial.
- [Unloading of assemblies](https://github.com/dotnet/coreclr/pull/8677) is still a work in progress. For now, this makes ALC unsuitable for
  many long running applications.
- Since only one System.Private.CoreLib can exist and CoreFX mostly just [forwards](https://docs.microsoft.com/en-us/dotnet/framework/app-domains/type-forwarding-in-the-common-language-runtime) types to it,
  loading different versions of CoreFX packages may not work as expected.
- Apart from System.Private.CoreLib, an entire framework can be loaded in an ALC. It is a good idea to load all framework assemblies, not a subset. Consider System.Runtime.dll version 4.1.1, it forwards some classes
  mscorlib.dll. mscorlib.dll version 4.0.0 in turn, forwards some classes to System.Runtime.dll. If these two assemblies are loaded at the same time, circular forwarding occurs and the program fails to run.
### ALC Additional Resources
- Basic [documentation](https://github.com/guhuro/coreclr/blob/6fb56841617d1bb45782b690b232d966353e94bc/Documentation/design-docs/assemblyloadcontext.md) from the .Net team.
- Relevant [Github issue](https://github.com/dotnet/coreclr/issues/6470).
- CoreCLR ALC [native source](https://github.com/dotnet/coreclr/blob/13e7c4368da664a8b50228b1a5ef01a660fbb2dd/src/vm/assemblynative.cpp) and [managed wrapper](https://github.com/dotnet/coreclr/blob/b38113c80d04c39890207d149bf0359a86711d62/src/mscorlib/src/System/Runtime/Loader/AssemblyLoadContext.cs).
- CoreFX System.Runtime.Loader [source](https://github.com/dotnet/corefx/tree/f8db6ae1c5534e2d0060e2fbc19465c81bee3a82/src/System.Runtime.Loader). 
  Exposes a subset of CoreCLR's ALC's members.