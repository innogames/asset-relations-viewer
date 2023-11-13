using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public static class ARVStyles
    {
        private static Color[] topRectColor = {new Color(0.67f, 0.67f, 0.67f, 1.0f), new Color(0.27f, 0.27f, 0.27f, 1.0f)};
        private static Color[] connectionColorMod = {new Color(0.50f, 0.5f, 0.5f, 0.5f), new Color(0.8f, 0.8f, 0.8f, 0.5f)};
        private static Color[] packageToAppColor = {new Color(0.1f, 0.7f, 0.2f, 0.7f), new Color(0.2f, 1.0f, 0.3f, 0.6f)};
        private static Color[] textColorMod = {new Color(0.3f, 0.3f, 0.3f, 4.0f), new Color(0.7f, 0.7f, 0.7f, 4.0f)};

        private static Color[] nodeBackGroundColor = {new Color(0.7f, 0.7f, 0.73f), new Color(0.1f, 0.1f, 0.15f)};
        private static Color[] nodeBackGroundColorOwn = {new Color(0.70f, 0.45f, 0.4f), new Color(0.60f, 0.25f, 0.2f)};

        private static Color[] nodeFilteredOverlayColor = {new Color32(194, 194, 194, 116), new Color32(56, 56, 56, 116)};

        private static int ColorIndex => EditorGUIUtility.isProSkin ? 1 : 0;

        public static Color TopRectColor => topRectColor[ColorIndex];
        public static Color ConnectionColorMod => connectionColorMod[ColorIndex];
        public static Color PackageToAppColor => packageToAppColor[ColorIndex];
        public static Color TextColorMod => textColorMod[ColorIndex];
        public static Color NodeBackGroundColor => nodeBackGroundColor[ColorIndex];
        public static Color NodeBackGroundColorOwn => nodeBackGroundColorOwn[ColorIndex];
        public static Color NodeFilteredOverlayColor => nodeFilteredOverlayColor[ColorIndex];
    }
}