using System;
using System.Collections.Generic;
using System.IO;
using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.GUI;
using FlaxEngine;

namespace ResourcesPlugin.Source.Editor
{
	public class ResourcePlugin : EditorPlugin
	{
		public override PluginDescription Description => new PluginDescription
		{
			Name = "Resource Auto Import Plugin",
			Category = "Other",
			Author = "Stefnotch",
			AuthorUrl = null,
			HomepageUrl = null,
			//RepositoryUrl = "https://github.com/FlaxEngine/ExamplePlugin",
			Description = "This is a Content/Resources folder auto importer.",
			Version = new Version(1, 0),
			IsAlpha = false,
			IsBeta = false,
		};

		private AssetFolderSynchronizer _assetFolderSynchronizer;

		public override void Initialize()
		{
			base.Initialize();
		}

		/// <inheritdoc />
		public override void InitializeEditor()
		{
			base.InitializeEditor();

			var rawAssetsPath = Path.Combine(Globals.ContentFolder, "..", "Assets");
			var importedAssetsPath = Path.Combine(Globals.ContentFolder, "Imported");
			_assetFolderSynchronizer = new AssetFolderSynchronizer(rawAssetsPath, importedAssetsPath, Editor);
		}

		/// <inheritdoc />
		public override void Deinitialize()
		{
			_assetFolderSynchronizer?.Dispose();
			base.Deinitialize();
		}
	}
}