namespace DemoApi.Bff.Tests.Bff;

public sealed class IsolationTests
{
    [Fact]
    public void Bff_references_no_DemoApi_assembly_other_than_itself()
    {
        string[] references = [.. typeof(Program).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("DemoApi.", StringComparison.Ordinal) && n != "DemoApi.Bff")];

        references.ShouldBeEmpty();
    }

    [Fact]
    public void Bff_project_file_has_no_project_reference()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "web", "DemoApi.Bff", "DemoApi.Bff.csproj")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        dir.ShouldNotBeNull();
        string csproj = File.ReadAllText(Path.Combine(dir, "web", "DemoApi.Bff", "DemoApi.Bff.csproj"));
        csproj.ShouldNotContain("ProjectReference");
    }
}