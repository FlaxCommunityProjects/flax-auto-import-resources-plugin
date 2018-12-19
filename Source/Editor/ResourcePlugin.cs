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

				if (ImportedContentFolder?.ParentFolder != null)
				{
					Editor.ContentDatabase.RefreshFolder(Editor.ContentDatabase.ProjectContent.Folder, false);
				}
				ImportedContentFolder = Editor.ContentDatabase.Find(ImportedContentPath) as ContentFolder;
				Editor.ContentDatabase.ItemAdded += ContentDatabase_ItemAdded;

				// Synchronizes everything
				SynchronizeFolders();

				FileSystemWatcherSetup();
			}
		}

		private void ContentDatabase_ItemAdded(ContentItem contentItem)
		{
			// Hide the files in the Content/Resources folder
			/*if (contentItem.IsChildOf(ResourcesContentFolder))
			{
				Debug.Log("child");
				contentItem.Thumbnail = ResourcesContentFolder.Thumbnail;
				contentItem.Width = 0;
				contentItem.Height = 0;
				contentItem.RefreshThumbnail();
				contentItem.PerformLayout(true);

				//contentItem.BackgroundColor = Color.Red;
				//contentItem. = "SHORT";
				//contentItem.ParentFolder = ResourcesContentFolder;
			}*/
		}

		private void SynchronizeFolders()
		{
			if (ImportedContentFolder == null || ResourcesContentFolder == null) return;

			foreach (var importedFile in ImportedContentFolder.GetChildrenRecursive())
			{
				// TODO: Optimize this
				if (importedFile is BinaryAssetItem binaryAssetItem)
				{
					binaryAssetItem.GetImportPath(out string importPath);
					bool hasSource = !string.IsNullOrEmpty(importPath) && File.Exists(importPath);
					if (!hasSource)
					{
						Editor.ContentDatabase.Delete(binaryAssetItem);
					}
				}
			}

			foreach (var resourceFile in ResourcesContentFolder.GetChildrenRecursive())
			{
				// TODO: If it's newer, reimport it
				if (!File.Exists(GetNewPath(resourceFile.Path)))
				{
					OnCreated(resourceFile.Path);
				}
			}
		}

		private void FileSystemWatcherSetup()
		{
			// Create the watcher
			_resourcesWatcher = new FileSystemWatcher
			{
				Path = ResourcesPath,
				NotifyFilter = NotifyFilters.LastWrite,
				Filter = "*.*",
				IncludeSubdirectories = true,
				InternalBufferSize = 8192 // Something large, the max is 65536
			};

			// Hook up the events
			_resourcesWatcher.Created += _resoucesBuffer.AddEvent;
			_resourcesWatcher.Changed += _resoucesBuffer.AddEvent;
			_resourcesWatcher.Renamed += _resoucesBuffer.AddEvent;
			_resourcesWatcher.Deleted += _resoucesBuffer.AddEvent;

			_resourcesWatcher.Error += OnError;

			_resoucesBuffer.Created += OnCreated;
			_resoucesBuffer.Changed += OnChanged;
			_resoucesBuffer.Deleted += OnDeleted;

			// Enable
			_resourcesWatcher.EnableRaisingEvents = true;
			_resoucesBuffer.Enabled = true;
		}

		private void OnCreated(string fullPath)
		{
			Debug.Log("Created: " + fullPath);
			string newPath = GetNewPath(fullPath);
			var newContentFolder = GetOrCreateFolder(newPath);
			if (File.Exists(newPath))
			{
				Editor.ContentImporting.Import(fullPath, newContentFolder, true);
			}
			else
			{
				Editor.Windows.MainWindow.BringToFront();
				Editor.ContentImporting.Import(fullPath, newContentFolder);
			}
		}

		private void OnChanged(string fullPath)
		{
			// Ignore directory changes (not sure about this)
			if (Directory.Exists(fullPath)) return;

			Debug.Log("Changed: " + fullPath);
			string newPath = GetNewPath(fullPath);
			var newContentFolder = GetOrCreateFolder(newPath);
			if (File.Exists(newPath))
			{
				Editor.ContentImporting.Import(fullPath, newContentFolder, true);
			}
			else
			{
				Debug.Log("Changed but import it? Huh?");
				Editor.Windows.MainWindow.BringToFront();
				Editor.ContentImporting.Import(fullPath, newContentFolder);
			}
		}

		private void OnDeleted(string fullPath)
		{
			Debug.Log("Deleted: " + fullPath);
			var item = GetBinaryAssetItem(GetNewPath(fullPath));
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
			// Get the new absolute path
			string newPath = StringUtils.CombinePaths(ImportedContentPath, GetResourcesRelativePath(importedResourcePath));

			// Fix the extension
			newPath = StringUtils.GetPathWithoutExtension(newPath) + ".flax";

			// Return a properly normalized path
			return StringUtils.NormalizePath(newPath);
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
			Editor.ContentDatabase.ItemAdded -= ContentDatabase_ItemAdded;

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