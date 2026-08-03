using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
    [Desc("Records match statistics to a league table file at the end of the game.")]
    public class LeagueTableManagerInfo : TraitInfo
    {
        public readonly string TableFilename = "leaguetable.yaml";

        public override object Create(ActorInitializer init) { return new LeagueTableManager(this); }
    }

    public class LeagueTableManager : INotifyWinStateChanged
    {
        readonly LeagueTableManagerInfo info;
        bool recorded = false;

        public LeagueTableManager(LeagueTableManagerInfo info)
        {
            this.info = info;
        }

        void INotifyWinStateChanged.OnPlayerWon(Player winner)
        {
            RecordMatch(winner.World);
        }

        void INotifyWinStateChanged.OnPlayerLost(Player loser)
        {
            RecordMatch(loser.World);
        }

        void RecordMatch(World world)
        {
            if (recorded) return;
            recorded = true;

            var localPlayer = world.LocalPlayer;
            if (localPlayer == null || localPlayer.NonCombatant)
                return;

            var stats = localPlayer.PlayerActor.TraitOrDefault<PlayerStatistics>();
            if (stats == null)
                return;

            var result = localPlayer.WinState.ToString();
            var score = stats.Experience;
            var kills = stats.UnitsKilled + stats.BuildingsKilled;
            var resources = localPlayer.PlayerActor.TraitOrDefault<PlayerResources>();
            var income = resources != null ? resources.Earned : 0;
            var date = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            var duration = world.WorldTick * world.Timestep / 1000 / 60; // Minutes

            var recordNode = new MiniYaml("", new List<MiniYamlNode>
            {
                new MiniYamlNode("Date", date),
                new MiniYamlNode("Result", result),
                new MiniYamlNode("Score", score.ToString()),
                new MiniYamlNode("Kills", kills.ToString()),
                new MiniYamlNode("Income", income.ToString()),
                new MiniYamlNode("Duration", duration.ToString())
            });

            var id = Guid.NewGuid().ToString();

            // Load existing
            var path = Path.Combine(Platform.SupportDir, info.TableFilename);
            List<MiniYamlNode> existing = new List<MiniYamlNode>();
            
            try
            {
                // Attempt to use Javascript interop if available (set by WASM platform)
                if (Platform.LoadPersistedData != null)
                {
                    var data = Platform.LoadPersistedData(info.TableFilename);
                    if (!string.IsNullOrEmpty(data))
                        existing = MiniYaml.FromString(data, info.TableFilename).ToList();
                }
                else if (File.Exists(path))
                {
                    existing = MiniYaml.FromFile(path).ToList();
                }
            }
            catch (Exception) { }

            existing.Add(new MiniYamlNode(id, recordNode));

            // Write back
            var yamlString = existing.WriteToString();
            
            try
            {
                if (Platform.SavePersistedData != null)
                {
                    Platform.SavePersistedData(info.TableFilename, yamlString);
                }
                else
                {
                    File.WriteAllText(path, yamlString);
                }
            }
            catch (Exception) { }
        }
    }
}
