using System;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// Helper class to more conveniently handle EditorPrefs 
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class PrefValue<T>
	{
		protected T DefaultValue;
			
		protected string Key;
		protected T Value;

		public readonly T MinValue;
		public readonly T MaxValue;

		protected abstract void Save();
		protected abstract void Load();
			
		public PrefValue(string key, T defaultValue, T minValue, T maxValue)
		{
			Key = key;
			DefaultValue = defaultValue;
				
			MinValue = minValue;
			MaxValue = maxValue;
		}

		public void SetValue(T value)
		{
			Value = value;
			Save();
		}

		public T GetValue()
		{
			Load();
			return Value;
		}
			
		public void DirtyOnChange(T newValue, Action<T> onChange = null)
		{
			if (!newValue.Equals(GetValue()))
			{
				SetValue(newValue);
					
				if(onChange != null)
					onChange(newValue);
			}
		}

		public static implicit operator T(PrefValue<T> pref)
		{
			return pref.GetValue();
		}
	}
		
	public class PrefValueBool : PrefValue<bool>
	{
		public PrefValueBool(string key, bool defaultValue): base(key, defaultValue, false, true){}
			
		protected override void Save()
		{
			EditorPrefs.SetBool(Key, Value);
		}

		protected override void Load()
		{
			Value = EditorPrefs.GetBool(Key, DefaultValue);
		}
	}
		
	public class PrefValueString : PrefValue<String>
	{
		public PrefValueString(string key, string defaultValue): base(key, defaultValue, "", ""){}
			
		protected override void Save()
		{
			EditorPrefs.SetString(Key, Value);
		}

		protected override void Load()
		{
			Value = EditorPrefs.GetString(Key, DefaultValue);
		}
	}

	public class PrefValueInt : PrefValue<int>
	{
		public PrefValueInt(string key, int defaultValue, int minValue, int maxValue) : base(key, defaultValue, minValue, maxValue){}
			
		protected override void Save()
		{
			EditorPrefs.SetInt(Key, Value);
		}

		protected override void Load()
		{
			Value = EditorPrefs.GetInt(Key, DefaultValue);
		}
	}
}