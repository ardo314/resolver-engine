import { useEditor } from "../hooks/useEditorState";
import { Panel } from "./Panel";

export function EntitiesPanel() {
  const { entities, selectedEntityId, selectEntity, createEntity, deleteEntity } =
    useEditor();

  return (
    <Panel id="entities" title="Entities">
      <div className="entity-toolbar">
        <button className="entity-toolbar-btn" onClick={createEntity} title="Create entity">
          + New
        </button>
        {selectedEntityId && (
          <button
            className="entity-toolbar-btn danger"
            onClick={() => deleteEntity(selectedEntityId)}
            title="Delete selected entity"
          >
            Delete
          </button>
        )}
      </div>
      <ul className="entity-list">
        {entities.map((entity) => {
          const nameComp = entity.components.find(
            (c) => c.componentId === "in-memory.name",
          );
          const nameRaw = nameComp?.schemas[0]?.properties.find(
            (p) => p.name === "value",
          )?.value;
          let displayName: string;
          try {
            displayName = nameRaw ? JSON.parse(nameRaw) : entity.id;
          } catch {
            displayName = entity.id;
          }

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
        {entities.length === 0 && (
          <li className="entity-empty">No entities</li>
        )}
      </ul>
    </Panel>
  );
}
