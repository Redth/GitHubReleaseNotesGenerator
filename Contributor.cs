using Newtonsoft.Json;

namespace GitHubReleaseNotesGenerator
{
	public class Contributor
	{
		[JsonProperty("user")]
		public string User { get; set; }

		[JsonProperty("team")]
		public string Team { get; set; }
	}
}