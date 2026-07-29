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

namespace OpenRA.WasmProbe
{
	// Phase W1 probe: does OpenRA.Game EXECUTE inside the .NET wasm runtime?
	// (W0 proved it publishes; this runs under Node in CI — see wasm-port.yml.)
	// Exercises the pure-managed data layer the real engine boots through:
	// fixed-point world geometry and the MiniYAML rules parser. No platform,
	// no rendering, no filesystem. See WASM-PORT-PLAN.md.
	public static class Program
	{
		const string Rules =
			"E1:\n" +
			"\tOxygen:\n" +
			"\t\tCapacity: 5000\n" +
			"\t\tDrainRate: 3\n" +
			"\tDamagedByVacuum:\n" +
			"\t\tDamage: 350\n" +
			"DOME:\n" +
			"\tProximityExternalCondition@LIFESUPPORT:\n" +
			"\t\tCondition: pressurised\n" +
			"\t\tRange: 8c0\n";

		public static async Task Main()
		{
			// Phase W5: the deployed page boots straight into the game; the
			// CI harness (and Node) runs the full probe gate ladder below.
			if (WebGL.GetBootMode() == "play")
			{
				await PlayMode.Run();
				return;
			}

			// 1. Fixed-point world math (the sim's foundation).
			var a = new WPos(1024, 2048, 0);
			var b = new WPos(4096, 0, 512);
			Console.WriteLine($"[probe] OpenRA.Game assembly: {typeof(WPos).Assembly.GetName().Name}");
			Console.WriteLine($"[probe] WPos distance math OK: {(b - a).Length}");
			Console.WriteLine($"[probe] WAngle from facing 128: {WAngle.FromFacing(128)}");

			// 2. MiniYAML: parse a SpaceAge-flavoured rules snippet and walk it.
			var nodes = MiniYaml.FromString(Rules, "probe-rules").ToList();
			var e1 = nodes.First(n => n.Key == "E1");
			var oxygen = e1.Value.Nodes.First(n => n.Key == "Oxygen");
			var capacity = oxygen.Value.Nodes.First(n => n.Key == "Capacity").Value.Value;
			var dome = nodes.First(n => n.Key == "DOME");
			var lifeSupport = dome.Value.Nodes.First(n => n.Key.StartsWith("ProximityExternalCondition", StringComparison.Ordinal));
			Console.WriteLine($"[probe] MiniYAML parsed {nodes.Count} actors; E1.Oxygen.Capacity={capacity}");
			Console.WriteLine($"[probe] MiniYAML trait key with suffix: {lifeSupport.Key}");

			if (capacity != "5000")
				throw new InvalidOperationException("MiniYAML round-trip mismatch");

			// 3. Phase W2 (browser host only): WebGL2 clear + textured quad,
			// pixel-verified. Under Node the stub module reports no DOM.
			// Phase W3c: the same frame again, but through OpenRA's own
			// IPlatform/IGraphicsContext contracts (Platform/ directory).
			if (WebGL.HasDocument())
			{
				QuadDemo.Run();
				InputFontDemo.Run();
				PlatformDemo.Run();
			}
			else
				Console.WriteLine("[probe] no DOM host - skipping WebGL checks (expected under Node)");

			// 4. Phase W3a: fetch REAL mod files (staged under probe-data/ by CI)
			// and push them through the real engine loaders — the same
			// MiniYaml.Merge that builds rulesets on desktop. This proves the
			// VFS-over-HTTP direction: bytes arrive via the host, the engine's
			// data layer does the rest, all in-wasm.
			var manifestText = await WebGL.FetchText("probe-data/mods/spaceage/mod.yaml");
			var manifest = MiniYaml.FromString(manifestText, "mod.yaml").ToList();
			var metadata = manifest.First(n => n.Key == "Metadata");
			var title = metadata.Value.Nodes.First(n => n.Key == "Title").Value.Value;
			var rulesList = manifest.First(n => n.Key == "Rules").Value.Nodes.Select(n => n.Key).ToList();
			Console.WriteLine($"[probe] manifest loaded over host fetch: Title={title}, {rulesList.Count} rule files");
			if (!rulesList.Contains("spaceage|rules/spaceage-defaults.yaml"))
				throw new InvalidOperationException("spaceage overlay missing from manifest Rules");

			var raDefaults = MiniYaml.FromString(
				await WebGL.FetchText("probe-data/mods/ra/rules/defaults.yaml"), "defaults.yaml", discardCommentsAndWhitespace: true);
			var overlay = MiniYaml.FromString(
				await WebGL.FetchText("probe-data/mods/spaceage/rules/spaceage-defaults.yaml"), "spaceage-defaults.yaml", discardCommentsAndWhitespace: true);
			var merged = MiniYaml.Merge([raDefaults, overlay]);
			var soldier = merged.First(n => n.Key == "^Soldier");
			var mergedOxygen = soldier.Value.Nodes.First(n => n.Key == "Oxygen");
			var mergedCapacity = mergedOxygen.Value.Nodes.First(n => n.Key == "Capacity").Value.Value;
			if (mergedCapacity != "6000")
				throw new InvalidOperationException($"merge mismatch: ^Soldier Oxygen Capacity = {mergedCapacity}");

			Console.WriteLine($"[probe] W3a SUCCESS: real ra+spaceage rules fetched and merged in-wasm; ^Soldier gains Oxygen (Capacity={mergedCapacity}, {soldier.Value.Nodes.Length} traits total)");

			// 5. Phase W3e: the full VFS + rules-materialization pipeline —
			// MemoryPackages -> engine FileSystem -> Manifest -> ObjectCreator
			// -> ActorInfo with live TraitInfo instances. Host-agnostic.
			await VfsDemo.Run();

			// 6. Phase W3f: MEMFS staging — the engine's STANDARD folder
			// mounting (^EngineDir string mounts -> Folder packages) over the
			// wasm in-memory filesystem. Host-agnostic.
			await MemfsDemo.Run();

			// 7. Phase W3g: live ModData over the MEMFS tree + the static
			// Game.ModData, unlocking full-actor materialization.
			ModDataDemo.Run();

			// 8. Phase W3h (browser only): the engine's own platform factory
			// must select the browser backend by assembly name.
			// 9. Phase W3i-a (browser only): the engine's real Renderer over
			// BrowserPlatform — glsl shaders compiled, frame cycle completed.
			// 10. Phase W3i-b (browser only): the game's real load screen via
			// the engine sprite pipeline — runs last for the CI screenshot.
			if (WebGL.HasDocument())
			{
				PlatformSeamDemo.Run();
				RendererDemo.Run();

				// 10b. Phase W4b (browser only): Web Audio behind ISoundEngine.
				SoundDemo.Run();

				LoadScreenDemo.Run();

				// 11. Phase W3i-c (browser only): the FULL menu via
				// Game.InitializeMod — content-installer UI on missing .mix.
				MenuDemo.Run();
			}

			Console.WriteLine("[probe] SUCCESS: OpenRA.Game core executes under the .NET wasm runtime");
		}
	}
}
