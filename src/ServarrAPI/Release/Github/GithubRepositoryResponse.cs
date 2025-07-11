using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ServarrAPI.Release.Github;

public class NodesType<T>
{
    [JsonPropertyName("nodes")]
    public IReadOnlyList<T> Nodes { get; set; }

    public int? TotalCount  { get; set; }
}

public class GithubRepositoryResponse
{
    [JsonPropertyName("repository")]
    public GithubRepository Repository { get; set; }
}

public class GithubRepository
{
    [JsonPropertyName("releases")]
    public NodesType<GithubRelease> Releases { get; set; }
}

public class GithubRelease
{
    public string TagName { get; set; }
    public string Description { get; set; }
    public bool IsDraft { get; set; }
    public bool IsPrerelease { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("releaseAssets")]
    public NodesType<GithubReleaseAsset> Assets { get; set; }
}

public class GithubReleaseAsset
{
    public string Name { get; set; }
    public string DownloadUrl { get; set; }
    public string Digest { get; set; }
}
