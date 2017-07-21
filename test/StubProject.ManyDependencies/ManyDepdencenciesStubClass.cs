using System.Reflection;

namespace StubProject.ManyDependencies
{
    public class ManyDependenciesStubClass
    {
        public TypeInfo GetTypeInfo()
        {
            return GetType().GetTypeInfo();
        }
    }
}
