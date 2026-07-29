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
using OpenRA.Traits;

namespace OpenRA.WasmProbe
{
	// Phase W3g milestone: construct the engine's real ModData in-wasm over
	// the MEMFS-staged mod tree (InstalledMods discovery -> ModData ctor:
	// ObjectCreator, package loaders, ContentInstallerFileSystem mounts,
	// FluentProvider), set the STATIC Game.ModData, and cash in the assertion
	// W3e had to defer: the FULL E1 actor — every ra trait plus our overlays —
	// materialized as live TraitInfos (HitShape included, which requires the
	// Game.ModData global). Runs after MemfsDemo (EngineDir already overridden).
	internal static class ModDataDemo
	{
		public static void Run()
		{
			var modsRoot = Platform.ResolvePath("^EngineDir|mods");
			var installed = new InstalledMods([modsRoot], []);
			Console.WriteLine($"[probe] step: InstalledMods discovered: {string.Join(", ", installed.Keys.OrderBy(k => k))}");
			if (!installed.ContainsKey("spaceage") || !installed.ContainsKey("ra"))
				throw new InvalidOperationException("InstalledMods did not discover both ra and spaceage");

			ModData modData;
			try
			{
				modData = new ModData(installed["spaceage"], installed);
				Console.WriteLine("[probe] step: ModData constructed (loaders + mounts + fluent OK)");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL ModData ctor: {e}");
				throw;
			}

			Game.ModData = modData;
			Console.WriteLine("[probe] step: static Game.ModData set");

			// The deferred W3e assertion, now with the global in place: the
			// COMPLETE merged E1, materialized through ModData's own
			// ObjectCreator — including HitShapeInfo.LoadShape, which builds
			// sub-objects via Game.CreateObject<IHitShape>.
			try
			{
				var sources = modData.Manifest.Rules.Select(path =>
				{
					using var stream = modData.DefaultFileSystem.Open(path);
					return (System.Collections.Generic.IEnumerable<MiniYamlNode>)MiniYaml.FromStream(stream, path).ToList();
				}).ToList();

				var merged = MiniYaml.Merge(sources);
				var e1Node = merged.First(n => n.Key == "E1");
				var actor = new ActorInfo(modData.ObjectCreator, "e1", e1Node.Value);
				var traits = actor.TraitInfos<TraitInfo>().ToList();

				var hitShape = traits.FirstOrDefault(t => t.GetType().Name == "HitShapeInfo");
				var oxygen = actor.TraitInfoOrDefault<OpenRA.Mods.Common.Traits.OxygenInfo>();
				if (hitShape == null)
					throw new InvalidOperationException("Full E1 is missing HitShapeInfo — Game.ModData path not exercised");
				if (oxygen == null || oxygen.Capacity != 5000)
					throw new InvalidOperationException("Full E1 is missing OxygenInfo(5000)");
				if (traits.Count < 90)
					throw new InvalidOperationException($"Full E1 materialized only {traits.Count} traits");

				Console.WriteLine($"[probe] W3g SUCCESS: live ModData + Game.ModData set; FULL E1 materialized {traits.Count} TraitInfos (HitShape via Game.CreateObject, Oxygen(5000), DamagedByVacuum) in-wasm");
			}
			catch (Exception e) when (e is not InvalidOperationException)
			{
				Console.WriteLine($"[probe] STEP-FAIL full-actor materialization: {e}");
				throw;
			}

			// Stretch (non-gating): how far does the full default ruleset get?
			try
			{
				var rules = modData.DefaultRules;
				Console.WriteLine($"[probe] stretch: DefaultRules loaded {rules.Actors.Count} actors");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] stretch: DefaultRules not yet loadable ({e.GetType().Name}: {FirstLine(e.Message)}) — expected until art/content stages exist");
			}
		}

		static string FirstLine(string s)
		{
			var i = s.IndexOf('\n');
			return i < 0 ? s : s[..i];
		}
	}
}
