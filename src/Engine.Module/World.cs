namespace Engine.Module;

public class World
{
    public World()
    {
        // Initialize the world and its systems
    }

    public async Task<Entity> CreateEntityAsync(CancellationToken ct = default)
    {
        var entityId = EntityId.New();
        var entity = new Entity(entityId);
        // Additional initialization if needed
        return entity;
    }
}
