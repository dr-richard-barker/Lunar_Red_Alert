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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.WasmProbe
{
	// Phase W4c: the LIVE game loop — requestAnimationFrame drives
	// Game.PerformBrowserFrame (engine edit #4) after MenuDemo boots.
	// Phase W4d: a unit in the live world obeys a movement command — the
	// activity -> pathfinding -> locomotion stack end to end. The command is
	// queued directly on the actor (the shellmap's local client is a
	// spectator, so lobby-level order ownership doesn't apply; the scripted
	// AI exercises the order pipeline itself every tick).
	public static partial class GameLoop
	{
		const int LiveFrames = 90;
		const int CommandFrames = 150;

		internal static bool Ready;

		// W5 play mode: no gates, no stop — the engine owns the frame for good.
		internal static bool PlayForever;
		static int frames;
		static int firstWorldTick = int.MinValue;
		static int lastWorldTick = int.MinValue;

		static Actor commanded;
		static WPos commandStart;
		static bool commandIssued;

		[JSExport]
		public static bool IsReady()
		{
			return Ready;
		}

		static string reportedWorld;

		// One-shot summary of each world the play loop takes over, to pin down
		// why the local player ends up with no units: whether there IS a local
		// player, which faction it resolved to (the StartingUnits groups are
		// faction-gated), where its spawn is, and what it actually owns.
		static void ReportWorldOnce()
		{
			var world = Game.ActiveWorld;
			if (world == null)
				return;

			var id = $"{world.Type}:{world.Map?.Title}";
			if (reportedWorld == id)
				return;

			reportedWorld = id;

			var p = world.LocalPlayer;
			if (p == null)
			{
				Console.WriteLine($"[diag] world '{id}' loaded with NO local player (spectator?)");
				return;
			}

			// Centre first: this is the actual fix, and it must not be hostage
			// to the reporting below.
			CenterOnSpawn(world, p);

			// Never let diagnostics take down the game loop -- an earlier
			// version of this threw partway through and silently froze the
			// match at 00:00 with a black screen.
			try
			{
				var owned = world.Actors.Where(a => a.Owner == p && a.IsInWorld).ToArray();
				Console.WriteLine(
					$"[diag] world '{id}': local player '{p.InternalName}' faction '{p.Faction?.InternalName}' " +
					$"spawn {p.HomeLocation} owns {owned.Length} actor(s); " +
					$"startingunits='{world.LobbyInfo.GlobalSettings.OptionOrDefault("startingunits", "?")}'");

				foreach (var a in owned.Take(10))
				{
					// Actors without an OccupiesSpace trait (player proxies and
					// similar) have no Location to read.
					var where = a.OccupiesSpace != null ? a.Location.ToString() : "(no position)";
					Console.WriteLine($"[diag]   owns: {a.Info.Name} at {where}");
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"[diag] world report failed (ignored): {e.Message}");
			}
		}

		// MapStartingLocations.WorldLoaded is supposed to leave the camera on
		// the local player's spawn, but on the deployed page it consistently
		// starts over unexplored map instead -- the player's own MCV sits at
		// its spawn cell, invisible, behind shroud. Re-centre once here, after
		// the world is fully up and the viewport has its final size, so a match
		// opens on your base like it does on desktop.
		static void CenterOnSpawn(World world, Player p)
		{
			try
			{
				// Game.worldRenderer is private; PlayMode already reaches for
				// non-public Game members during browser boot for the same reason.
				var wr = typeof(Game)
					.GetField("worldRenderer", BindingFlags.NonPublic | BindingFlags.Static)
					?.GetValue(null) as WorldRenderer;

				if (wr?.Viewport == null)
				{
					Console.WriteLine("[diag] no world renderer/viewport yet — camera not centred");
					return;
				}

				wr.Viewport.Center(world.Map.CenterOfCell(p.HomeLocation));
				Console.WriteLine($"[diag] camera centred on spawn {p.HomeLocation}");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[diag] centring the camera failed (ignored): {e.Message}");
			}
		}

		// Autoplay: hand the local player's base over to one of the mod's own
		// AI personalities, using the same IBot.Activate hook the engine uses
		// to start bots for AI slots. Returns the resulting state so the page
		// button can label itself; returns false (and does nothing) when there
		// is no local player to hand over, e.g. in the menu or as a spectator.
		[JSExport]
		public static string GetEndGameStats()
		{
			var player = Game.ActiveWorld?.LocalPlayer;
			if (player == null)
				return null;

			var stats = player.PlayerActor?.TraitOrDefault<PlayerStatistics>();
			if (stats == null)
				return null;

			var winState = player.WinState.ToString();
			
			return $"{{\"WinState\":\"{winState}\",\"KillsCost\":{stats.KillsCost},\"DeathsCost\":{stats.DeathsCost},\"UnitsKilled\":{stats.UnitsKilled},\"UnitsDead\":{stats.UnitsDead},\"BuildingsKilled\":{stats.BuildingsKilled},\"BuildingsDead\":{stats.BuildingsDead},\"ArmyValue\":{stats.ArmyValue}}}";
		}

		// War/Peace: flips the local player's relationship with whichever
		// players are currently Enemy (the map's hostile Creeps side, and
		// anyone else hostile by default) to Ally and back. AlliedPlayersMask/
		// EnemyPlayersMask are plain public bitset fields on Player -- no
		// order/networking plumbing needed for a local, single-player match.
		// Only touches players that were genuinely Enemy to begin with, so it
		// can't accidentally turn two already-neutral/allied sides hostile.
		static bool peaceMode;
		static readonly List<Player> peaceModeAffected = [];

		// Peace mode also reaches into the "normal" AI's own build/attack
		// logic (spaceage-defaults.yaml's ExternalCondition@PEACE +
		// SquadManagerBotModule@normal/UnitBuilderBotModule@normal
		// RequiresCondition overrides): while it's granted, that AI stops
		// raising new squads or combat units and just keeps its economy
		// running, on whichever player it's granted to -- the human's own
		// autoplay AI (ToggleAutoplay) and/or any AI rival opponent.
		static readonly object PeaceConditionSource = new();
		static readonly Dictionary<Actor, int> peaceConditionTokens = [];

		static void SetPeaceCondition(Actor playerActor, bool enable)
		{
			var ec = playerActor.TraitsImplementing<ExternalCondition>().FirstOrDefault(e => e.Info.Condition == "peace-mode");
			if (ec == null)
				return;

			if (enable)
			{
				if (!peaceConditionTokens.ContainsKey(playerActor))
					peaceConditionTokens[playerActor] = ec.GrantCondition(playerActor, PeaceConditionSource);
			}
			else if (peaceConditionTokens.TryGetValue(playerActor, out var token))
			{
				ec.TryRevokeCondition(playerActor, PeaceConditionSource, token);
				peaceConditionTokens.Remove(playerActor);
			}
		}

		[JSExport]
		public static bool ToggleWarPeace()
		{
			var world = Game.ActiveWorld;
			var player = world?.LocalPlayer;
			if (player == null)
				return false;

			peaceMode = !peaceMode;

			if (peaceMode)
			{
				peaceModeAffected.Clear();
				foreach (var other in world.Players)
				{
					if (other == player || player.RelationshipWith(other) != PlayerRelationship.Enemy)
						continue;

					peaceModeAffected.Add(other);
					player.AlliedPlayersMask = player.AlliedPlayersMask.Union(other.PlayerMask);
					player.EnemyPlayersMask = player.EnemyPlayersMask.Except(other.PlayerMask);
					other.AlliedPlayersMask = other.AlliedPlayersMask.Union(player.PlayerMask);
					other.EnemyPlayersMask = other.EnemyPlayersMask.Except(player.PlayerMask);
				}

				// Granted regardless of whether autoplay (ToggleAutoplay) is
				// currently active on the local player, so the AI's strategy
				// is always correct the moment it does take over -- and on
				// every AI rival opponent too, so nobody keeps massing an
				// army against a player they're no longer at war with.
				SetPeaceCondition(player.PlayerActor, true);
				foreach (var other in peaceModeAffected)
					SetPeaceCondition(other.PlayerActor, true);

				// Relationship alone stops the AI's squad manager picking new
				// targets (SquadManagerBotModule.IsPreferredEnemyUnit checks
				// RelationshipWith == Enemy), but anything already mid-raid
				// keeps walking toward wherever it was headed. Send every
				// mobile, non-Harvester unit home explicitly so "peace" reads
				// as a real retreat, not just a ceasefire units ignore.
				// Harvesters (and buildings) are left alone so the AI keeps
				// its economy running -- "focus on building and growth" means
				// production should carry on, not idle at home too.
				foreach (var other in peaceModeAffected)
				{
					var retreating = other.World.Actors
						.Where(a => a.Owner == other && !a.IsDead && a.IsInWorld
							&& a.Info.HasTraitInfo<MobileInfo>() && !a.Info.HasTraitInfo<HarvesterInfo>())
						.ToArray();

					foreach (var actor in retreating)
						world.IssueOrder(new Order("Move", actor, Target.FromCell(world, other.HomeLocation), false));

					Console.WriteLine($"[play] '{other.PlayerName}' retreating {retreating.Length} unit(s) home to {other.HomeLocation}");
				}

				Console.WriteLine($"[play] peace mode on — ceasefire with {peaceModeAffected.Count} player(s)");
			}
			else
			{
				SetPeaceCondition(player.PlayerActor, false);
				foreach (var other in peaceModeAffected)
				{
					SetPeaceCondition(other.PlayerActor, false);
					player.AlliedPlayersMask = player.AlliedPlayersMask.Except(other.PlayerMask);
					player.EnemyPlayersMask = player.EnemyPlayersMask.Union(other.PlayerMask);
					other.AlliedPlayersMask = other.AlliedPlayersMask.Except(player.PlayerMask);
					other.EnemyPlayersMask = other.EnemyPlayersMask.Union(player.PlayerMask);
				}

				Console.WriteLine("[play] peace mode off — war resumed");
				peaceModeAffected.Clear();
			}

			return peaceMode;
		}

		// Free Build: tops the local player's cash up to a floor whenever it
		// drops below, so production is effectively free without touching
		// per-item cost logic. Reuses the same ChangeCash the desktop debug
		// menu's "Give Cash" cheat calls (DeveloperMode.cs), which is already
		// unconditionally enabled here since a single non-bot player always
		// satisfies DeveloperMode.Created()'s `NonBotPlayers.Count() == 1` check.
		const int FreeBuildCashFloor = 20000;
		static bool freeBuildEnabled;

		[JSExport]
		public static bool ToggleFreeBuild()
		{
			var player = Game.ActiveWorld?.LocalPlayer;
			if (player == null)
				return false;

			freeBuildEnabled = !freeBuildEnabled;
			Console.WriteLine(freeBuildEnabled ? "[play] free build on" : "[play] free build off");
			return freeBuildEnabled;
		}

		static void TickFreeBuild()
		{
			if (!freeBuildEnabled)
				return;

			var player = Game.ActiveWorld?.LocalPlayer;
			var resources = player?.PlayerActor.TraitOrDefault<PlayerResources>();
			if (resources != null && resources.Cash < FreeBuildCashFloor)
				resources.ChangeCash(FreeBuildCashFloor - resources.Cash);
		}

		[JSExport]
		public static bool ToggleAutoplay()
		{
			var player = Game.ActiveWorld?.LocalPlayer;
			if (player == null)
				return false;

			var bots = player.PlayerActor.TraitsImplementing<IBot>().ToArray();
			var bot = bots.FirstOrDefault(b => b.Info.Type == "normal") ?? bots.FirstOrDefault();
			if (bot is not ModularBot modular)
				return false;

			if (modular.IsEnabled)
			{
				// Activate() wires up the module lists, so once it has run the
				// enable flag alone is enough to stop and restart the AI.
				modular.IsEnabled = false;
				Console.WriteLine("[play] autoplay off — you have control");
				return false;
			}

			bot.Activate(player);
			Console.WriteLine($"[play] autoplay on — '{bot.Info.Type}' AI is playing for you");
			return modular.IsEnabled;
		}

		[JSExport]
		public static bool OnFrame(double timestamp)
		{
			int tick;
			try
			{
				tick = Game.PerformBrowserFrame();
			}
			catch (Exception e)
			{
				Console.WriteLine($"[probe] STEP-FAIL live game frame {frames}: {e}");
				throw;
			}

			// Play mode: no gates — keep the engine on the browser's frame.
			if (PlayForever)
			{
				// Belt and braces: nothing in here is worth losing the match
				// over, and an inner catch already failed to contain one throw.
				// Log the full stack so the next failure names its own site.
				try
				{
					ReportWorldOnce();
				}
				catch (Exception e)
				{
					Console.WriteLine($"[diag] world report threw (ignored): {e}");
				}

				try
				{
					TickFreeBuild();
				}
				catch (Exception e)
				{
					Console.WriteLine($"[diag] free build tick threw (ignored): {e}");
				}

				return true;
			}

			if (firstWorldTick == int.MinValue)
				firstWorldTick = tick;
			lastWorldTick = tick;
			frames++;

			// Phase W4c: prove the loop is alive.
			if (frames == LiveFrames)
			{
				var simNote = firstWorldTick >= 0 && lastWorldTick > firstWorldTick
					? $"shellmap sim advanced {lastWorldTick - firstWorldTick} world ticks ({firstWorldTick}->{lastWorldTick})"
					: firstWorldTick >= 0
						? $"world present but sim did not advance (tick {lastWorldTick})"
						: "no world loaded — UI loop live";

				Console.WriteLine($"[probe] W4c SUCCESS: {LiveFrames} live frames via Game.PerformBrowserFrame — {simNote}");

				// Phase W4d setup: command a mobile unit in the live world.
				var world = Game.ActiveWorld;
				if (world == null)
				{
					Console.WriteLine("[probe] W4d skipped: no active world");
					return false;
				}

				commanded = world.Actors.FirstOrDefault(a =>
					a.IsInWorld && !a.IsDead && a.Info.HasTraitInfo<MobileInfo>());
				if (commanded == null)
				{
					Console.WriteLine("[probe] W4d skipped: no mobile actor in the world");
					return false;
				}

				commandStart = commanded.CenterPosition;
				var destination = world.Map.CellContaining(commandStart) + new CVec(5, 0);
				try
				{
					commanded.QueueActivity(false, new Move(commanded, destination));
					commandIssued = true;
					Console.WriteLine($"[probe] step: commanded '{commanded.Info.Name}' ({commanded.ActorID}) to move 5 cells east from {commandStart}");
				}
				catch (Exception e)
				{
					Console.WriteLine($"[probe] STEP-FAIL issuing Move: {e}");
					throw;
				}

				return true;
			}

			// Phase W4d verdict: did the unit obey?
			if (commandIssued && frames >= LiveFrames + CommandFrames)
			{
				var displacement = (commanded.CenterPosition - commandStart).Length;
				if (commanded.IsDead)
					Console.WriteLine("[probe] W4d note: commanded unit died mid-move (battle casualties are canon) — treating as inconclusive");
				else if (displacement > 256)
					Console.WriteLine($"[probe] W4d SUCCESS: unit '{commanded.Info.Name}' obeyed the Move command — displaced {displacement} world units through pathfinding/locomotion in-browser");
				else
					throw new InvalidOperationException($"Unit did not move (displacement {displacement} after {CommandFrames} frames)");

				return false;
			}

			return true;
		}
	}
}
