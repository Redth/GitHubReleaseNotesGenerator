using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GitHubReleaseNotesGenerator
{
	public class ArtifactDiff
	{
		[JsonProperty("fromNupkg")]
		public string FromNupkg { get; set; }

		[JsonProperty("toNupkg")]
		public string ToNupkg { get; set; }
	}

	public class Config
	{
		[JsonProperty("repositoryLocalPath")]
		public string RepositoryLocalPath { get; set; }

		[JsonProperty("repositoryOwner")]
		public string RepositoryOwner { get; set; }

		[JsonProperty("repositoryName")]
		public string RepositoryName { get; set; }

		[JsonProperty("fromCommit")]
		public string FromCommit { get; set; }

		[JsonProperty("toCommit")]
		public string ToCommit { get; set; }

		[JsonProperty("areas")]
		public ProjectArea[] Areas { get; set; }

		[JsonProperty("contributors")]
		public Contributor[] Contributors { get; set; }

		[JsonProperty("artifactDiffs")]
		public ArtifactDiff[] ArtifactDiffs { get; set; }

		public Dictionary<string, List<Contributor>> GetTeams()
		{
			var results = new Dictionary<string, List<Contributor>>();

			foreach (var c in Contributors)
			{
				if (!results.ContainsKey(c.Team))
					results.Add(c.Team, new List<Contributor>());

				results[c.Team].Add(c);
			}

			return results;
		}

		public IEnumerable<ProjectArea> GetAreas(params string[] labels)
		{
			foreach (var area in Areas)
			{
				if (labels?.Any(l => area.MatchesLabel(l)) ?? false)
					yield return area;
			}
		}

		public IEnumerable<Contributor> GetContributors(params ProjectArea[] areas)
		{
			foreach (var area in areas)
			{
				foreach (var owner in area.Owners)
				{
					foreach (var match in Contributors?.Where(
						c => c.Team.Equals(owner, System.StringComparison.InvariantCultureIgnoreCase)
							|| c.User.Equals(owner, System.StringComparison.InvariantCultureIgnoreCase)))
						yield return match;
				}
			}
		}
	}
}
