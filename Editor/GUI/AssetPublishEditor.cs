using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace UnityEditor.AddressableAssets
{
    [System.Serializable]
    internal class AssetPublishEditor
    {
        [SerializeField]
        Vector2 scrollPosition = new Vector2();

        [SerializeField]
        bool fullBuildFoldout = true;
        [SerializeField]
        bool updateFoldout = true;
        [SerializeField]
        string snapshotPath = "/Snapshots/ABuildSnapshot";

        public bool OnGUI(Rect pos)
        {
            GUILayout.BeginArea(pos);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.MaxWidth(pos.width));

            GUILayout.Space(20);
            GUILayout.Label("     NOT YET FUNCTIONAL    ");
            GUILayout.Space(10);

            fullBuildFoldout = EditorGUILayout.Foldout(fullBuildFoldout, "Full Build");
            if(fullBuildFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(new GUIContent("This section will create a rebuild of all content packs as well as the core player build.  A snapshot of this build must be saved in order to do updates to it later."));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Build and Save Snapshot"))
                {
                    Debug.Log("we aren't actually building yet.");
                }
                GUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(20);

            updateFoldout = EditorGUILayout.Foldout(updateFoldout, "Update Build");
            if (updateFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(new GUIContent("This section will not create a core player build, and it will only create the new bundles needed when compared to a given snapshot."));
                GUILayout.BeginHorizontal();
                snapshotPath = EditorGUILayout.TextField(new GUIContent("Reference Snapshot"), snapshotPath);
                GUILayout.Space(10);
                if(GUILayout.Button("Browse"))
                {
                    Debug.Log("we aren't actually browsing yet.");
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create Updated Packs"))
                {
                    Debug.Log("we aren't actually updating yet.");
                }
                GUILayout.EndHorizontal();
                EditorGUI.indentLevel--;

            }


            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return false;
        }

        internal void OnEnable()
        {
           
        }

        internal void OnDisable()
        {
           
        }
    }
}
