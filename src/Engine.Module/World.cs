using Engine.Core;

namespace Engine.Module;

public class World
{
    public World()
    {
        // Initialize the world and its systems
    }

    public async Task<Entity> CreateEntityAsync(CancellationToken ct = default) { }
}
