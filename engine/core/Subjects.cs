namespace Engine.Core;

public static class Subjects
{
    public const string CreateEntity = "engine.world.createEntity";
    public const string DeleteEntity = "engine.world.deleteEntity";
    public const string HasEntity = "engine.world.hasEntity";
    public const string ListEntities = "engine.world.listEntities";

    public const string AddComponent = "engine.entity.addComponent";
    public const string RemoveComponent = "engine.entity.removeComponent";
    public const string HasComponent = "engine.entity.hasComponent";
    public const string GetComponents = "engine.entity.getComponents";
    public const string QueryEntity = "engine.entity.query";

    public const string RegisterComponent = "engine.component.register";
    public const string ListComponents = "engine.component.list";

    public const string StartWorker = "engine.worker.start";
    public const string StopWorker = "engine.worker.stop";
}

public static class WorkerSubjects
{
    public static string CallMethod(string componentId, string entityId, string method) =>
        $"engine.worker.{componentId}.{entityId}.method.{method}";
}
