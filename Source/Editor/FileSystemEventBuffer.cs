using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlaxEngine;

namespace ResourcesPlugin.Source.Editor
{
	/// <summary>
	/// Buffers the events of a <see cref="FileSystemWatcher"/>
	/// </summary>
	public class FileSystemEventBuffer : IDisposable
	{
		private Dictionary<string, WatcherChangeTypes> _changedPaths = new Dictionary<string, WatcherChangeTypes>();
		private Dictionary<string, WatcherChangeTypes> _currentChangedPaths = new Dictionary<string, WatcherChangeTypes>();

		private readonly System.Timers.Timer _timer = new System.Timers.Timer(500);

		public double EventInterval
		{
			get => _timer.Interval;
			set => _timer.Interval = value;
		}

		public bool Enabled
		{
			get
			{
				return _timer.Enabled;
			}
			set
			{
				if (_timer.Enabled != value)
				{
					if (value)
					{
						_timer.Elapsed += _timer_Elapsed;
					}
					else
					{
						_timer.Elapsed -= _timer_Elapsed;
					}

					_timer.Enabled = value;
				}
			}
		}

		private void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			RaiseEvents();
		}

		public void ChangeEvent(object sender, RenamedEventArgs e)
		{
			_changedPaths.Add(e.OldFullPath, WatcherChangeTypes.Deleted);
			_changedPaths.Add(e.FullPath, WatcherChangeTypes.Created);
		}

		public void ChangeEvent(object sender, FileSystemEventArgs e)
		{
			//WatcherChangeTypes
			string path = e.FullPath;

			if (_changedPaths.TryGetValue(path, out WatcherChangeTypes changeType))
			{
				_changedPaths[path] = CombineChangeTypes(changeType, e.ChangeType);
			}
			else
			{
				_changedPaths.Add(path, e.ChangeType);
			}
		}

		private WatcherChangeTypes CombineChangeTypes(WatcherChangeTypes oldChangeType, WatcherChangeTypes newChangeType)
		{
			switch (newChangeType)
			{
			case WatcherChangeTypes.Created:
			{
				if (oldChangeType == WatcherChangeTypes.Deleted) return WatcherChangeTypes.Changed; // TODO: Is this a good idea?
				else return WatcherChangeTypes.Created;
			}
			case WatcherChangeTypes.Changed:
			{
				return WatcherChangeTypes.Changed;
			}
			case WatcherChangeTypes.Deleted:
			{
				return WatcherChangeTypes.Deleted;
			}
			default:
			{
				throw new Exception("Unknown change type");
			}
			}
		}

		public void RaiseEvents()
		{
			_currentChangedPaths.Clear();

			Dictionary<string, WatcherChangeTypes> temp = _changedPaths;
			_changedPaths = _currentChangedPaths;
			_currentChangedPaths = temp;

			// Now raise all the events
			foreach (var item in _currentChangedPaths)
			{
				switch (item.Value)
				{
				case WatcherChangeTypes.Created:
				{
					Created?.Invoke(item.Key);
					break;
				}
				case WatcherChangeTypes.Changed:
				{
					Changed?.Invoke(item.Key);
					break;
				}
				case WatcherChangeTypes.Deleted:
				{
					Deleted?.Invoke(item.Key);
					break;
				}
				default:
				{
					throw new Exception("Unknown change type");
				}
				}
			}
		}

		public event Action<string> Created;

		public event Action<string> Changed;

		public event Action<string> Deleted;

		#region IDisposable Support

		private bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects).
					Enabled = false;
					_timer?.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

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