using System.Collections.Generic;
using UnityEditor.Compilation;

namespace UnityEditor.AddressableAssets.Build
{
    internal class BuildUtility
    {
        static HashSet<string> s_EditorAssemblies = null;
        static HashSet<string> editorAssemblies
        {
            get
            {
                if (s_EditorAssemblies == null)
                {
                    s_EditorAssemblies = new HashSet<string>();
                    foreach (var assembly in Compilation.CompilationPipeline.GetAssemblies())
                    {
                        if ((assembly.flags & AssemblyFlags.EditorAssembly) != 0)
                            s_EditorAssemblies.Add(assembly.name);
                    }
                }

                return s_EditorAssemblies;
            }
        }
        public static bool IsEditorAssembly(System.Reflection.Assembly assembly)
        {
            var splitName = assembly.FullName.Split(',');
            return splitName.Length > 0 && editorAssemblies.Contains(splitName[0]);
        }
    }
}
