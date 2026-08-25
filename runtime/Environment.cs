//using Java.Util;
namespace Org.Puffinbasic.Runtime
{
    using System;
    using System.Collections.Generic;

    public interface IEnvironment
    {
        string Get(string key);
        void Set(string key, string value);
        class SystemEnv : IEnvironment
        {
            private readonly Dictionary<string, string> overrides;
            public SystemEnv() => this.overrides = new Dictionary<string, string>();

            public virtual string Get(string key)
            {
                string result = overrides[key];
                return result != null ? result : Environment.GetEnvironmentVariable(key);
            }

            public virtual void Set(string key, string value) => overrides.Add(key, value);
        }
    }
}

