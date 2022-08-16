**1.4.5**
- Fixed issue that total amount of supported assets was limited to max size of short but is now limitted to max size of int.

**1.4.4**
- Added support for Unity 2021.3.x

**1.4.3**
 - Referenced components of animatorcontrollers are now found as a dependency

**1.4.2**
 - Referenced components in other prefab assets are now found as a dependency
 
**1.4.1**
 - Bugfix for dependency types being initially activated instead of disabled
 - Settings are now saved project independently per project inside editor prefs

**1.4.0**
 - Optimized performance of initial dependency calculation
 - Optimized performance of dependency tree view calculation
 - Calculation of hierarchy tree filesizes is now done in thread and calculated on the fly when node gets visible
 - Fixed bug where optimization to save calculations in the reflection stack (which can find generic dependencies) could lead to wrong results
 - Type of assets is now displayed when having "Show additional node information" enabled
 - Dependency types can now be partially updated and unloaded

**1.3.2**
 - Various optimizations to support dependency trees which would result in millions of displayed nodes
 - Fixed bug where the same nodes dependencies would be added multiple times when using the AssetToFile dependency resolver

**1.3.1**
 - Scenes from packages are avoided from being scanned since they cant be loaded from readonly packages

**1.3.0**
 - Fixed several bugs regarding detection of if an asset is packed into the app or not and added cache to speed up the detection
 - Fixed issue that added components to a PrefabInstance didnt show the usage of their script as a dependency
 - Fixed that once "Sync to explorer" option in asset type handler it could not get disabled anymore
 - Increased serialized version to 1.3 to force
 - Added progressbars for loading and saving the AssetDependencyCache

**1.2.4**
 - Increased version to make a new release

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