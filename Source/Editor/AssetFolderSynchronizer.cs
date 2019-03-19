using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlaxEditor.Content;
using FlaxEditor.Utilities;
using FlaxEngine;

namespace ResourcesPlugin.Source.Editor
{
	/// <summary>
	/// Keeps 2 folders in sync (raw assets and imported assets)
	///
	/// TODO: It only cares about the files. It doesn't delete folders.
	/// </summary>
	public class AssetFolderSynchronizer : IDisposable
	{
		private FlaxEditor.Editor _editor;
		private FileSystemEventBuffer _eventBuffer = new FileSystemEventBuffer();
		private FileSystemWatcher _fileSystemWatcher;

		public AssetFolderSynchronizer(string assetsPath, string importedAssetsPath, FlaxEditor.Editor editor)
		{
			AssetsPath = Path.GetFullPath(assetsPath);
			ImportedAssetsPath = Path.GetFullPath(importedAssetsPath);
			_editor = editor;

			// Check if imported assets are in the content folder
			if (!ImportedAssetsPath.StartsWith(Path.GetFullPath(_editor.ContentDatabase.ProjectContent.Path)))
			{
				throw new ArgumentException("Imported assets need to end up in the content folder", nameof(importedAssetsPath));
			}

			// Make sure that the directories exists
			if (!Directory.Exists(AssetsPath))
			{
				Directory.CreateDirectory(AssetsPath);
			}

			if (!Directory.Exists(ImportedAssetsPath))
			{
				Directory.CreateDirectory(ImportedAssetsPath);
				_editor.ContentDatabase.RefreshFolder(_editor.ContentDatabase.ProjectContent.Folder, true);
			}

			// Get the imported assets folder
			ImportedAssetsContentFolder = _editor.ContentDatabase.Find(importedAssetsPath) as ContentFolder;

			SynchronizeFolders();
			FileSystemWatcherSetup();
		}

		public string AssetsPath { get; }
		public string ImportedAssetsPath { get; }
		public ContentFolder ImportedAssetsContentFolder { get; }

		public string RawAssetToImportedPath(string assetPath)
		{
			assetPath = Path.GetFullPath(assetPath);
			// Get the "relative" path of the asset
			var relativeAssetPath = assetPath.Replace(AssetsPath, "").TrimStart(new char[] { '\\', '/' });

			// Get the new path
			var importedPath = Path.Combine(ImportedAssetsPath, relativeAssetPath);
			// Remember to change the extension
			// Because of this, it's not trivially possible to get back the original path
			importedPath = Path.ChangeExtension(importedPath, ".flax");

			return importedPath;
		}

		private ContentFolder GetOrCreateContentFolder(string path)
		{
			path = Path.GetDirectoryName(Path.GetFullPath(path));
			Directory.CreateDirectory(path);

			// TODO: Optimize this:
			_editor.ContentDatabase.RefreshFolder(ImportedAssetsContentFolder, true);

			return _editor.ContentDatabase.Find(path) as ContentFolder;
		}

		/// <summary>
		/// Fully re-synchronizes the 2 folders
		/// </summary>
		private void SynchronizeFolders()
		{
			if (string.IsNullOrEmpty(ImportedAssetsPath) || string.IsNullOrEmpty(AssetsPath)) return;

			// Delete the imported files that don't have an asset anymore
			foreach (var importedFile in ImportedAssetsContentFolder.GetChildrenRecursive())
			{
				// TODO: Optimize this?
				if (importedFile is BinaryAssetItem binaryAssetItem)
				{
					binaryAssetItem.GetImportPath(out string importPath);
					bool hasSource = !string.IsNullOrEmpty(importPath) && File.Exists(importPath);
					if (!hasSource)
					{
						_editor.ContentDatabase.Delete(binaryAssetItem);
					}
				}
			}

			// (Re)import the changed ones
			foreach (string rawAssetPath in GetFilesRecursive(AssetsPath))
			{
				string importedPath = RawAssetToImportedPath(rawAssetPath);

				// If it hasn't been imported, do it
				if (!File.Exists(importedPath))
				{
					RawAssetCreated(rawAssetPath);
				}
				else
				{
					// If it's newer, reimport it
					FileInfo rawAssetInfo = new FileInfo(rawAssetPath);
					FileInfo importedAssetInfo = new FileInfo(importedPath);
					if (rawAssetInfo.LastWriteTime > importedAssetInfo.LastWriteTime)
					{
						RawAssetChanged(rawAssetPath);
					}
				}
			}
		}

		private void FileSystemWatcherSetup()
		{
			// Create the watcher
			_fileSystemWatcher = new FileSystemWatcher
			{
				Path = AssetsPath,
				NotifyFilter = NotifyFilters.LastWrite,
				Filter = "*.*",
				IncludeSubdirectories = true,
				InternalBufferSize = 8192 // Something large, the max is 65536
			};

			// Hook up the watcher events
			_fileSystemWatcher.Created += _eventBuffer.ChangeEvent;
			_fileSystemWatcher.Changed += _eventBuffer.ChangeEvent;
			_fileSystemWatcher.Renamed += _eventBuffer.ChangeEvent;
			_fileSystemWatcher.Deleted += _eventBuffer.ChangeEvent;

			_fileSystemWatcher.Error += FileSystemWatcherError;

			// Hook up the buffer events
			_eventBuffer.Created += RawAssetCreated;
			_eventBuffer.Changed += RawAssetChanged;
			_eventBuffer.Deleted += RawAssetDeleted;

			// Enable both of them
			_fileSystemWatcher.EnableRaisingEvents = true;
			_eventBuffer.Enabled = true;
		}

		private void RawAssetCreated(string assetPath)
		{
			string importedPath = RawAssetToImportedPath(assetPath);
			var newContentFolder = GetOrCreateContentFolder(importedPath);
			if (File.Exists(importedPath))
			{
				_editor.ContentImporting.Import(assetPath, newContentFolder, true);
			}
			else
			{
				_editor.Windows.MainWindow.BringToFront();
				_editor.ContentImporting.Import(assetPath, newContentFolder);
			}
		}

		private void RawAssetChanged(string assetPath)
		{
			//TODO: Ignore directory changes?? (not sure about this)
			//if (Directory.Exists(assetPath)) return;
			if (Directory.Exists(assetPath))
			{
				Debug.Log(assetPath);
			}
			RawAssetCreated(assetPath);
		}

		private void RawAssetDeleted(string assetPath)
		{
			string importedPath = RawAssetToImportedPath(assetPath);
			var item = _editor.ContentDatabase.Find(importedPath) as BinaryAssetItem;
			if (item != null)
			{
				_editor.ContentDatabase.Delete(item);
			}
		}

		private void FileSystemWatcherError(object sender, ErrorEventArgs e)
		{
			Debug.Log("File System Watcher Error: " + e.GetException());
		}

		// From https://stackoverflow.com/a/929418/3492994
		private static IEnumerable<string> GetFilesRecursive(string path)
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
					Console.Error.WriteLine(ex);
				}
				string[] files = null;
				try
				{
					files = Directory.GetFiles(path);
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine(ex);
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

		#region IDisposable Support

		private bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					// File System Watcher Disposing
					if (_fileSystemWatcher != null)
					{
						_fileSystemWatcher.Created -= _eventBuffer.ChangeEvent;
						_fileSystemWatcher.Changed -= _eventBuffer.ChangeEvent;
						_fileSystemWatcher.Renamed -= _eventBuffer.ChangeEvent;
						_fileSystemWatcher.Deleted -= _eventBuffer.ChangeEvent;

						_fileSystemWatcher.Error -= FileSystemWatcherError;
					}
					_fileSystemWatcher?.Dispose();

					// Event Buffer Disposing
					_eventBuffer.Created -= RawAssetCreated;
					_eventBuffer.Changed -= RawAssetChanged;
					_eventBuffer.Deleted -= RawAssetDeleted;

					_eventBuffer.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}