#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenRA.FileSystem;

namespace OpenRA.Platforms.Browser
{
	// Phase W3e: an in-memory IReadOnlyPackage backing the VFS-over-HTTP
	// design — files are fetched by the host and staged here, then mounted
	// into the engine's real FileSystem exactly like a folder or archive.
	public sealed class MemoryPackage : IReadOnlyPackage
	{
		readonly Dictionary<string, byte[]> files = [];

		public MemoryPackage(string name)
		{
			Name = name;
		}

		public string Name { get; }
		public IEnumerable<string> Contents => files.Keys;

		public void Add(string filename, byte[] contents)
		{
			files[filename] = contents;
		}

		public void AddText(string filename, string contents)
		{
			files[filename] = Encoding.UTF8.GetBytes(contents);
		}

		public Stream GetStream(string filename)
		{
			return files.TryGetValue(filename, out var contents) ? new MemoryStream(contents) : null;
		}

		public bool Contains(string filename)
		{
			return files.ContainsKey(filename);
		}

		public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context)
		{
			return null;
		}

		public void Dispose() { }
	}
}
