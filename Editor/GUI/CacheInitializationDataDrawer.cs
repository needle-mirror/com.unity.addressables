using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    [CustomPropertyDrawer(typeof(CacheInitializationData), true)]
    class CacheInitializationDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var s = EditorStyles.label.CalcSize(label);
            EditorGUI.BeginProperty(position, label, property);
            var r = new Rect(position.x, position.y, position.width, s.y);

            {
                var prop = property.FindPropertyRelative("m_compressionEnabled");
                var val = EditorGUI.Toggle(r, new GUIContent("Compress Bundles", "Bundles are recompressed into LZ4 format to optimize load times."), prop.boolValue);
                if (val != prop.boolValue)
                    prop.boolValue = val;
                r.y += s.y + EditorGUIUtility.standardVerticalSpacing;
            }


            {
                var prop = property.FindPropertyRelative("m_cacheDirectoryOverride");
                var val = EditorGUI.TextField(r, new GUIContent("Cache Directory Override", "Specify custom directory for cache.  Leave blank for default."), prop.stringValue);
                if (val != prop.stringValue)
                    prop.stringValue = val;
                r.y += s.y + EditorGUIUtility.standardVerticalSpacing;
            }

            {
                var prop = property.FindPropertyRelative("m_expirationDelay");
                var val = EditorGUI.IntSlider(r, new GUIContent("Expiration Delay (in seconds)", "Controls how long items are left in the cache before deleting."), prop.intValue, 0, 12960000);
                r.y += s.y + EditorGUIUtility.standardVerticalSpacing;
                var ts = new System.TimeSpan(0, 0, val);
                EditorGUI.LabelField(new Rect(r.x + 16, r.y, r.width - 16, r.height), new GUIContent(NicifyTimeSpan(ts)));
                if (val != prop.intValue)
                    prop.intValue = val;
                r.y += s.y + EditorGUIUtility.standardVerticalSpacing;
            }

            {
                var limProp = property.FindPropertyRelative("m_limitCacheSize");
                if (limProp.boolValue = EditorGUI.ToggleLeft(r, new GUIContent("Limit Cache Size"), limProp.boolValue))
                {
                    var prop = property.FindPropertyRelative("m_maximumCacheSize");
                    if (prop.longValue == long.MaxValue)
                        prop.longValue = (1024 * 1024 * 1024);//default to 1GB

                    r.y += s.y + EditorGUIUtility.standardVerticalSpacing;
                    var mb = (prop.longValue / (1024 * 1024));
                    var val = EditorGUI.LongField(new Rect(r.x + 16, r.y, r.width - 16, r.height), new GUIContent("Maximum Cache Size (in MB)", "Controls how large the cache can get before deleting."), mb);
                    if (val != mb)
                        prop.longValue = val * (1024 * 1024);
                }
            }
            EditorGUI.EndProperty();
        }

        private string NicifyTimeSpan(TimeSpan ts)
        {
            if (ts.Days >= 1)
                return string.Format("{0} days, {1} hours, {2} minutes, {3} seconds.", ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
            if (ts.Hours >= 1)
                return string.Format("{1} hours, {2} minutes, {3} seconds.", ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
            if (ts.Minutes >= 1)
                return string.Format("{2} minutes, {3} seconds.", ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
            return string.Format("{0} seconds.", ts.Seconds);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var s = EditorStyles.label.CalcSize(label);
            return s.y * 5 + EditorGUIUtility.standardVerticalSpacing * 4;
        }
    }
}
