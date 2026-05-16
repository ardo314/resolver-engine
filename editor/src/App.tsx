import "rc-dock/dist/rc-dock-dark.css";
import "./App.css";
import DockLayout, { type LayoutData, type TabData } from "rc-dock";
import { EditorProvider, useEditor } from "./hooks/useEditorState";
import { TopBar } from "./components/TopBar";
import { EntitiesPanel } from "./components/EntitiesPanel";
import { InspectorPanel } from "./components/InspectorPanel";
import { ComponentsPanel } from "./components/ComponentsPanel";

function loadTab(savedTab: TabData): TabData {
  switch (savedTab.id) {
    case "entities":
      return { ...savedTab, title: "Entities", closable: true, content: <EntitiesPanel /> };
    case "components":
      return { ...savedTab, title: "Components", closable: true, content: <ComponentsPanel /> };
    case "inspector":
      return { ...savedTab, title: "Inspector", closable: true, content: <InspectorPanel /> };
    default:
      return { ...savedTab, title: savedTab.id, content: <div /> };
  }
}

const defaultLayout: LayoutData = {
  dockbox: {
    mode: "horizontal",
    children: [
      {
        mode: "vertical",
        size: 260,
        children: [
          { tabs: [{ id: "entities" } as TabData] },
          { tabs: [{ id: "components" } as TabData] },
        ],
      },
      {
        size: 320,
        tabs: [{ id: "inspector" } as TabData],
      },
    ],
  },
};

function Layout() {
  const { connected, dockLayoutRef, loadTabRef, onDockLayoutChange } = useEditor();
  loadTabRef.current = loadTab;

  return (
    <div className="editor">
      <TopBar />
      {!connected && (
        <div className="connection-banner">Connecting to NATS...</div>
      )}
      <DockLayout
        ref={dockLayoutRef}
        defaultLayout={defaultLayout}
        loadTab={loadTab}
        onLayoutChange={onDockLayoutChange}
        style={{ position: "relative", flex: 1 }}
      />
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
