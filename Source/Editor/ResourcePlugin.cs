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

				// Synchronizes everything
				SynchronizeFolders();

				FileSystemWatcherSetup();
			}
		}

		private void SynchronizeFolders()
		{
			if (!(Directory.Exists(ResourcesPath) && Directory.Exists(ImportedContentPath))) return;

			foreach (var importedFilePath in GetFiles(ImportedContentPath))
			{
			}

			foreach (var resourceFilePath in GetFiles(ResourcesPath))
			{
				//GetNewPath(resourceFilePath)
			}
		}

		// From: https://stackoverflow.com/a/929418/3492994
		private static IEnumerable<string> GetFiles(string path)
		{
			Queue<string> queue = new Queue<string>();
			queue.Enqueue(path);
			while (queue.Count > 0)
			{
				path = queue.Dequeue();
				try
				{
					foreach (string subDir in Directory.GetDirectories(path))
					{
						queue.Enqueue(subDir);
					}
				}
				catch (Exception ex)
				{
					Debug.LogError(ex);
				}
				string[] files = null;
				try
				{
					files = Directory.GetFiles(path);
				}
				catch (Exception ex)
				{
					Debug.LogError(ex);
				}
				if (files != null)
				{
					for (int i = 0; i < files.Length; i++)
					{
						yield return files[i];
					}
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
			if (File.Exists(newPath))
			{
				var item = GetBinaryAssetItem(newPath);
				Reimport(item);
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
				var item = GetBinaryAssetItem(newPath);
				Reimport(item);
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

		private void Reimport(BinaryAssetItem item)
		{
			Editor.ContentImporting.Reimport(item); // TODO: Settings!
													// The window pops up every single time.

			/*var asset = FlaxEngine.Content.LoadAsync(item.ID);
			if (asset is BinaryAsset binaryAsset)
			{
				binaryAsset.WaitForLoaded(100);
				binaryAsset.Reimport();
			}*/
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