import { useEditor } from "../hooks/useEditorState";
import { Panel } from "../components/Panel";

export function EntitiesPanel() {
  const { entities, selectedEntityId, selectEntity } = useEditor();

  return (
    <Panel id="entities" title="Entities">
      <ul className="entity-list">
        {entities.map((entity) => {
          const nameComp = entity.components.find(
            (c) => c.componentId === "in-memory.name",
          );
          const nameRaw = nameComp?.schemas[0]?.properties.find(
            (p) => p.name === "value",
          )?.value;
          const displayName = nameRaw ? JSON.parse(nameRaw) : entity.id;

          return (
            <li key={entity.id}>
              <button
                className={`entity-item${selectedEntityId === entity.id ? " selected" : ""}`}
                onClick={() =>
                  selectEntity(
                    selectedEntityId === entity.id ? null : entity.id,
                  )
                }
              >
                <span className="entity-icon">◆</span>
                <span className="entity-name">{displayName}</span>
                <span className="entity-id">{entity.id}</span>
              </button>
            </li>
          );
        })}
      </ul>
    </Panel>
  );
}
