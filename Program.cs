using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.ApiTools;

namespace GitHubReleaseNotesGenerator
{
	class Program
	{
		static async Task Main(string[] args)
		{
			var configFile = Path.Combine(AppContext.BaseDirectory, "config.json");
			var configData = File.ReadAllText(configFile);
			var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(configData);

			var token = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ".ghtoken"));

			var rxPrNum = new Regex(@"\(#(?<pr>[0-9]+)\)", RegexOptions.Compiled | RegexOptions.Singleline);

			var fromCommit = config.FromCommit;
			var toCommit = config.ToCommit;

			var repoPath = config.RepositoryLocalPath;
			var owner = config.RepositoryOwner;
			var repo = config.RepositoryName;

			var notes = new System.Text.StringBuilder();

			var github = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("dotnet-gh-releasenotes-generator"));
			github.Credentials = new Octokit.Credentials(token);

			using (var localRepo = new LibGit2Sharp.Repository(repoPath))
			{
				var filter = new LibGit2Sharp.CommitFilter
				{
					ExcludeReachableFrom = fromCommit,
					IncludeReachableFrom = toCommit,
				};

				foreach (LibGit2Sharp.Commit c in localRepo.Commits.QueryBy(filter))
				{
					var prMatch = rxPrNum.Match(c.MessageShort);

					if (int.TryParse(prMatch?.Groups?["pr"]?.Value ?? string.Empty, out var prNumber) && prNumber > 0)
					{
						var pr = await github.Repository.PullRequest.Get(owner, repo, prNumber);

						if (pr != null)
						{
							var authorName = pr.User.Login ?? pr.User.Email;
							var str = $" - {pr.Title} - [#{pr.Number}]({pr.HtmlUrl}) ([@{authorName}]({pr.User.HtmlUrl}))";
							notes.AppendLine(str);
							Console.WriteLine(str);
						}

						var labels = pr.Labels.Select(l => l.Name).ToArray();

						var areas = config.GetAreas(labels);

						var contributors = config.GetContributors(areas?.ToArray());

						// TODO: Organize or pretty print based on grouping things by area and/or contributor/team
					}
				}
			}

			File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "notes.md"), notes.ToString());


			// Diff nupkg's
			foreach (var artifact in config.ArtifactDiffs)
			{
				var id = string.Empty;

				using (var r = new NuGet.Packaging.PackageArchiveReader(artifact.FromNupkg))
					id = r.GetIdentity().Id;

				var diffDir = Path.Combine(AppContext.BaseDirectory, "api-diff", id);
				Directory.CreateDirectory(diffDir);

				// create the comparer
				var comparer = new NuGetDiff();

				// set any properties, in this case ignore errors as this is not essential
				comparer.IgnoreResolutionErrors = true;
				comparer.SaveAssemblyMarkdownDiff = true;
				comparer.SaveAssemblyApiInfo = true;
				comparer.SaveNuGetXmlDiff = true;

				using (var oldPkg = new NuGet.Packaging.PackageArchiveReader(artifact.FromNupkg))
				using (var newPkg = new NuGet.Packaging.PackageArchiveReader(artifact.ToNupkg))
				{
					await comparer.SaveCompleteDiffToDirectoryAsync(oldPkg, newPkg, diffDir);
				}
			}
		}
	}
}
