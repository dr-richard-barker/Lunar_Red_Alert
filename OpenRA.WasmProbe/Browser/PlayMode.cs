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

			// Diagnostic #2 (cheap, cannot hang): replicate the ENGINE's exact
			// resolution + mount attempt for the "content" system package alias
			// (~^SupportDir|Content/ra/v2/: content). File.Exists on our own
			// hand-built path proved the files are physically present, but that
			// is a DIFFERENT code path from whether FileSystem.Mount/OpenPackage
			// actually succeeds -- test that directly, using the engine's own
			// Platform.ResolvePath.
			var engineResolvedContentPath = Platform.ResolvePath("^SupportDir|Content/ra/v2/");
			Console.WriteLine($"[play] diag2: engine-resolved content path = '{engineResolvedContentPath}'");
			Console.WriteLine($"[play] diag2: Directory.Exists(engine path) = {Directory.Exists(engineResolvedContentPath)}");
			try
			{
				var tempFs = new OpenRA.FileSystem.FileSystem("diag", null, []);
				var contentPkg = tempFs.OpenPackage(engineResolvedContentPath);
				Console.WriteLine($"[play] diag2: OpenPackage succeeded, contains allies.mix = {contentPkg?.Contains("allies.mix")}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[play] diag2: OpenPackage THREW: {ex.GetType().Name}: {ex.Message}");
			}

			// Diagnostic #3: replicate the REAL failing call -- mounting
			// "content|allies.mix" as its own sub-package (this is what
			// actually parses the .mix archive format; diag2 only proved the
			// FOLDER opens and lists the file, not that the .mix parses).
			// Game.ModData isn't set yet at this point (that's the whole
			// point -- we're testing BEFORE the real boot), so build our own
			// ObjectCreator from the manifest already in scope, exactly like
			// ModData's own constructor does internally.
			try
			{
				var diagObjectCreator = new ObjectCreator(manifest, installed);
				var loaders = diagObjectCreator.GetLoaders<OpenRA.FileSystem.IPackageLoader>(manifest.PackageFormats, "package");
				Console.WriteLine($"[play] diag3: resolved {loaders.Length} package loader(s) for formats [{string.Join(",", manifest.PackageFormats)}]");
				var tempFs2 = new OpenRA.FileSystem.FileSystem("diag2", null, loaders);
				tempFs2.Mount(engineResolvedContentPath, "content");
				Console.WriteLine("[play] diag3: mounted content alias OK");
				tempFs2.Mount("content|allies.mix");
				Console.WriteLine("[play] diag3: Mount(content|allies.mix) SUCCEEDED");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[play] diag3: Mount(content|allies.mix) THREW: {ex.GetType().FullName}: {ex.Message}");
				Console.WriteLine($"[play] diag3: stack: {ex.StackTrace}");
			}

			// Diagnostic #4: test EVERY required (non-optional) ContentPackage
			// entry from mod.yaml individually -- diag3 only proved allies.mix
			// works; one of the other 8 is the actual failure.
			try
			{
				var diagObjectCreator2 = new ObjectCreator(manifest, installed);
				var loaders2 = diagObjectCreator2.GetLoaders<OpenRA.FileSystem.IPackageLoader>(manifest.PackageFormats, "package");
				var tempFs3 = new OpenRA.FileSystem.FileSystem("diag3", null, loaders2);
				tempFs3.Mount(engineResolvedContentPath, "content");
				foreach (var file in new[] { "allies.mix", "conquer.mix", "interior.mix", "lores.mix", "local.mix", "russian.mix", "snow.mix", "sounds.mix", "temperat.mix" })
				{
					try
					{
						tempFs3.Mount($"content|{file}");
						Console.WriteLine($"[play] diag4: content|{file} -> OK");
					}
					catch (Exception fileEx)
					{
						Console.WriteLine($"[play] diag4: content|{file} -> FAILED: {fileEx.GetType().Name}: {fileEx.Message}");
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[play] diag4: setup THREW: {ex.GetType().FullName}: {ex.Message}");
			}

			// Diagnostic #5 (definitive): replicate ModData's EXACT
			// FileSystemLoader construction sequence verbatim (same calls, same
			// order: GetLoaders<IPackageLoader> -> new FS(...) ->
			// GetLoader<IFileSystemLoader> -> FieldLoader.Load -> Mount), then
			// read the loader's private isContentAvailable field via
			// reflection. diag2-4 each independently succeeded, so the
			// remaining explanation is a subtle difference between my manual
			// reimplementation and the real loader's own bookkeeping --this
			// stops guessing and asks the real object directly.
			try
			{
				var diagOc5 = new ObjectCreator(manifest, installed);
				var diagPackageLoaders5 = diagOc5.GetLoaders<OpenRA.FileSystem.IPackageLoader>(manifest.PackageFormats, "package");
				var diagModFiles5 = new OpenRA.FileSystem.FileSystem(manifest.Id, installed, diagPackageLoaders5);
				var diagLoader5 = diagOc5.GetLoader<OpenRA.FileSystem.IFileSystemLoader>(manifest.FileSystem.Value, "filesystem");
				FieldLoader.Load(diagLoader5, manifest.FileSystem);
				diagLoader5.Mount(manifest, diagModFiles5, diagOc5);

				var isContentAvailableField = diagLoader5.GetType().GetField("isContentAvailable", BindingFlags.NonPublic | BindingFlags.Instance);
				var value = isContentAvailableField?.GetValue(diagLoader5);
				Console.WriteLine($"[play] diag5: REAL loader ({diagLoader5.GetType().Name}) isContentAvailable = {value?.ToString() ?? "(field not found)"}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[play] diag5: THREW: {ex.GetType().FullName}: {ex.Message}");
				Console.WriteLine($"[play] diag5: stack: {ex.StackTrace}");
			}

			Console.WriteLine("[play] booting Lunar Red Alert…");
			Game.InitializeMod(manifest, Arguments.Empty);
			Console.WriteLine($"[play] boot complete — active mod '{Game.ModData?.Manifest.Id}'");

			GameLoop.PlayForever = true;
			GameLoop.Ready = true;
		}
	}
}
