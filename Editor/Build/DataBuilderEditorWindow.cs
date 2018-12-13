using System;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.UIElements;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets.GraphBuild
{
    class DataBuilderEditorWindow : EditorWindow
    {
        IDataBuilderGUI m_GUI;
        Editor m_DefaultEditor;

        public void SetTarget(IDataBuilder target)
        {
            if (m_GUI != null)
            {
                m_GUI.HideGUI();
                m_GUI = null;
            }

            m_DefaultEditor = null;

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var context = new AddressablesBuildDataBuilderContext(settings);
            m_GUI = target.CreateGUI(context);
            if (m_GUI == null)
            {
                m_DefaultEditor = Editor.CreateEditor(target as Object);
            }
            else
            {
                m_GUI.ShowGUI(this.GetRootVisualContainer());
            }
        }

        public void OnGUI()
        {
            if (m_GUI == null)
            {
                m_DefaultEditor.DrawDefaultInspector();
            }
            else
            {
                m_GUI.UpdateGUI(new Rect(0, 0, position.width, position.height));
            }
        }

        public void OnDisable()
        {
            if (m_GUI == null)
            {
                m_DefaultEditor = null;
            }
            else
            {
                m_GUI.HideGUI();
            }
        }

        [OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            if (Selection.activeObject is IDataBuilder)
            {
                var window = GetWindow<DataBuilderEditorWindow>();
                window.Show();
                window.SetTarget(Selection.activeObject as IDataBuilder);
                return true; //catch open file
            }

            return false; // let unity open the file
        }
    }
}
