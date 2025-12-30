using System;

namespace Huxley2.Security
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RequireApiKeyAttribute : Attribute { }
}
