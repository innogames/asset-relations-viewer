# asset-relations-viewer

Plugin to display dependencies between assets, files, assetgroups, etc. in a tree based view within the Unity editor.

![](Docs~/Images/arv.png)

<br><br><br><br>
## Features

* Standalone Editor UI without any external dependencies
* View which dependencies an asset has to other assets
* View which assets have the given asset as a dependency
* Show thumbnails of all assets in the dependency tree
* filter for Asset Names and Asset Types in displayed tree
* Highlight if an asset is used in the project (going to be packed in the app)
* Show path of where an asset is used exactly within a scene, prefab or asset
* Display byte size of compressed asset together with overall size of dependency tree
* Extendable by own dependency resolvers, for example to show addressables
* Support additional connection- and nodetypes which can be added via addons, for example:
	* Addressable Groups
	* Addressables
	* AssetBundles
	* LocaKeys
	* AssemblyDefinitions
	* Etc.

<br><br><br><br>
## Installation

#### For Unity 2018.3 or later (Using Unity Package Manager)

Find the manifest.json file in the packages folder of your project and edit it to look like this:
```js
{
  "dependencies": {
    "com.innogames.asset-relations-viewer": "https://github.com/innogames/asset-relations-viewer.git",
    ...
  },
}
```

If you are in production and not want to get automatic updates always use a tagged version like:

```js
	...
    "com.innogames.asset-relations-viewer": "https://github.com/innogames/asset-relations-viewer.git#1.4.0",
    ...

```

<br><br><br><br>
## First Usage

1. Select an asset within the unity project explorer
2. Right click to open context menu for an asset
3. Select "Show in Asset Relations Viewer"
4. On Dialog for the first startup of AssetRelationsViewer click on yes
5. Wait for the resolver to find all dependencies for all assets in the project which can take a while for a large project with many assets

<br><br><br><br>
## Controls

#### Menu items

Menu items sorted from left to right

<br>

**Back button ("<<")**: Button to go back to previous selected asset to view <br/>
**Thumbnail Size**: Size of the shown thumbnail in pixels <br/>
**Node Depth**: Depth of the shown tree structure <br/>
**Refresh**: Refreshed view after asset has changed <br/>
**Save and Refresh**: Saves the project before refreshing the view to make sure all changes are applied to assets before <br/>
**Show additional node information **: Displays size and also type of the nodes if selected <br/>
**Show Thumbnails**: Shown correct thumbnails on nodes if available <br/>
**Show nodes Once**: To only show each node (Asset) once within the displayed tree <br/>
**Show hierarchy Once**: To only show the same dependency hierarchy for a node once within the displayed tree <br/>
**Show referencers**: If referencers (Assets that have the selected asset as a dependency) should be shown or not <br/>
**Show Property Pathes**: If path of where the dependency is within the scene, prefab, scriptable object is shown <br/>
**Align Nodes**: If all nodes if the same depth should be align horizontally in the displayed tree  <br/>
**Hide Filtered Nodes**: Hide all nodes that are filtered out instead of just graying them out <br/>
**Highlight packaged assets**: Adds green highlight to nodes which are going to be packed into the app (Are actually used by the game) <br/>
**Merge Relations**: If a node has the same asset as a dependency multiple times, the same dependency is just shown once <br/>


#### Node

![](Docs~/Images/arv_node.png)

**s**: If the selected node in an asset, it will be selected in the unity project explorer

**>**: Makes the clicked on node the current viewed node in the AssetRelationsViewer

<br><br><br><br>

# Dependency type list
The AssetRelationViewer can display several types of dependencies. In order for them to show up these need to be selected first.

![](Docs~/Images/arv_connectiontypes_base.png)

After selecting a dependency type that is currently not loaded the <b>Load caches</b> button need to be clicked on in order to finally load the dependency types. <br/>
Unused dependency types can be unloaded by clicking on the <b>U</b> button.

![](Docs~/Images/arv_connectiontypes_activation.png)

**L** Dependency type is active and currently loaded <br/>
**R** Dependency type got activated but needs to be loaded <br/>
**U** Dependency type got deactivated and can be unloaded <br/>

## Dependency types

### Asset->Asset by Object
Dependencies of assets to assets by a UnityEngine.Object reference.

![](Docs~/Images/arv_assettoasset_dependencies.png)

### Asset->File
Dependencies of assets to the file they are part of. <br/>
This is required since a file can have several subassets meaning that an asset is not automatically the file itself even though most files only contain a single asset. <br/>
Note: Only the file itself has size information about the assets it contains. So in order to get size information with the "Show additional node information" this dependecy type need to be enabled <br/>

![](Docs~/Images/arv_assettofile_dependencies.png)

### Scene GameObject->GameObject
Dependencies of GameObject to GameObjects within the currently loaded scene or prefab. <br/>
If an in scene GameObject is selected as the viewed node only GameObject->GameObject dependencies are shown. The GameObject->GameObject dependency view can not be viewed together with other dependencies. <br/>
Note: If the opened scene/prefab changed after opening the AssetRelationViewer the cache needs to be refreshed. <br/> 

![](Docs~/Images/arv_inscene_dependencies.png)

<br><br><br><br>
## Node search and filter
![](Docs~/Images/arv_node_search_and_filter.png)

### Node search
Since every type of node can be viewed in the AssetRelationViewer like Assets, Files, AddressableAssetGroups, AsmDefs, etc. is sometime helpful to have a generic node search.<br/>
Because finding all nodes in the projects might take a while this feature needs to be activated first by clicking on the "Enable" botton.<br/>
Only known nodes based on the activated dependency types are shown 
Nodes can be searched for by name and by type. <br/>
Based on the filtered name and type the dropdown will show all available nodes to select. <br/>
Once selected in the dropdown the current node can be shown in the AssetRelationsViewer by clicking <b>Select</b>. <br/>

### Node hierarchy filter
To specifically filter for either a node name or node type the "Node hierarchy filter:" can be used. <br/>
Only the matching nodes will be highlighted and all others are grayed out. <br/>
In the example the dependency tree is filtered for "vertexLit". <br/>

![](Docs~/Images/arv_node_filter.png)

<br><br><br><br>
## Showing dependency pathes
In the AssetRelationsViewer it is possible to not only view dependencies between Asset->Asset for example but also exactly through with path the dependency exist. <br/>
In order to view the pathes "Show Property Pathes" needs to be activated. <br/>
Once active the whole path of the dependency (GameObject->Components->ClassMemberVariable) is shown in the hierarchy tree view. <br/>

![](Docs~/Images/arv_pathes.png)

## Node handlers
GUI options specific to a node type. <br/>

![](Docs~/Images/arv_typehandler.png)

### Asset
Options specific to Assets. <br/>

Contains dropdown to select an asset in the project. <br/>
**Sync to explorer**: If selected the currently selected asset in the project explorer will be the one shown in the AssetRelationsViewer. <br/>

### File
Options specific to Files. <br/>
Currently there are no specific options for them. <br/>

### InSceneGameObject
Options for in scene GameObjects. <br/>

Contains dropdown to select a GameObject from the currently opened scene/prefab. <br/>
**Sync to hierarchy**: If selected the currently selected GameObject in the currently opened scene/prefab will be shown in the AssetRelationsViewer. <br/>

<br><br><br><br>
## Troubleshooting
There can be cases where no tree is shown in the AssetRelationsViewer

* Make sure a node (Asset) is selected to be shown
* Make sure a dependency cache (AssetDependencyCache) and dependency resolver (ObjectDependencyResolver) is selected, otherwise no dependency can be found
* After a code recompile the dependency cache needs to be updated by clicking on "Refresh"
* Some dependency types might not update/work correctly when being in PlayMode

<br><br><br><br>
## Addons
Support to display different connection and node types can be added by addons.

#### Addressable system
An addon is available to add support for showing addressables and also addressable groups from Unitys Addressables system. <br/>
The Package is called asset-relations-viewer-addressables. <br/>
It can be found at https://github.com/innogames/asset-relations-viewer-addressables <br/>

#### Writing own addons to support custom connection- and nodetypes
Own addons can be also added so custom dependencies with any nodetype and dependency type can be added to be viewed inside the Asset Relations Viewer. <br/>
For seing how to add own addons, please have a look at the Addressable sytem addon source code. <br/>

