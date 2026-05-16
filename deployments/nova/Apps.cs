using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deployments.Nova;

public record AppEnvironmentVariable(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value
);

public record ContainerImage(
    [property: JsonPropertyName("image")] string Image
);

public record App(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("app_icon")] string AppIcon,
    [property: JsonPropertyName("container_image")] ContainerImage ContainerImage,
    [property: JsonPropertyName("port")] int Port,
    [property: JsonPropertyName("environment")] List<AppEnvironmentVariable> Environment
);

public static class Apps
{
    public static App Backend(
        string image, string cellName, string natsUrl,
        string? natsUser = null, string? natsPass = null)
    {
        var env = new List<AppEnvironmentVariable>
        {
            new("NATS_URL", natsUrl),
            new("BASE_PATH", $"/{cellName}/component-engine-backend"),
        };
        if (natsUser is not null) env.Add(new("NATS_USER", natsUser));
        if (natsPass is not null) env.Add(new("NATS_PASS", natsPass));

        return new App("component-engine-backend", "favicon.ico",
            new ContainerImage(image), 8080, env);
    }

    public static App Editor(string image, string cellName, string natsUrl)
    {
        return new App("component-engine-editor", "favicon.ico",
            new ContainerImage(image), 8080, new List<AppEnvironmentVariable>
            {
                new("NATS_URL", natsUrl),
                new("BASE_PATH", $"/{cellName}/component-engine-editor"),
            });
    }

    public static App Provider(
        string name, string image, string natsUrl,
        string? natsUser = null, string? natsPass = null)
    {
        var env = new List<AppEnvironmentVariable>
        {
            new("NATS_URL", natsUrl),
        };
        if (natsUser is not null) env.Add(new("NATS_USER", natsUser));
        if (natsPass is not null) env.Add(new("NATS_PASS", natsPass));

        return new App(name, "favicon.ico",
            new ContainerImage(image), 8080, env);
    }
}

public class NovaClient
{
    private readonly HttpClient _http;

    public NovaClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task InstallApp(string cellName, App app)
    {
        Console.WriteLine($"Installing app '{app.Name}' into cell '{cellName}'...");
        var response = await _http.PostAsJsonAsync(
            $"/api/v2/cells/{cellName}/apps", app);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            Console.WriteLine($"  -> '{app.Name}' already exists, skipping");
            return;
        }

        response.EnsureSuccessStatusCode();
        Console.WriteLine($"  -> '{app.Name}' installed");
    }
}
