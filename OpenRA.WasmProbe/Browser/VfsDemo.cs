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
using System.Linq;
using System.Threading.Tasks;
using OpenRA.Platforms.Browser;
using OpenRA.Traits;

namespace OpenRA.WasmProbe
{
	// Phase W3e milestone: the engine's real VFS and rules pipeline, in-wasm:
	//   host fetch -> MemoryPackage -> FileSystem.Mount ("ra|", "spaceage|")
	//   -> Manifest -> ObjectCreator (resident-assembly fallback)
	//   -> MiniYaml.Merge of ALL manifest rules (Inherits resolution included)
	//   -> new ActorInfo(...) materializing REAL TraitInfo objects,
	// asserted down to our Oxygen trait's field values.
	internal static class VfsDemo
	{
		public static async Task Run()
		{
			// Stage both mods' rule trees into memory packages via the host.
			var spaceagePackage = new MemoryPackage("spaceage");
			var raPackage = new MemoryPackage("ra");
			var staged = 0;
			foreach (var line in (await WebGL.FetchText("probe-data/file-list.txt")).Split('\n'))
			{
				var path = line.Trim();
				if (path.Length == 0)
					continue;

				var text = await WebGL.FetchText($"probe-data/{path}");
				if (path.StartsWith("spaceage/", StringComparison.Ordinal))
					spaceagePackage.AddText(path["spaceage/".Length..], text);
				else if (path.StartsWith("ra/", StringComparison.Ordinal))
					raPackage.AddText(path["ra/".Length..], text);

				staged++;
			}

			Console.WriteLine($"[probe] staged {staged} files into memory packages");

			// The engine's real Manifest + FileSystem, over our packages.
			var manifest = new Manifest("spaceage", spaceagePackage);
			var fileSystem = new OpenRA.FileSystem.FileSystem("spaceage", null, []);
			fileSystem.Mount(raPackage, "ra");
			fileSystem.Mount(spaceagePackage, "spaceage");

			using (var testStream = fileSystem.Open("spaceage|rules/spaceage-defaults.yaml"))
				if (testStream == null || testStream.Length == 0)
					throw new InvalidOperationException("VFS pipe-open of spaceage|rules/spaceage-defaults.yaml failed");

			Console.WriteLine("[probe] engine FileSystem mounted; pipe-prefixed open OK");

			// ObjectCreator over the manifest's assembly list (Mods.Common +
			// Mods.Cnc resolve via the resident-assembly fallback in-wasm).
			// Step-labelled with full exception dumps: wasm trims exception
			// resource strings, so we must surface our own diagnostics.
			ObjectCreator objectCreator;
			try
			{
				objectCreator = new ObjectCreator(manifest, null);
				Console.WriteLine("[probe] step: ObjectCreator constructed");
				var oxygenType = objectCreator.FindType("OxygenInfo");
				Console.WriteLine($"[probe] step: FindType(OxygenInfo) -> {oxygenType?.FullName ?? "NULL"}");
				if (oxygenType == null)
					throw new InvalidOperationException("ObjectCreator cannot resolve OxygenInfo from resident assemblies");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL ObjectCreator: {e}");
				throw;
			}

			// Merge EVERY rules file the manifest lists, through the VFS —
			// the same load the desktop game performs (Inherits included).
			System.Collections.Generic.List<MiniYamlNode> merged;
			try
			{
				var sources = new System.Collections.Generic.List<System.Collections.Generic.IEnumerable<MiniYamlNode>>();
				foreach (var path in manifest.Rules)
				{
					using var stream = fileSystem.Open(path);
					if (stream == null)
						throw new InvalidOperationException($"VFS returned null stream for {path}");

					sources.Add(MiniYaml.FromStream(stream, path).ToList());
				}

				Console.WriteLine($"[probe] step: {sources.Count} rule files loaded via VFS");
				merged = MiniYaml.Merge(sources);
				Console.WriteLine($"[probe] step: merged {merged.Count} top-level nodes");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL rules load/merge: {e}");
				throw;
			}

			var e1Node = merged.First(n => n.Key == "E1");

			// Materialize the actor: every trait yaml node becomes a live
			// TraitInfo instance via the engine's FieldLoader.
			System.Collections.Generic.List<TraitInfo> traits;
			ActorInfo actor;
			try
			{
				actor = new ActorInfo(objectCreator, "e1", e1Node.Value);
				traits = actor.TraitInfos<TraitInfo>().ToList();
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL ActorInfo('e1'): {e}");
				Console.WriteLine($"[probe] E1 trait keys were: {string.Join(", ", e1Node.Value.Nodes.Select(n => n.Key))}");
				throw;
			}

			var oxygen = actor.TraitInfoOrDefault<OpenRA.Mods.Common.Traits.OxygenInfo>();
			if (oxygen == null)
				throw new InvalidOperationException("Materialized E1 has no OxygenInfo");
			if (oxygen.Capacity != 5000 || oxygen.DrainRate != 3)
				throw new InvalidOperationException($"OxygenInfo fields wrong: Capacity={oxygen.Capacity} DrainRate={oxygen.DrainRate}");

			var vacuum = actor.TraitInfoOrDefault<OpenRA.Mods.Common.Traits.DamagedByVacuumInfo>();
			if (vacuum == null || vacuum.Damage != 350)
				throw new InvalidOperationException("Materialized E1 has no DamagedByVacuumInfo(350)");

			Console.WriteLine($"[probe] W3e SUCCESS: ActorInfo('e1') materialized {traits.Count} live TraitInfos via VFS incl. Oxygen(Capacity=5000, Drain=3) + DamagedByVacuum(350)");
		}
	}
}
