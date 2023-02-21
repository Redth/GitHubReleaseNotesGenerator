using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.ApiTools;
using Octokit;

namespace GitHubReleaseNotesGenerator
{
	class Program
	{
		static async Task Main(string[] args)
		{
			var configFile = Path.Combine(AppContext.BaseDirectory, "maui.config.json");
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

			var changeNotes = new System.Text.StringBuilder();
			var dependancyNotes = new System.Text.StringBuilder();
			var maestroNotes = new System.Text.StringBuilder();

			var github = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("dotnet-gh-releasenotes-generator"));
			github.Credentials = new Octokit.Credentials(token);

			var processedPrs = new List<int>();

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
						PullRequest pr = null;

						try
						{
							pr = await github.Repository.PullRequest.Get(owner, repo, prNumber);
						} catch { }

						string authorName;
						string msg;
						bool skipped = false;

						if (pr != null)
						{
							authorName = pr.User.Login ?? pr.User.Email;
							msg = $" * {pr.Title} - [#{pr.Number}]({pr.HtmlUrl}) ([@{authorName}]({pr.User.HtmlUrl}))";
						}
						else
						{
							authorName = c.Author.Name ?? c.Committer.Name ?? string.Empty;
							if (!string.IsNullOrEmpty(authorName))
								authorName = "@" + authorName;

							msg = $" * {c.MessageShort} - {c.Sha} ({authorName})";
						}

						if (config.SkipPRTitlePatterns.Any(p => msg.Contains(p)))
							skipped = true;

						if (!string.IsNullOrEmpty(msg) && !skipped && (pr is not null && !processedPrs.Contains(pr.Number)))
						{
							var isMaestro = authorName.StartsWith("dotnet-maestro");
							var isDependabot = authorName.StartsWith("dependabot");

							if (isMaestro && config.IncludeMaestroBumps)
								maestroNotes.AppendLine(msg);

							if (isDependabot)
								dependancyNotes.AppendLine(msg);

							if (!isMaestro && !isDependabot)
								changeNotes.AppendLine(msg);

							processedPrs.Add(pr.Number);

							Console.WriteLine(msg);
						}

						//var labels = pr.Labels.Select(l => l.Name).ToArray();

						//var areas = config.GetAreas(labels);

						//var contributors = config.GetContributors(areas?.ToArray());
						// TODO: Organize or pretty print based on grouping things by area and/or contributor/team
					}
				}
			}


			notes.AppendLine("## What's Changed");
			notes.AppendLine(changeNotes.ToString());

			notes.AppendLine("## Dependency Updates");
			notes.AppendLine(dependancyNotes.ToString());


			if (config.IncludeMaestroBumps)
			{
				notes.AppendLine("## DotNet Maestro Updates");
				notes.AppendLine(maestroNotes.ToString());
			}

			notes.AppendLine();

			var changesLink = $"https://github.com/{owner}/{repo}/compare/{fromCommit}...{toCommit}";

			notes.AppendLine("**Full Changelog:** " + changesLink);

			File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "notes.md"), notes.ToString());

			if (config.ArtifactDiffs != null && config.ArtifactDiffs.Any())
			{

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
}
