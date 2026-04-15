import "./App.css";
import { EditorProvider, useEditor } from "./hooks/useEditorState";
import { TopBar } from "./components/TopBar";
import { EntitiesPanel } from "./components/EntitiesPanel";
import { InspectorPanel } from "./components/InspectorPanel";
import { ComponentsPanel } from "./components/ComponentsPanel";

function Layout() {
  const { panels, connected } = useEditor();
  const hasLeft = panels.entities || panels.components;
  const hasRight = panels.inspector;

  return (
    <div className="editor">
      <TopBar />
      {!connected && (
        <div className="connection-banner">Connecting to NATS...</div>
      )}
      <div
        className="editor-body"
        data-left={hasLeft || undefined}
        data-right={hasRight || undefined}
      >
        {hasLeft && (
          <aside className="editor-left">
            {panels.entities && <EntitiesPanel />}
            {panels.components && <ComponentsPanel />}
          </aside>
        )}
        <main className="editor-center" />
        {hasRight && (
          <aside className="editor-right">
            <InspectorPanel />
          </aside>
        )}
      </div>
    </div>
  );
}

function App() {
  return (
    <EditorProvider>
      <Layout />
    </EditorProvider>
  );
}

export default App;
