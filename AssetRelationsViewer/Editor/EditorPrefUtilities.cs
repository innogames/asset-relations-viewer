using System;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public static class EditorPrefUtilities
    {
        public static string GetProjectSpecificKey(string key)
        {
            return $"ARV_{Application.dataPath}_{key}";
        }
        
        public static void TogglePref(PrefValue<bool> pref, string label, Action<bool> onChange = null, int width = 180)
        {
            pref.DirtyOnChange(EditorGUILayout.ToggleLeft(label, pref, GUILayout.Width(width)), onChange);
        }

        public static void IntSliderPref(PrefValue<int> pref, string label, Action<int> onChange = null)
        {
            pref.DirtyOnChange(EditorGUILayout.IntSlider(label, pref, pref.MinValue, pref.MaxValue), onChange);
        }
    }
}