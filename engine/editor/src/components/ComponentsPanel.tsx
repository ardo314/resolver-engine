import { useEditor } from "../hooks/useEditorState";
import { Panel } from "./Panel";

export function ComponentsPanel() {
  const { registeredComponents } = useEditor();

  return (
    <Panel id="components" title="Components">
      <ul className="component-reg-list">
        {registeredComponents.map((comp) => (
          <li key={comp.componentId}>
            <details className="component-reg-section" open>
              <summary className="component-reg-header">
                <span className="component-reg-icon">⬡</span>
                <span className="component-reg-name">{comp.componentId}</span>
              </summary>
              {comp.compositeIds.length > 0 && (
                <div className="component-reg-composites">
                  <span className="component-reg-composites-label">
                    Composites
                  </span>
                  <ul className="component-reg-composite-list">
                    {comp.compositeIds.map((id) => (
                      <li key={id} className="component-reg-composite-item">
                        {id}
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
