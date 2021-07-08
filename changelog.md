**1.2.3**
 - Fixed issue with dependency resolvers getting updated several times when a dependency resolver gets activated/deactivated in the AssetRelationsViewer
 - Sped up calculation for asset sizes by adding cache

**1.2.2**
 - Added dependency resolver to resolve gameobjects dependencies inside the currently opened scene/prefab
 - Removed old ObjectDependencyResolver since its fully replaced by the ObjectSerializedDependencyResolver

**1.2.0**
 - Added support for subassets
 - Added support for unity internal assets
 - Split assets and files into different caches
 - Sped up the ObjectSerializedDependencyResolver asset traversal
 - ObjectSerializedDependencyResolver and AssetToFileDependencyResolver are now the default activated dependency resolvers
 - Removed compiler warnings due to deprecated code in Unity 2020.2

**1.1.1**
 - Renamed some classes
 - Sped up the ObjectSerializedDependencyResolver by skipping over char properties fast.
 - Removed comparison against dependencies returned from the AssetDatabase.GetDependencies() function in the ObjectSerializedDependencyResolver 
 - Updated readme file
 
**1.1.0**
 - Added support for prefab variants to be detected by the ObjectSerializedDependencyResolver
 - Fixed calculation of asset sizes when using AssetDatabaseV2

**1.0.0**

 - Initial commit