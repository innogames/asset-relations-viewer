using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.Addressables
{
	public class AssetToAssetAssetRefDependency
	{
		public const string Name = "ATOA_AssetRef";
	}

	/// <summary>
	/// Resolver to find dependencies to assets which are connected via the AddressableAssets system
	/// </summary>
	public class AddressableAssetReferenceResolver : IAssetDependencyResolver
	{
		private const string ConnectionTypeDescription = "Dependencies between assets done by an Addressable AssetReference";
		private static DependencyType AddressableType = new DependencyType("Asset->Asset by AssetReference", new Color(0.6f, 0.7f, 0.85f), true, false, ConnectionTypeDescription);

		private readonly HashSet<string> validGuids = new HashSet<string>();
		private const string Id = "AddressableReferenceResolver";

		public bool IsGuidValid(string guid)
		{
			return validGuids.Contains(guid);
		}

		public string GetId()
		{
			return Id;
		}

		public DependencyType GetDependencyTypeForId(string typeId)
		{
			return AddressableType;
		}

		public string[] GetDependencyTypes()
		{
			return new[] { AssetToAssetAssetRefDependency.Name };
		}

		public void SetValidGUIDs()
		{
			validGuids.Clear();

			string[] filters =
			{
				"t:GameObject",
				"t:Scene",
				"t:ScriptableObject",
				"t:TimelineAsset",
			};

			foreach (string filter in filters)
			{
				foreach (string guid in AssetDatabase.FindAssets(filter))
				{
					validGuids.Add(guid);
				}
			}
		}

		public void Initialize(AssetDependencyCache cache)
		{
			// No implementation
		}

		public void TraversePrefab(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
		{
			// No implementation
		}

		public void TraversePrefabVariant(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
		{
			// No implementation
		}

		public AssetDependencyResolverResult GetDependency(ref string sourceAssetId, ref SerializedProperty property,
			ref string propertyPath, SerializedPropertyType type)
		{
			if (!property.type.StartsWith("AssetReference", StringComparison.Ordinal))
			{
				return null;
			}

			SerializedProperty assetGUIDProperty = property.FindPropertyRelative("m_AssetGUID");

			if (assetGUIDProperty != null && !string.IsNullOrEmpty(assetGUIDProperty.stringValue))
			{
				string guid = assetGUIDProperty.stringValue;
				string assetPath = AssetDatabase.GUIDToAssetPath(guid);

				if (string.IsNullOrEmpty(assetPath))
				{
					return null;
				}

				Object asset = null;
				string subAssetName = property.FindPropertyRelative("m_SubObjectName").stringValue;

				if (!string.IsNullOrEmpty(subAssetName))
				{
					string subAssetType = property.FindPropertyRelative("m_SubObjectType").stringValue;
					Object[] allAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);

					foreach (Object allAsset in allAssets)
					{
						if (allAsset != null && allAsset.name == subAssetName && (string.IsNullOrEmpty(subAssetType) || allAsset.GetType().Name == subAssetType))
						{
							asset = allAsset;
							break;
						}
					}
				}
				else
				{
					asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
				}

				string assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(asset);
				return new AssetDependencyResolverResult{Id = assetId, NodeType = AssetNodeType.Name, DependencyType = AssetToAssetAssetRefDependency.Name};
			}

			return null;
		}
	}
}