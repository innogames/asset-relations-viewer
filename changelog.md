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