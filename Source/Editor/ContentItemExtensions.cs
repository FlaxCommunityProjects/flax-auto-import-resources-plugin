using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlaxEditor.Content;
using FlaxEngine;

namespace ResourcesPlugin.Source.Editor
{
	internal static class ContentItemExtensions
	{
		// From: https://stackoverflow.com/a/929418/3492994
		internal static IEnumerable<ContentItem> GetChildrenRecursive(this ContentFolder contentFolder)
		{
			Queue<ContentFolder> queue = new Queue<ContentFolder>();
			queue.Enqueue(contentFolder);
			while (queue.Count > 0)
			{
				contentFolder = queue.Dequeue();
				try
				{
					foreach (var subDir in contentFolder.Children)
					{
						if (subDir is ContentFolder folder)
						{
							queue.Enqueue(folder);
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogError(ex);
				}
				List<ContentItem> files = null;
				try
				{
					files = contentFolder.Children;
				}
				catch (Exception ex)
				{
					Debug.LogError(ex);
				}
				if (files != null)
				{
					for (int i = 0; i < files.Count; i++)
					{
						yield return files[i];
					}
				}
			}
		}
	}
}