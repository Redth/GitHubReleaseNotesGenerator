using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GitHubReleaseNotesGenerator
{
	public class ProjectArea
	{
		[JsonProperty("label")]
		public string Label { get; set; }

		[JsonProperty("owners")]
		public string[] Owners { get; set; }

		public bool MatchesLabel(string label)
			=> label.StartsWith($"area/{Label}", StringComparison.OrdinalIgnoreCase);
	}
}