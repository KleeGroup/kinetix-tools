using Kinetix.ComponentModel;

namespace Kinetix.ClassGenerator
{
    public static class Singletons
    {
        public static DomainManager<object> DomainManager = new DomainManager<object>(null);
        public static BeanDescriptor BeanDescriptor = new BeanDescriptor(DomainManager);
    }
}
