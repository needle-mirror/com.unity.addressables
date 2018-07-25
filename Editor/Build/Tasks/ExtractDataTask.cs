using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.AddressableAssets
{
    public class ExtractDataTask : IBuildTask
    {
        public int Version { get { return 1; } }

        public IDependencyData DependencyData { get { return m_DependencyData; } }

        public IBundleWriteData WriteData { get { return m_WriteData; } }
        
#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IDependencyData m_DependencyData;
        
        [InjectContext(ContextUsage.In)]
        IBundleWriteData m_WriteData;
#pragma warning restore 649

        public ReturnCode Run()
        {
            return ReturnCode.Success;
        }
    }
}