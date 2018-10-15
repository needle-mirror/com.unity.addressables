using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.UIElements;

namespace UnityEditor.AddressableAssets.GraphBuild
{
    internal class DataBuilderEditorWindow : EditorWindow
    {
        IDataBuilderGUI m_gui;
        Editor m_defaultEditor;
        public void SetTarget(IDataBuilder target)
        {
            if (m_gui != null)
            {
                m_gui.HideGUI();
                m_gui = null;
            }
            if (m_defaultEditor != null)
            {
                m_defaultEditor = null;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var context = new AddressablesBuildDataBuilderContext(settings);
            m_gui = target.CreateGUI(context);
            if (m_gui == null)
            {
                m_defaultEditor = Editor.CreateEditor(target as UnityEngine.Object);
            }
            else
            {
                m_gui.ShowGUI(UIElementsEntryPoint.GetRootVisualContainer(this));
            }
        }

        public void OnGUI()
        {
            if (m_gui == null)
            {
                m_defaultEditor.DrawDefaultInspector();
            }
            else
            {
                m_gui.UpdateGUI(new Rect(0, 0, position.width, position.height));
            }
        }
        public void OnDisable()
        {
            if (m_gui == null)
            {
                m_defaultEditor = null;
            }
            else
            {
                m_gui.HideGUI();
            }
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (Selection.activeObject as IDataBuilder != null)
            {
                var window = EditorWindow.GetWindow<DataBuilderEditorWindow>();
                window.Show();
                window.SetTarget(Selection.activeObject as IDataBuilder);
                return true; //catch open file
            }

            return false; // let unity open the file
        }
    }
}