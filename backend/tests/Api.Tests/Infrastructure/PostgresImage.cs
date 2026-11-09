using YamlDotNet.RepresentationModel;

namespace PersonalFinance.Api.Tests.Infrastructure;

/// <summary>
/// The PostgreSQL image used by integration tests (Testcontainers).
/// The version is pinned once, in the db service of compose.yaml at the repository root,
/// so local development, tests and CI always run the same PostgreSQL (docs/devops/docker.md).
/// </summary>
public static class PostgresImage
{
    public const string ComposeFileName = "compose.yaml";
    public const string ServiceName = "db";

    private static readonly Lazy<string> Image = new(ReadFromCompose);

    /// <summary>Image reference of the db service, e.g. <c>postgres:18.6</c>.</summary>
    public static string Name => Image.Value;

    private static string ReadFromCompose()
    {
        var composeFile = FindComposeFile();
        using var reader = File.OpenText(composeFile);

        var yaml = new YamlStream();
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var services = (YamlMappingNode)root.Children[new YamlScalarNode("services")];
        var db = (YamlMappingNode)services.Children[new YamlScalarNode(ServiceName)];
        var image = db.Children[new YamlScalarNode("image")].ToString();

        if (string.IsNullOrWhiteSpace(image))
        {
            throw new InvalidOperationException($"Service '{ServiceName}' in {composeFile} has no image.");
        }

        return image;
    }

    private static string FindComposeFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, ComposeFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"{ComposeFileName} not found in any parent folder of {AppContext.BaseDirectory}.");
    }
}
