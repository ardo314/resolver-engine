import "./App.css";
import { EditorProvider, useEditor } from "./hooks/useEditorState";
import { TopBar } from "./components/TopBar";
import { EntitiesPanel } from "./panels/EntitiesPanel";
import { InspectorPanel } from "./panels/InspectorPanel";

function Layout() {
  const { panels } = useEditor();
  const hasLeft = panels.entities;
  const hasRight = panels.inspector;

  return (
    <div className="editor">
      <TopBar />
      <div
        className="editor-body"
        data-left={hasLeft || undefined}
        data-right={hasRight || undefined}
      >
        {hasLeft && (
          <aside className="editor-left">
            <EntitiesPanel />
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
