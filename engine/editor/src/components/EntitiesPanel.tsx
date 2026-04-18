import { useState } from "react";
import { useEditor } from "../hooks/useEditorState";
import { Panel } from "./Panel";
import { COMPONENT_DRAG_TYPE } from "./ComponentsPanel";

export function EntitiesPanel() {
  const { entities, selectedEntityId, selectEntity, createEntity, deleteEntity, addComponentToEntity } =
    useEditor();
  const [dragOverEntityId, setDragOverEntityId] = useState<string | null>(null);

  return (
    <Panel>
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
          const nameRaw = nameComp?.properties.find(
            (p) => p.name === "name",
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
                className={`entity-item${selectedEntityId === entity.id ? " selected" : ""}${dragOverEntityId === entity.id ? " drag-over" : ""}`}
                onClick={() =>
                  selectEntity(
                    selectedEntityId === entity.id ? null : entity.id,
                  )
                }
                onDragOver={(e) => {
                  if (e.dataTransfer.types.includes(COMPONENT_DRAG_TYPE)) {
                    e.preventDefault();
                    e.dataTransfer.dropEffect = "copy";
                    setDragOverEntityId(entity.id);
                  }
                }}
                onDragLeave={() => setDragOverEntityId(null)}
                onDrop={(e) => {
                  e.preventDefault();
                  setDragOverEntityId(null);
                  const componentId = e.dataTransfer.getData(COMPONENT_DRAG_TYPE);
                  if (componentId) {
                    addComponentToEntity(entity.id, componentId);
                  }
                }}
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
