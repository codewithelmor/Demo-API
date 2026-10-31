WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddBff();

WebApplication app = builder.Build();
app.UseBff();
await app.RunAsync();

/// <summary>Entry point; public for WebApplicationFactory in the BFF tests.</summary>
public partial class Program;