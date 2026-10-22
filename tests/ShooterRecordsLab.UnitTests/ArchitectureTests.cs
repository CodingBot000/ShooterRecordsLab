using System.Xml.Linq;

namespace ShooterRecordsLab.UnitTests;

public sealed class ArchitectureTests
{
    [Theory]
    [InlineData("Domain", "")]
    [InlineData("Application", "Domain")]
    [InlineData("Infrastructure", "Application,Domain")]
    [InlineData("Api", "Application,Infrastructure")]
    public void Project_references_preserve_layer_boundaries(string layer, string expected)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ShooterRecordsLab.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var project = XDocument.Load(Path.Combine(root.FullName, "src", $"ShooterRecordsLab.{layer}", $"ShooterRecordsLab.{layer}.csproj"));
        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/'))
                .Replace("ShooterRecordsLab.", "", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), references);
        if (layer is "Domain" or "Application")
        {
            Assert.Empty(project.Descendants("PackageReference"));
            Assert.Empty(project.Descendants("FrameworkReference"));
        }
    }
}
