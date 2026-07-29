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
using System.Linq;
using System.Threading.Tasks;

namespace OpenRA.WasmProbe
{
	// Phase W3f milestone: make the browser look like a disk. The .NET wasm
	// runtime ships Emscripten's in-memory filesystem (MEMFS), so instead of
	// porting the engine's mounting machinery, we stage the fetched mod tree
	// with plain System.IO and let the STANDARD path run unmodified:
	//   Platform.OverrideEngineDir -> "^EngineDir|mods/..." string mounts ->
	//   FileSystem.OpenPackage -> the engine's real Folder package.
	// This is the exact chain ModData's FileSystemLoader uses (W3g).
	internal static class MemfsDemo
	{
		public const string Root = "/openra/";

		public static async Task Run()
		{
			// Recreate the full directory skeleton first: the manifest
			// Folder-mounts some dirs that hold no text assets (e.g. uibits),
			// and a mount of a missing dir fails. probe-data mirrors the
			// engine root (mods/ + glsl/), staged verbatim under /openra/.
			foreach (var line in (await WebGL.FetchText("probe-data/dir-list.txt")).Split('\n'))
			{
				var dir = line.Trim();
				if (dir.Length == 0)
					continue;

				Directory.CreateDirectory(dir.StartsWith("supportdir/", StringComparison.Ordinal)
					? Path.Combine("/home/web_user/.openra/", dir["supportdir/".Length..])
					: Path.Combine(Root, dir));
			}

			// Stage every probe-data file into MEMFS under /openra/.
			var staged = 0;
			foreach (var line in (await WebGL.FetchText("probe-data/file-list.txt")).Split('\n'))
			{
				var path = line.Trim();
				if (path.Length == 0 || path.EndsWith("-list.txt", StringComparison.Ordinal))
					continue;

				// W4a: content lands under the user support dir (where the ra
				// manifest's ^SupportDir|Content/ra/v2 mounts expect it).
				// Hardcoded Emscripten home (proven by W3f recon) because
				// touching Platform.SupportDir here would lock EngineDir
				// before the override below.
				var target = path.StartsWith("supportdir/", StringComparison.Ordinal)
					? Path.Combine("/home/web_user/.openra/", path["supportdir/".Length..])
					: Path.Combine(Root, path);
				Directory.CreateDirectory(Path.GetDirectoryName(target));

				// Binary-safe staging (fonts/PNGs/.mix would corrupt through text).
				File.WriteAllBytes(target, await WebGL.FetchBinary($"probe-data/{path}"));
				staged++;
			}

			// Round-trip sanity: MEMFS must give back what we wrote.
			var probePath = Path.Combine(Root, "mods/spaceage/rules/spaceage-defaults.yaml");
			if (!File.ReadAllText(probePath).Contains("Oxygen"))
				throw new InvalidOperationException("MEMFS round-trip failed for spaceage-defaults.yaml");

			Console.WriteLine($"[probe] step: {staged} files staged into MEMFS under {Root}mods/");

			// ORDER MATTERS: OverrideEngineDir must run before ANY EngineDir
			// access — and InitializeSupportDir reads EngineDir for its
			// portable-install check, so even printing SupportDir first would
			// lock the engine dir (it did: that was this gate's first failure).
			Platform.OverrideEngineDir(Root);
			Console.WriteLine($"[probe] step: EngineDir overridden -> '{Platform.EngineDir}'");
			Console.WriteLine($"[probe] step: Platform.BinDir = '{Platform.BinDir}'");
			try
			{
				Console.WriteLine($"[probe] step: Platform.SupportDir = '{Platform.SupportDir}'");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] step: Platform.SupportDir threw {e.GetType().Name} (W3g will need a home for settings/logs)");
			}

			// Now the ENGINE's standard mount chain, exactly as a manifest uses
			// it: string names with ^EngineDir prefixes -> Folder packages.
			var fileSystem = new OpenRA.FileSystem.FileSystem("spaceage", null, []);
			fileSystem.Mount("^EngineDir|mods/ra", "ra");
			fileSystem.Mount("^EngineDir|mods/spaceage", "spaceage");

			using var stream = fileSystem.Open("spaceage|rules/spaceage-defaults.yaml");
			var nodes = MiniYaml.FromStream(stream, "spaceage-defaults.yaml").ToList();
			var soldier = nodes.FirstOrDefault(n => n.Key == "^Soldier");
			if (soldier == null)
				throw new InvalidOperationException("Folder-mounted spaceage-defaults.yaml did not parse ^Soldier");

			using var raStream = fileSystem.Open("ra|rules/defaults.yaml");
			if (raStream == null || raStream.Length == 0)
				throw new InvalidOperationException("Folder-mounted ra|rules/defaults.yaml failed to open");

			Console.WriteLine("[probe] W3f SUCCESS: MEMFS-staged mod tree mounted via the engine's standard ^EngineDir string mounts (real Folder packages) and parsed");
		}
	}
}
