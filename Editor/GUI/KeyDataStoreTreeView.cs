using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System;
using System.Linq;

namespace UnityEditor.AddressableAssets
{
    public class KeyDataStoreTreeView : TreeView
    {
        class Item : TreeViewItem
        {
            internal string m_key;
            public Item(string key) : base(key.GetHashCode(), 0, key)
            {
                m_key = key;
            }
        }

        KeyDataStore m_data;
        AddressableAssetSettings m_settings;
        public KeyDataStoreTreeView(AddressableAssetSettings settings, KeyDataStore data, TreeViewState state, MultiColumnHeaderState mchs) : base(state, new MultiColumnHeader(mchs))
        {
            if (data != null)
            {
                m_data = data;
                m_settings = settings;
                m_data.OnSetData += OnSetData;
            }
        }

        ~KeyDataStoreTreeView()
        {
            if(m_data != null)
                m_data.OnSetData -= OnSetData;
            m_data = null;
        }

        void OnSetData(string key, object data, bool isNew)
        {
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1);
            root.children = new List<TreeViewItem>();
            foreach (var k in m_data.Keys)
                root.AddChild(new Item(k));

            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                CellGUI(args.GetCellRect(i), args.item as Item, args.GetColumn(i), ref args);
        }

        private void CellGUI(Rect cellRect, Item item, int column, ref RowGUIArgs args)
        {
            if (item == null)
                return;

            if (column == 0)
            {
                EditorGUI.LabelField(cellRect, item.displayName);
            }
            else
            {
                var itemType = m_data.GetDataType(item.m_key);
                if (itemType == typeof(string))
                {
                    string profileEntryId = m_data.GetData(item.m_key, default(object)).ToString();
                    AddressableAssetProfileSettings.ProfileIDData profileEntry = m_settings.profileSettings.GetProfileDataById(profileEntryId);
                    var displayNames = m_settings.profileSettings.GetVariableNames();
                    int currentIndex = Array.IndexOf(displayNames, profileEntry == null ? AddressableAssetProfileSettings.k_customEntryString : profileEntry.Name);
                    bool custom = profileEntry == null && currentIndex >= 0;
                    var leftRect = new Rect(cellRect.x, cellRect.y, cellRect.width / 3, cellRect.height);
                    var rightRect = new Rect(cellRect.x + leftRect.width, cellRect.y, cellRect.width - leftRect.width, cellRect.height);
                    var newIndex = EditorGUI.Popup(leftRect, currentIndex, displayNames);
                    if (newIndex != currentIndex)
                    {
                        if (displayNames[newIndex] == AddressableAssetProfileSettings.k_customEntryString)
                        {
                            custom = true;
                            profileEntryId = "<undefined>";
                            m_data.SetDataFromString(item.m_key, profileEntryId);
                        }
                        else
                        {
                            custom = false;
                            profileEntry = m_settings.profileSettings.GetProfileDataByName(displayNames[newIndex]);
                            if (profileEntry != null)
                                profileEntryId = profileEntry.Id;
                        }
                    }

                    if (custom)
                    {
                        var currVal = m_data.GetDataString(item.m_key, "<undefined>");
                        var newValue = EditorGUI.DelayedTextField(rightRect, currVal);
                        if (newValue != currVal)
                            m_data.SetDataFromString(item.m_key, newValue);
                    }
                    else
                    {
                        if (profileEntry != null)
                        {
                            if (profileEntry.Id != m_data.GetData(item.m_key, default(object)).ToString())
                                m_data.SetDataFromString(item.m_key, profileEntry.Id);
                            var evaluated = profileEntry.Evaluate(m_settings.profileSettings, m_settings.activeProfileId);
                            EditorGUI.LabelField(rightRect, evaluated);
                        }
                    }
                }
                else
                {
                    //do custom UI for type...
                    if (itemType.IsEnum)
                    {
                        var currVal = m_data.GetData(item.m_key, default(Enum));
                        var newval = EditorGUI.EnumPopup(cellRect, currVal);
                        if (currVal != newval)
                        {
                            m_data.SetData(item.m_key, newval);
                        }
                    }
                    else if (itemType.IsPrimitive)
                    {
                        var currVal = m_data.GetData(item.m_key, default(object)).ToString();
                        var newVal = EditorGUI.DelayedTextField(cellRect, currVal);
                        if (newVal != currVal)
                            m_data.SetDataFromString(item.m_key, newVal);
                    }
                }
            }
        }

        internal static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            var retVal = new MultiColumnHeaderState.Column[2];
            retVal[0] = new MultiColumnHeaderState.Column();
            retVal[0].headerContent = new GUIContent("Key", "Data Key");
            retVal[0].minWidth = 50;
            retVal[0].width = 100;
            retVal[0].maxWidth = 300;
            retVal[0].headerTextAlignment = TextAlignment.Left;
            retVal[0].canSort = true;
            retVal[0].autoResize = true;

            retVal[1] = new MultiColumnHeaderState.Column();
            retVal[1].headerContent = new GUIContent("Value", "Data Value");
            retVal[1].minWidth = 300;
            retVal[1].width = 500;
            retVal[1].maxWidth = 1000;
            retVal[1].headerTextAlignment = TextAlignment.Left;
            retVal[1].canSort = true;
            retVal[1].autoResize = true;

            return new MultiColumnHeaderState(retVal);
        }
    }
}