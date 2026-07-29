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

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace OpenRA.WasmProbe
{
	// Phase W3f: make the browser look like a disk. The .NET wasm runtime
	// ships Emscripten's in-memory filesystem (MEMFS), so instead of porting
	// the engine's mounting machinery, we stage the game tree with plain
	// System.IO and let the STANDARD path run unmodified:
	//   Platform.OverrideEngineDir -> "^EngineDir|mods/..." string mounts ->
	//   FileSystem.OpenPackage -> the engine's real Folder package.
	//
	// The tree arrives as ONE zip (CI builds probe-data.zip). Fetching ~730
	// files individually cost hundreds of serial interop round trips and hung
	// the page long enough to starve rAF and screenshots.
	internal static class MemfsDemo
	{
		public const string Root = "/openra/";
		public const string SupportRoot = "/home/web_user/.openra/";

		public static async Task Run()
		{
			var archive = await WebGL.FetchBinary("probe-data.zip");
			Console.WriteLine($"[probe] step: fetched probe-data.zip ({archive.Length / 1024 / 1024} MB)");

			var staged = 0;
			var skippedContent = 0;

			// Game content (supportdir/) is only needed by the browser gates
			// and play mode; a DOM-less host (Node) skips it.
			var stageContent = WebGL.HasDocument();

			using (var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read))
			{
				foreach (var entry in zip.Entries)
				{
					var name = entry.FullName.Replace('\\', '/').TrimStart('.', '/');
					if (name.Length == 0 || name.EndsWith('/'))
						continue;

					if (!stageContent && name.StartsWith("supportdir/", StringComparison.Ordinal))
					{
						skippedContent++;
						continue;
					}

					// Content lands in the user support dir (where the ra
					// manifest's ^SupportDir|Content mounts expect it); the
					// rest under the engine dir. SupportRoot is hardcoded from
					// the W3f recon because touching Platform.SupportDir here
					// would lock EngineDir before the override below.
					var target = name.StartsWith("supportdir/", StringComparison.Ordinal)
						? Path.Combine(SupportRoot, name["supportdir/".Length..])
						: Path.Combine(Root, name);

					Directory.CreateDirectory(Path.GetDirectoryName(target));
					using (var source = entry.Open())
					using (var file = File.Create(target))
						source.CopyTo(file);

					staged++;
				}
			}

			// Round-trip sanity: MEMFS must give back what we wrote.
			var probePath = Path.Combine(Root, "mods/spaceage/rules/spaceage-defaults.yaml");
			if (!File.ReadAllText(probePath).Contains("Oxygen"))
				throw new InvalidOperationException("MEMFS round-trip failed for spaceage-defaults.yaml");

			Console.WriteLine($"[probe] step: {staged} files staged into MEMFS ({skippedContent} content files skipped)");

			// ORDER MATTERS: OverrideEngineDir must run before ANY EngineDir
			// access — InitializeSupportDir reads EngineDir for its
			// portable-install check and would lock it.
			Platform.OverrideEngineDir(Root);
			Console.WriteLine($"[probe] step: EngineDir overridden -> '{Platform.EngineDir}'");
			Console.WriteLine($"[probe] step: Platform.SupportDir = '{Platform.SupportDir}'");

			// Now the ENGINE's standard mount chain, exactly as a manifest uses
			// it: string names with ^EngineDir prefixes -> Folder packages.
			var fileSystem = new OpenRA.FileSystem.FileSystem("spaceage", null, []);
			fileSystem.Mount("^EngineDir|mods/ra", "ra");
			fileSystem.Mount("^EngineDir|mods/spaceage", "spaceage");

			using var stream = fileSystem.Open("spaceage|rules/spaceage-defaults.yaml");
			var nodes = MiniYaml.FromStream(stream, "spaceage-defaults.yaml").ToList();
			if (nodes.All(n => n.Key != "^Soldier"))
				throw new InvalidOperationException("Folder-mounted spaceage-defaults.yaml did not parse ^Soldier");

			using var raStream = fileSystem.Open("ra|rules/defaults.yaml");
			if (raStream == null || raStream.Length == 0)
				throw new InvalidOperationException("Folder-mounted ra|rules/defaults.yaml failed to open");

			Console.WriteLine("[probe] W3f SUCCESS: MEMFS-staged mod tree mounted via the engine's standard ^EngineDir string mounts (real Folder packages) and parsed");
		}
	}
}
