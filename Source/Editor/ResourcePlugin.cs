using System;
using System.Collections.Generic;
using System.IO;
using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.GUI;
using FlaxEngine;

namespace ResourcesPlugin.Editor
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
			/*SupportedPlatforms = new[]
			{
				PlatformType.Windows,
				PlatformType.WindowsStore,
				PlatformType.XboxOne,
			},*/
			IsAlpha = false,
			IsBeta = false,
		};

		private FileSystemWatcher _resourcesWatcher;
		private readonly FileSystemWatcherBuffer _resoucesBuffer = new FileSystemWatcherBuffer();

		public string ResourcesPath { get; private set; }
		public string ImportedContentPath { get; private set; }

		public ContentFolder ResourcesContentFolder { get; private set; }
		public ContentFolder ImportedContentFolder { get; private set; }

		//private readonly SortedDictionary<string> _changedFilePaths = new HashSet<string>();

		public override void Initialize()
		{
			base.Initialize();
		}

		/// <inheritdoc />
		public override void InitializeEditor()
		{
			base.InitializeEditor();

			ResourcesPath = StringUtils.NormalizePath(StringUtils.CombinePaths(Globals.ContentFolder, "Resources"));
			ResourcesContentFolder = Editor.ContentDatabase.Find(ResourcesPath) as ContentFolder;

			ImportedContentPath = StringUtils.NormalizePath(StringUtils.CombinePaths(Globals.ContentFolder, "Imported"));

			// TODO: Instantly pick up on the creation of this folder (Editor.ContentDatabase.ItemAdded)
			if (ResourcesContentFolder != null)
			{
				Debug.Log("Watching Resources...");

				if (!Directory.Exists(ImportedContentPath))
				{
					Directory.CreateDirectory(ImportedContentPath);
				}

				// TODO: Full sync!
				if (ImportedContentFolder?.ParentFolder != null)
				{
					Editor.ContentDatabase.RefreshFolder(Editor.ContentDatabase.ProjectContent.Folder, false);
				}
				ImportedContentFolder = Editor.ContentDatabase.Find(ImportedContentPath) as ContentFolder;

				// TODO: Watch out, the FileSystemWatcher reports too many events (e.g. moving a file to a new folder = 2 extra "OnChanged" events)

				_resourcesWatcher = new FileSystemWatcher();
				_resourcesWatcher.Path = ResourcesPath;
				_resourcesWatcher.NotifyFilter = NotifyFilters.LastWrite;
				_resourcesWatcher.Filter = "*.*";
				_resourcesWatcher.IncludeSubdirectories = true;
				_resourcesWatcher.EnableRaisingEvents = true;
				_resourcesWatcher.InternalBufferSize = 8192; // Something large, the max is 65536

				_resourcesWatcher.Created += _resoucesBuffer.AddEvent;
				_resourcesWatcher.Changed += _resoucesBuffer.AddEvent;
				_resourcesWatcher.Renamed += _resoucesBuffer.AddEvent;
				_resourcesWatcher.Deleted += _resoucesBuffer.AddEvent;

				_resourcesWatcher.Error += OnError;

				_resoucesBuffer.Created += OnCreated;
				_resoucesBuffer.Changed += OnChanged;
				_resoucesBuffer.Deleted += OnDeleted;

				_resoucesBuffer.Enabled = true;
			}
		}

		private void OnCreated(string fullPath)
		{
			Debug.Log("Created: " + fullPath);
			string newPath = GetNewPath(fullPath);
			if (File.Exists(newPath))
			{
				Editor.ContentImporting.Reimport(GetBinaryAssetItem(newPath));
			}
			else
			{
				var newContentFolder = GetOrCreateFolder(newPath);
				Editor.Windows.MainWindow.BringToFront();
				Editor.ContentImporting.Import(fullPath, newContentFolder);
			}
		}

		private void OnChanged(string fullPath)
		{
			// Ignore directory changes
			if (Directory.Exists(fullPath)) return;

			Debug.Log("Changed: " + fullPath);
			string newPath = GetNewPath(fullPath);
			if (File.Exists(newPath))
			{
				Editor.ContentImporting.Reimport(GetBinaryAssetItem(newPath));
			}
			else
			{
				Debug.Log("Changed but import it? Huh?");
				var newContentFolder = GetOrCreateFolder(newPath);
				Editor.Windows.MainWindow.BringToFront();
				Editor.ContentImporting.Import(fullPath, newContentFolder);
			}
		}

		private void OnDeleted(string fullPath)
		{
			Debug.Log("Deleted: " + fullPath);
			string toDelete = StringUtils.GetPathWithoutExtension(GetNewPath(fullPath)) + ".flax";
			var item = GetBinaryAssetItem(toDelete);
			if (item != null)
			{
				Editor.ContentDatabase.Delete(item);
			}
			else
			{
				Debug.Log("not deleted");
			}
		}

		private void OnError(object sender, ErrorEventArgs e)
		{
			Debug.Log("File System Watcher Error: " + e.GetException());
		}

		private string GetResourcesRelativePath(string path)
		{
			path = StringUtils.NormalizePath(path);
			return path.Replace(ResourcesPath, "").TrimStart(new char[] { '\\', '/' });
		}

		private string GetNewPath(string importedResourcePath)
		{
			return StringUtils.NormalizePath(
					StringUtils.CombinePaths(ImportedContentPath, GetResourcesRelativePath(importedResourcePath))
				);
		}

		private ContentFolder GetOrCreateFolder(string path)
		{
			path = Path.GetDirectoryName(Path.GetFullPath(path));
			Directory.CreateDirectory(path);

			// TODO: Optimize this:
			Editor.ContentDatabase.RefreshFolder(ImportedContentFolder, true);

			return Editor.ContentDatabase.Find(path) as ContentFolder;
		}

		private BinaryAssetItem GetBinaryAssetItem(string path)
		{
			return Editor.ContentDatabase.Find(path) as BinaryAssetItem;
		}

		/// <inheritdoc />
		public override void Deinitialize()
		{
			if (_resourcesWatcher != null)
			{
				_resourcesWatcher.Created -= _resoucesBuffer.AddEvent;
				_resourcesWatcher.Changed -= _resoucesBuffer.AddEvent;
				_resourcesWatcher.Renamed -= _resoucesBuffer.AddEvent;
				_resourcesWatcher.Deleted -= _resoucesBuffer.AddEvent;

				_resourcesWatcher.Error -= OnError;
			}
			_resourcesWatcher?.Dispose();

			_resoucesBuffer.Created -= OnCreated;
			_resoucesBuffer.Changed -= OnChanged;
			_resoucesBuffer.Deleted -= OnDeleted;

			_resoucesBuffer.Dispose();

			base.Deinitialize();
		}
	}
}