import { useEditor } from "../hooks/useEditorState";
import { Panel } from "./Panel";

export const COMPONENT_DRAG_TYPE = "application/x-component-id";

export function ComponentsPanel() {
  const { registeredComponents } = useEditor();

  return (
    <Panel>
      <ul className="component-reg-list">
        {registeredComponents.map((comp) => (
          <li
            key={comp.componentId}
            draggable
            onDragStart={(e) => {
              e.dataTransfer.setData(COMPONENT_DRAG_TYPE, comp.componentId);
              e.dataTransfer.effectAllowed = "copy";
            }}
          >
            <details className="component-reg-section" open>
              <summary className="component-reg-header">
                <span className="component-reg-icon">⬡</span>
                <span className="component-reg-name">{comp.componentId}</span>
              </summary>
              {comp.methodNames.length > 0 && (
                <div className="component-reg-composites">
                  <span className="component-reg-composites-label">
                    Methods
                  </span>
                  <ul className="component-reg-composite-list">
                    {comp.methodNames.map((name) => (
                      <li key={name} className="component-reg-composite-item">
                        {name}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </details>
          </li>
        ))}
        {registeredComponents.length === 0 && (
          <li className="component-reg-empty">No components registered</li>
        )}
      </ul>
    </Panel>
  );
}
