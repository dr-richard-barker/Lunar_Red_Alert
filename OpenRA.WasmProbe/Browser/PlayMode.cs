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
using OpenRA.Widgets;

namespace OpenRA.WasmProbe
{
	// Phase W5: the deployed page's boot path — no gates, no asserts, just
	// the straight boot the CI ladder proved: stage MEMFS -> settings ->
	// mods -> platform -> renderer -> sound -> Game.InitializeMod -> hand
	// the frame to the engine forever. The public site ships the RA freeware
	// content (W5b), so this should land directly in spaceage's own menu
	// rather than diverting into the content-installer mod.
	internal static class PlayMode
	{
		public static async Task Run(string mode = "play")
		{
			Console.WriteLine("[play] staging game files into MEMFS…");
			await MemfsDemo.Run();

			Directory.CreateDirectory(Platform.SupportDir);
			Game.InitializeSettings(Arguments.Empty);
			BrowserBoot.ApplyDefaults();

			var windowSize = await BrowserPlatform.GetWindowSize();
			Game.Settings.Graphics.WindowedSize = new int2(windowSize.Width, windowSize.Height);

			var installed = new InstalledMods([Platform.ResolvePath("^EngineDir|mods")], []);
			typeof(Game).GetProperty(nameof(Game.Mods), BindingFlags.Public | BindingFlags.Static)
				.GetSetMethod(nonPublic: true).Invoke(null, [installed]);

			var platform = Game.CreatePlatform("Browser");
			var manifest = installed["spaceage"];
			Game.Renderer = new Renderer(platform, Game.Settings.Graphics, manifest.RendererConstants.VertexBatchSize);
			Game.Sound = new Sound(platform, Game.Settings.Sound);

			Console.WriteLine("[play] booting Lunar Red Alert…");
			
			var args = new Arguments("Launch.Map=ysmir");
			// In autopilot mode, we could start a specific map or just let the shellmap run
			// but we hide the UI to let the user spectate the shellmap AI battle.
			if (mode == "autopilot")
			{
				// Launching the shellmap as a regular map makes it a full game, 
				// or we can pass a specific map if one exists.
				args = new Arguments("Launch.Map=shellmap");
			}

			Game.InitializeMod(manifest, args);

			if (mode == "autopilot")
			{
				Ui.ResetAll();
			}

			Console.WriteLine($"[play] boot complete — active mod '{Game.ModData?.Manifest.Id}'");

			GameLoop.PlayForever = true;
			GameLoop.Ready = true;
		}
	}
}
