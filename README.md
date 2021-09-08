# GitHubReleaseNotesGenerator
A simple git/github based release notes generator

1. Create a GitHub Personal Access Token for the repo you're generating notes for and put it in `.ghtoken`
2. Clone the repo for notes locally and make sure it's up to date
3. Edit `config.json`
  - local repo path, repo name, owner
  - from/to commit hashes to generate notes for
  - fromNupkg/toNupkg for each nupkg artifact to API diff
