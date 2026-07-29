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
using System.Reflection;
using System.Threading.Tasks;
using OpenRA.Platforms.Browser;

namespace OpenRA.WasmProbe
{
	// Phase W5: the deployed page's boot path — no gates, no asserts, just
	// the straight boot the CI ladder proved: stage MEMFS -> settings ->
	// mods -> platform -> renderer -> sound -> Game.InitializeMod -> hand
	// the frame to the engine forever. Without game content staged (the
	// public site ships none), the engine's own flow lands on the
	// content-installer menu — a real OpenRA UI built from repo assets.
	internal static class PlayMode
	{
		public static async Task Run()
		{
			Console.WriteLine("[play] staging game files into MEMFS…");
			await MemfsDemo.Run();

			Directory.CreateDirectory(Platform.SupportDir);
			Game.InitializeSettings(Arguments.Empty);
			BrowserBoot.ApplyDefaults();

			var installed = new InstalledMods([Platform.ResolvePath("^EngineDir|mods")], []);
			typeof(Game).GetProperty(nameof(Game.Mods), BindingFlags.Public | BindingFlags.Static)
				.GetSetMethod(nonPublic: true).Invoke(null, [installed]);

			var platform = Game.CreatePlatform("Browser");
			var manifest = installed["spaceage"];
			Game.Renderer = new Renderer(platform, Game.Settings.Graphics, manifest.RendererConstants.VertexBatchSize);
			Game.Sound = new Sound(platform, Game.Settings.Sound);

			// Diagnostic (cheap, cannot hang): confirm the staged freeware
			// content is where the ra manifest's content check expects it.
			var contentDir = Path.Combine(Platform.SupportDir, "Content", "ra", "v2");
			Console.WriteLine($"[play] diag: content dir exists = {Directory.Exists(contentDir)} ({contentDir})");
			if (Directory.Exists(contentDir))
			{
				var files = Directory.GetFiles(contentDir);
				Console.WriteLine($"[play] diag: {files.Length} files in Content/ra/v2");
				foreach (var f in files)
					Console.WriteLine($"[play] diag: file = {Path.GetFileName(f)}");
			}

			foreach (var f in new[] { "allies.mix", "conquer.mix", "temperat.mix" })
			{
				var p = Path.Combine(contentDir, f);
				Console.WriteLine($"[play] diag: File.Exists({f}) = {File.Exists(p)}");
			}

			Console.WriteLine("[play] booting Lunar Red Alert…");
			Game.InitializeMod(manifest, Arguments.Empty);
			Console.WriteLine($"[play] boot complete — active mod '{Game.ModData?.Manifest.Id}'");

			GameLoop.PlayForever = true;
			GameLoop.Ready = true;
		}
	}
}
