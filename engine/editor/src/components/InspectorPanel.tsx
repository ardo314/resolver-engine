import { useState } from "react";
import { useEditor } from "../hooks/useEditorState";
import { Panel } from "./Panel";
import { COMPONENT_DRAG_TYPE } from "./ComponentsPanel";

export function InspectorPanel() {
  const { entities, selectedEntityId, addComponentToEntity, removeComponentFromEntity } = useEditor();
  const entity = entities.find((e) => e.id === selectedEntityId);
  const [dragOver, setDragOver] = useState(false);

  const handleDragOver = (e: React.DragEvent) => {
    if (entity && e.dataTransfer.types.includes(COMPONENT_DRAG_TYPE)) {
      e.preventDefault();
      e.dataTransfer.dropEffect = "copy";
      setDragOver(true);
    }
  };

  const handleDragLeave = () => setDragOver(false);

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    if (!entity) return;
    const componentId = e.dataTransfer.getData(COMPONENT_DRAG_TYPE);
    if (componentId) {
      addComponentToEntity(entity.id, componentId);
    }
  };

  return (
    <Panel>
      <div
        className={`inspector-drop-zone${dragOver ? " drag-over" : ""}`}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
      >
      {!entity ? (
        <div className="inspector-empty">No entity selected</div>
      ) : (
        <div className="inspector-content">
          <div className="inspector-entity-header">
            <span className="inspector-entity-id">{entity.id}</span>
          </div>
          {entity.components.map((comp) => (
            <details key={comp.componentId} className="component-section" open>
              <summary className="component-header">
                {comp.componentId}
                <button
                  className="component-remove-btn"
                  title="Remove component"
                  onClick={(e) => {
                    e.preventDefault();
                    removeComponentFromEntity(entity.id, comp.componentId);
                  }}
                >
                  ×
                </button>
              </summary>
              <div className="property-list">
                {comp.properties.map((prop) => (
                  <div key={prop.name} className="property-row">
                    <span className="property-name">{prop.name}</span>
                    <span className="property-value">{prop.value}</span>
                  </div>
                ))}
              </div>
            </details>
          ))}
        </div>
      )}
      </div>
    </Panel>
  );
}
