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
using System.Reflection;
using OpenRA.Widgets;

namespace OpenRA.WasmProbe
{
	// Phase W3i-c milestone: the FULL menu attempt through the engine's own
	// Game.InitializeMod. With no EA .mix content staged, the real boot flow
	// is: LoadScreen.BeforeLoad -> IFileSystemExternalContent
	// .InstallContentIfRequired -> switch to the content-installer mod
	// (ra-content) -> its chrome/widget UI — a genuine OpenRA menu built
	// entirely from repo assets. Runs LAST: the CI screenshot IS the menu.
	internal static class MenuDemo
	{
		sealed class NullInputHandler : IInputHandler
		{
			public void ModifierKeys(Modifiers mods) { }
			public void OnKeyInput(KeyInput input) { }
			public void OnMouseInput(MouseInput input) { }
			public void OnTextInput(string text) { }
		}

		public static void Run()
		{
			// Game.Mods has a private setter (Game.Initialize owns it on
			// desktop); the probe sets it via reflection. The real page boot
			// will drive Game.Initialize itself and not need this.
			var modsProperty = typeof(Game).GetProperty(nameof(Game.Mods), BindingFlags.Public | BindingFlags.Static);
			var setter = modsProperty?.GetSetMethod(nonPublic: true);
			if (setter != null)
				setter.Invoke(null, [ModDataDemo.Installed]);
			else
				throw new InvalidOperationException("Cannot set Game.Mods (setter not found)");

			Console.WriteLine("[probe] step: Game.Mods set via reflection");

			try
			{
				Game.InitializeMod(ModDataDemo.Installed["spaceage"], Arguments.Empty);
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL Game.InitializeMod: {e}");
				throw;
			}

			var bootedMod = Game.ModData?.Manifest.Id ?? "(null)";
			var rootWidgets = Ui.Root.Children.Count;
			Console.WriteLine($"[probe] step: InitializeMod completed; active mod '{bootedMod}', {rootWidgets} root widgets");

			// Render one real UI frame so the screenshot captures the menu.
			try
			{
				Game.Renderer.BeginUI();
				Ui.Draw();
				Game.Renderer.EndFrame(new NullInputHandler());
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL menu frame: {e}");
				throw;
			}

			if (rootWidgets == 0)
				throw new InvalidOperationException("No widgets in the UI root after InitializeMod");

			var widgetNames = string.Join(", ", Ui.Root.Children.Select(w => w.Id ?? w.GetType().Name).Take(5));
			Console.WriteLine($"[probe] W3i-c SUCCESS: full menu attempt booted mod '{bootedMod}' with {rootWidgets} root widgets ({widgetNames}) and rendered a UI frame in-browser");
		}
	}
}
