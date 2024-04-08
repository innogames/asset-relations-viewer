**3.0.0**
- SerializedPropertyType.ManagedReference are now traversed so SerializeReferences are now working
- Cache now handles files with over 32k subassets or dependencies without causing errors

**3.0.0-pre.2**
- Fix possible ```cs ArgumentException: An item with the same key has already been added``` which happened if a deleted file was referenced as a dependency

**3.0.0-pre.1**
- Unity Addressables are now supported in the base package, the additional repo is not required anymore!
- Removed any reflection code and fully rely on SerializedProperties
- Node Search calculation is now done in seperate thread
- API changes to INodeHandler and IAssetDependencyResolver to simplify implementations
- Sizes of FileNodes and AssetNodes are now cached for faster loading
- Added async update functionality using Enumerators
- Removed support for Unity 2018 and below

**2.0.0**
- Removed IsExisting info from nodes since caches now need to delete nodes if they are saved but not existing anymore on update from their node list
- Added use of Span from Unity 2021 onwards where previously as lot of garbage was created due to strings
- Node name, type and type information is now directly stored inside the Node class itself
- Added functions to NodeDependencyLookupUtility to get all node sizes, names and type information
- Sprites file sizes are not taken into account anymore if they are part of a SpriteAtlases
- Sizes of SpriteAtlases and AudioClips are now calculated correctly
- Sped up creation of large dependency trees in ARV
- AssetRelationsViewer can now be opened without updating or loading any caches

**1.5.2**
- Remove warning

**1.5.1**
- Increase serialize version of AssetToFileDependencyCache because of dependency order change
- Fixed possible StackOverflowException with very huge dependency trees and ShowAdditionalInformation option being enabled

**1.5.0**
- AssetDependencyCache update is now a lot faster due to improved reflection code
- "Calculating all node sizes" step is now faster since it now only calculates reachable nodes
- Unity builtin assets are now references
- AssetToFile dependency cache node dependencies now always have the main asset as the first element in the list
- Add support for AssemblyDefitions and AssemblyDefinitionReferences with new AsmDef dependency type

**1.4.6**
- Fixed issue references inside added components of nested prefabs where not found properly

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