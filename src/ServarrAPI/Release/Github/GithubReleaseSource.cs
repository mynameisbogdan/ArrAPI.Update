using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServarrAPI.Extensions;
using ServarrAPI.Model;
using ServarrAPI.Util;

namespace ServarrAPI.Release.Github
{
    public class GithubReleaseSource : ReleaseSourceBase
    {
        private static readonly Regex ReleaseFeaturesGroup = new (@"\*\s+[0-9a-f]{40}\s+(?:New:|\(?feat\)?.*:)\s*(?<text>.*?)\r*$", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex ReleaseFixesGroup = new (@"\*\s+[0-9a-f]{40}\s+(?:Fix(?:ed)?:|\(?fix\)?.*:)\s*(?<text>.*?)\r*$", RegexOptions.Compiled | RegexOptions.Multiline);

        private readonly Config _config;
        private readonly IUpdateService _updateService;
        private readonly IUpdateFileService _updateFileService;
        private readonly ILogger<GithubReleaseSource> _logger;

        private readonly GraphQLHttpClient _graphqlClient;

        public GithubReleaseSource(IUpdateService updateService,
                                   IUpdateFileService updateFileService,
                                   IOptions<Config> config,
                                   ILogger<GithubReleaseSource> logger)
        {
            _updateService = updateService;
            _updateFileService = updateFileService;
            _logger = logger;
            _config = config.Value;

            if (string.IsNullOrWhiteSpace(_config.GithubApiToken))
            {
                throw new Exception("Github API token not set.");
            }

            _graphqlClient = new GraphQLHttpClient("https://api.github.com/graphql", new SystemTextJsonSerializer());
            _graphqlClient.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.GithubApiToken);
            _graphqlClient.HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ServarrUpdateAPI", "1.0.0"));
        }

        protected override async Task<List<string>> DoFetchReleasesAsync()
        {
            var updated = new HashSet<string>();

            var githubOrg = _config.GithubOrg ?? _config.Project;

            var fetchReleasesRequest = new GraphQLRequest
            {
                Query = """
                query ($owner: String!, $repository: String!) {
                  repository(owner: $owner, name: $repository) {
                    releases(first: 100, orderBy: { field: CREATED_AT, direction: DESC }) {
                      nodes {
                        tagName
                        description
                        isDraft
                        isPrerelease
                        createdAt
                        publishedAt
                        releaseAssets(first: 100) {
                          nodes {
                            name
                            downloadUrl
                            digest
                          }
                          totalCount
                        }
                      }
                    }
                  }
                }
                """,
                Variables = new
                {
                    owner = githubOrg,
                    repository = _config.Project
                }
            };

            var graphqlResponse = await _graphqlClient.SendQueryAsync<GithubRepositoryResponse>(fetchReleasesRequest);

            var releases = graphqlResponse.Data.Repository.Releases.Nodes
                .Where(release => !release.IsDraft && release.PublishedAt.HasValue)
                .OrderByDescending(release => release.PublishedAt.Value)
                .Where(release => release.TagName.StartsWith('v') && VersionUtil.IsValid(release.TagName[1..]))
                .Take(5)
                .Reverse();

            foreach (var release in releases)
            {
                var version = release.TagName[1..];

                if (release.Assets.TotalCount is > 100)
                {
                    throw new TooManyReleaseAssetsException($"Too many release assets for release: {release.TagName}");
                }

                // Determine the branch
                var branch = release.Assets.Nodes.Any(a => a.Name.StartsWith($"{_config.Project}.master")) ? "master" : "develop";

                if (await ProcessRelease(release, branch, version))
                {
                    updated.Add(branch);
                }

                // Releases on master should also appear on develop
                if (branch == "master" && await ProcessRelease(release, "develop", version))
                {
                    updated.Add("develop");
                }
            }

            return updated.ToList();
        }

        private async Task<bool> ProcessRelease(GithubRelease release, string branch, string version)
        {
            // Get an updateEntity
            var updateEntity = await _updateService.Find(version, branch).ConfigureAwait(false);

            if (updateEntity is not null)
            {
                return false;
            }

            var parsedVersion = Version.Parse(version);

            // Create update object
            updateEntity = new UpdateEntity
            {
                Version = version,
                IntVersion = parsedVersion.ToIntVersion(),
                ReleaseDate = release.PublishedAt!.Value.UtcDateTime,
                Branch = branch
            };

            // Parse changes
            var releaseBody = release.Description;

            var features = ReleaseFeaturesGroup.Matches(releaseBody).ToList();

            if (features.Count > 0)
            {
                updateEntity.New.Clear();

                foreach (var match in features)
                {
                    updateEntity.New.Add(match.Groups["text"].Value.Trim());
                }
            }

            var fixes = ReleaseFixesGroup.Matches(releaseBody).ToList();

            if (fixes.Count > 0)
            {
                updateEntity.Fixed.Clear();

                foreach (var match in fixes)
                {
                    updateEntity.Fixed.Add(match.Groups["text"].Value.Trim());
                }
            }

            await _updateService.Insert(updateEntity).ConfigureAwait(false);

            // Process release files
            foreach (var asset in release.Assets.Nodes)
            {
                await ProcessAsset(asset, updateEntity.Id);
            }

            return true;
        }

        private async Task ProcessAsset(GithubReleaseAsset releaseAsset, int updateId)
        {
            var operatingSystem = Parser.ParseOS(releaseAsset.Name);

            if (!operatingSystem.HasValue)
            {
                return;
            }

            var runtime = Parser.ParseRuntime(releaseAsset.Name);
            var architecture = Parser.ParseArchitecture(releaseAsset.Name);
            var isInstaller = Parser.ParseInstaller(releaseAsset.Name);

            var releaseHash = releaseAsset.Digest?.Split(':').Skip(1).FirstOrDefault();

            var updateFile = new UpdateFileEntity
            {
                UpdateId = updateId,
                OperatingSystem = operatingSystem.Value,
                Architecture = architecture,
                Runtime = runtime,
                Filename = releaseAsset.Name,
                Url = releaseAsset.DownloadUrl,
                Hash = releaseHash,
                Installer = isInstaller
            };

            await _updateFileService.Insert(updateFile).ConfigureAwait(false);
        }
    }
}
