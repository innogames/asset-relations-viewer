# asset-relations-viewer

Plugin to display dependencies between assets in a tree based view within the Unity editor.

![](Docs~/Images/arv_example.png)

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
	* Etc.

<br><br><br><br>
## Installation

#### For Unity 2018.3 or later (Using Unity Package Manager)

Find the manifest.json file in the Packages folder of your project and edit it to look like this:
```js
{
  "dependencies": {
    "com.innogames.asset-relations-viewer": "https://github.com/innogames/asset-relations-viewer.git",
    ...
  },
}
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

**Back button ("<<")**: Button to go back to previous selected asset to view

**Thumbnail Size**: Size of the shown thumbnail in pixels

**Node Depth**: Depth of the shown tree structure

**Refresh**: Refreshed view after asset has changed

**Save and Refresh**: Saves the project before refreshing the view to make sure all changes are applied to assets before

**Show Size Information**: Displays additional information for bytesize of asset on each node

**Show Thumbnails**: Shown correct thumbnails on nodes if available

**Show nodes Once**: To only show each node (Asset) once within the displayed tree

**Show hierarchy Once**: To only show the same dependency hierarchy for an asset once within the displayed tree

**Show referencers**: If referencers (Assets that have the selected asset as a dependency) should be shown or not

**Show Property Pathes**: If path of where the dependency is within the scene, prefab, scriptable object is shown

**Align Nodes**: If all nodes if the same depth should be align horizontally in the displayed tree 

**Hide Filtered Nodes**: Hide all nodes that are filtered out instead of just graying them out

**Highlight packaged assets**: Adds green highlight to nodes which are going to be packed into the app (Are actually used by the game)

**Merge Relations**: If an asset has the same asset as a dependency multiple times, the same dependency is just shown once


#### Asset Options
Options specific to assets

**Selected Asset**: The asset the current dependency tree is shown for

**Filter**: To filter for names or types within the displayed tree (Usage same as filter in project explorer)

**Sync to explorer**: If selected the currently selected asset in the project explorer will be the one shown in the AssetRelationsViewer

#### Node

![](Docs~/Images/arv_node.png)

**s**: If the selected node in an asset, it will be selected in the unity project explorer

**>**: Makes the clicked on node the current viewed node in the AssetRelationsViewer

<br><br><br><br>
## Resolvers
The AssetRelationsViewer supports different resolvers to find dependencies for assets. By default there are two built in ones, but additional ones can be added as plugins, to not only find dependencies for assets, but also 

#### ObjectDependencyResolver
Uses Unitys internal AssetDatabase.GetDependencies() function to find dependencies for assets.
Its fast and reliable, but can only find dependencies between assets.

#### ObjectSerializedDependencyResolver
Uses own implementation which is based on SerializedObjects and SerializedProperties to find assets and other dependency types.
Since this solution is based on an own dependency search implementation, it is much slower than the ObjectDependencyResolver.

<br><br><br><br>
## Showing dependency pathes
If one wants to know where exactly in a scene, prefab, scriptable object a reference is done the "ObjectSerializedDependencyResolver" needs to be active and "Show Property Pathes" in the menu needs to be active.
Once active the whole path of the dependency (GameObject->Components->ClassMemberVariable) is shown.

#### Showing "Unknown Path" path nodes
This is due to the issue that the AssetDatabase.GetDependencies() function returns dependencies of nested prefabs as well as prefab variants even though the dependencies are not serialized within the asset itself.
This is why the ObjectSerializedDependencyResolver cant find these dependencies within the serialized properties of the asset itself while AssetDatabase.GetDependencies() still returns it for non recursive dependencies so the path is unknown.

![](Docs~/Images/arv_example_pathes.png)

<br><br><br><br>
## Troubleshooting
There can be cases where no tree is shown in the AssetRelationsViewer

* Make sure a node (Asset) is selected to be shown
* Make sure a dependency cache (AssetDependencyCache) and dependency resolver (ObjectDependencyResolver) is selected, otherwise no dependency can be found
* After a  code recompile the dependency cache needs to be updated by clicking on "Refresh"

<br><br><br><br>
## Addons
Support to display different connection and node types can be added by addons.

#### Addressable system
An addon is available to add support for showing addressables and also addressable groups from Unitys Addressables system.
The Package is called asset-relations-viewer-addressables

#### Writing own addons to support custom connection- and nodetypes
Documentation on how to write own addons will be added later