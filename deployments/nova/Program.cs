using Deployments.Nova;

var novaApi = Environment.GetEnvironmentVariable("NOVA_API")
    ?? throw new InvalidOperationException("NOVA_API is not set");
var cellName = Environment.GetEnvironmentVariable("CELL_NAME")
    ?? throw new InvalidOperationException("CELL_NAME is not set");
var natsBroker = Environment.GetEnvironmentVariable("NATS_BROKER")
    ?? throw new InvalidOperationException("NATS_BROKER is not set");
var backendImage = Environment.GetEnvironmentVariable("BACKEND_IMAGE")
    ?? throw new InvalidOperationException("BACKEND_IMAGE is not set");
var editorImage = Environment.GetEnvironmentVariable("EDITOR_IMAGE")
    ?? throw new InvalidOperationException("EDITOR_IMAGE is not set");

var natsBrokerUrl = new Uri(natsBroker);
var natsUser = string.IsNullOrEmpty(natsBrokerUrl.UserInfo)
    ? null
    : natsBrokerUrl.UserInfo.Split(':')[0];
var natsPass = natsBrokerUrl.UserInfo?.Contains(':') == true
    ? natsBrokerUrl.UserInfo.Split(':')[1]
    : null;
var natsUrl = new UriBuilder(natsBrokerUrl) { UserName = "", Password = "" }.Uri.ToString();

var client = new NovaClient($"http://{novaApi}");

await client.InstallApp(cellName,
    Apps.Backend(backendImage, cellName, natsUrl, natsUser, natsPass));

await client.InstallApp(cellName,
    Apps.Editor(editorImage, cellName, "/api/nats"));

// Install provider images from PROVIDER_IMAGE_0, PROVIDER_IMAGE_1, ...
for (var i = 0; ; i++)
{
    var image = Environment.GetEnvironmentVariable($"PROVIDER_IMAGE_{i}");
    if (image is null) break;
    var name = image.Split('/').Last().Split(':')[0];
    await client.InstallApp(cellName,
        Apps.Provider(name, image, natsUrl, natsUser, natsPass));
}

Console.WriteLine("\nAll apps installed. component-engine-nova done.");

// Keep alive
await Task.Delay(Timeout.Infinite);
