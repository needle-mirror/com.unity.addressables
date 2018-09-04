
using UnityEngine;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    static internal class GraphColors
    {
        private static Color kWindowBackground = new Color(0.63f, 0.63f, 0.63f, 1.0f);
        private static Color kLabelGraphLabelBackground = new Color(0.75f, 0.75f, 0.75f, 0.75f);

        private static Color kWindowBackgroundPro = new Color(0.15f, 0.15f, 0.15f, 1.0f);
        private static Color kLabelGraphLabelBackgroundPro = new Color(0, 0, 0, .75f);

        internal static Color WindowBackground { get { return EditorGUIUtility.isProSkin ? kWindowBackgroundPro : kWindowBackground; } }
        internal static Color LabelGraphLabelBackground { get { return EditorGUIUtility.isProSkin ? kLabelGraphLabelBackgroundPro : kLabelGraphLabelBackground; } }
    }
}