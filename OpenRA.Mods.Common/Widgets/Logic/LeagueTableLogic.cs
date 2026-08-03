using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class LeagueTableLogic : ChromeLogic
	{
		public LeagueTableLogic(Widget widget, Action onExit)
		{
			var closeButton = widget.GetOrNull<ButtonWidget>("BACK_BUTTON");
			if (closeButton != null)
			{
				closeButton.OnClick = () =>
				{
					Ui.CloseWindow();
					onExit?.Invoke();
				};
			}

			var list = widget.Get<ScrollPanelWidget>("LEAGUE_LIST");
			var template = widget.Get<ScrollItemWidget>("LEAGUE_TEMPLATE");
			var emptyTemplate = widget.GetOrNull<ScrollItemWidget>("LEAGUE_EMPTY_TEMPLATE");

			list.RemoveChildren();

			const string tableFilename = "leaguetable.yaml";
			var path = Path.Combine(Platform.SupportDir, tableFilename);
			var existing = new List<MiniYamlNode>();

			try
			{
				if (Platform.LoadPersistedData != null)
				{
					var data = Platform.LoadPersistedData(tableFilename);
					if (!string.IsNullOrEmpty(data))
						existing = MiniYaml.FromString(data, tableFilename).ToList();
				}
				else if (File.Exists(path))
				{
					existing = MiniYaml.FromFile(path).ToList();
				}
			}
			catch (Exception) { }

			if (existing.Count == 0 && emptyTemplate != null)
			{
				var item = ScrollItemWidget.Setup(emptyTemplate, () => true, () => { });
				list.AddChild(item);
				return;
			}

			// Reverse to show most recent first
			existing.Reverse();

			foreach (var node in existing)
			{
				var record = node.Value;
				var date = record.Nodes.FirstOrDefault(n => n.Key == "Date")?.Value.Value ?? "Unknown";
				var result = record.Nodes.FirstOrDefault(n => n.Key == "Result")?.Value.Value ?? "Unknown";
				var score = record.Nodes.FirstOrDefault(n => n.Key == "Score")?.Value.Value ?? "0";
				var kills = record.Nodes.FirstOrDefault(n => n.Key == "Kills")?.Value.Value ?? "0";
				var income = record.Nodes.FirstOrDefault(n => n.Key == "Income")?.Value.Value ?? "0";
				var duration = record.Nodes.FirstOrDefault(n => n.Key == "Duration")?.Value.Value ?? "0";

				var item = ScrollItemWidget.Setup(template, () => true, () => { });

				var dateLabel = item.GetOrNull<LabelWidget>("DATE");
				if (dateLabel != null) dateLabel.GetText = () => date;

				var resultLabel = item.GetOrNull<LabelWidget>("RESULT");
				if (resultLabel != null) resultLabel.GetText = () => result;

				var scoreLabel = item.GetOrNull<LabelWidget>("SCORE");
				if (scoreLabel != null) scoreLabel.GetText = () => score;

				var killsLabel = item.GetOrNull<LabelWidget>("KILLS");
				if (killsLabel != null) killsLabel.GetText = () => kills;

				var incomeLabel = item.GetOrNull<LabelWidget>("INCOME");
				if (incomeLabel != null) incomeLabel.GetText = () => income;

				var durationLabel = item.GetOrNull<LabelWidget>("DURATION");
				if (durationLabel != null) durationLabel.GetText = () => duration + "m";

				list.AddChild(item);
			}
		}
	}
}
