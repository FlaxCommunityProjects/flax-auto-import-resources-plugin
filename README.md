# Auto Import Resources Plugin

Automagically imports everything from the `Assets` folder into `Content/Imported`. Whenever you change something, it re-imports it.

## [Video](https://youtu.be/KJiFIaDeB38)


## Want to contribute?

Check out the documentation on [Flax plugins](https://docs.flaxengine.com/manual/scripting/plugins/index.html). This one is an editor plugin.

Clone the project and open it in Flax. Then, check out the code in `Source/Editor`. It currently has 4 files
- [`ResourcePlugin.cs`](https://github.com/FlaxCommunityProjects/flax-auto-import-resources-plugin/blob/master/Source/Editor/ResourcePlugin.cs) simply sets up the plugin
- [`AssetFolderSynchronizer.cs`](https://github.com/FlaxCommunityProjects/flax-auto-import-resources-plugin/blob/master/Source/Editor/AssetFolderSynchronizer.cs) implements the actual logic. It looks at the `Assets` folder and (re)imports the new files into `Content/Imported`. It does so once at the beginning and then again every time something changes.
- [`FileSystemEventBuffer.cs`](https://github.com/FlaxCommunityProjects/flax-auto-import-resources-plugin/blob/master/Source/Editor/FileSystemEventBuffer.cs) is a wrapper around a [`FileSystemWatcher`](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-6.0). It debounces the usually quite chaotic filesystem events.
- [`ContentItemExtensions.cs`](https://github.com/FlaxCommunityProjects/flax-auto-import-resources-plugin/blob/master/Source/Editor/ContentItemExtensions.cs) has a single extension methods to get all children of a [`ContentFolder`](https://docs.flaxengine.com/api/FlaxEditor.Content.ContentFolder.html)
