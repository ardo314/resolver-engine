import { useEditor } from "../hooks/useEditorState";
import { Panel } from "./Panel";

export function InspectorPanel() {
  const { entities, selectedEntityId } = useEditor();
  const entity = entities.find((e) => e.id === selectedEntityId);

  return (
    <Panel id="inspector" title="Inspector">
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
    </Panel>
  );
}
