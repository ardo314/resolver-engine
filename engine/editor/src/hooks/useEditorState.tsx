import {
  createContext,
  useContext,
  useState,
  useCallback,
  type ReactNode,
} from "react";

export type PanelId = "entities" | "inspector";

export const PANEL_LABELS: Record<PanelId, string> = {
  entities: "Entities",
  inspector: "Inspector",
};

export interface EntityEntry {
  id: string;
  components: ComponentEntry[];
}

export interface ComponentEntry {
  componentId: string;
  schemas: SchemaEntry[];
}

export interface SchemaEntry {
  schemaId: string;
  properties: PropertyEntry[];
}

export interface PropertyEntry {
  name: string;
  value: string;
}

interface EditorState {
  panels: Record<PanelId, boolean>;
  entities: EntityEntry[];
  selectedEntityId: string | null;
  togglePanel: (id: PanelId) => void;
  selectEntity: (id: string | null) => void;
}

const EditorContext = createContext<EditorState | null>(null);

const DEMO_ENTITIES: EntityEntry[] = [
  {
    id: "1743292800000-0",
    components: [
      {
        componentId: "in-memory.name",
        schemas: [
          {
            schemaId: "in-memory.name",
            properties: [{ name: "value", value: '"Player"' }],
          },
        ],
      },
      {
        componentId: "in-memory.pose",
        schemas: [
          {
            schemaId: "in-memory.pose",
            properties: [{ name: "value", value: "[0, 1, 0, 0, 0, 0]" }],
          },
        ],
      },
    ],
  },
  {
    id: "1743292800000-1",
    components: [
      {
        componentId: "in-memory.name",
        schemas: [
          {
            schemaId: "in-memory.name",
            properties: [{ name: "value", value: '"Camera"' }],
          },
        ],
      },
      {
        componentId: "in-memory.follow-pose",
        schemas: [
          {
            schemaId: "in-memory.follow-pose",
            properties: [{ name: "target", value: '"1743292800000-0"' }],
          },
        ],
      },
    ],
  },
  {
    id: "1743292800000-2",
    components: [
      {
        componentId: "in-memory.name",
        schemas: [
          {
            schemaId: "in-memory.name",
            properties: [{ name: "value", value: '"Ground"' }],
          },
        ],
      },
      {
        componentId: "in-memory.pose",
        schemas: [
          {
            schemaId: "in-memory.pose",
            properties: [{ name: "value", value: "[0, 0, 0, 0, 0, 0]" }],
          },
        ],
      },
      {
        componentId: "in-memory.parent",
        schemas: [
          {
            schemaId: "in-memory.parent",
            properties: [{ name: "value", value: '"1743292800000-0"' }],
          },
        ],
      },
    ],
  },
];

export function EditorProvider({ children }: { children: ReactNode }) {
  const [panels, setPanels] = useState<Record<PanelId, boolean>>({
    entities: true,
    inspector: true,
  });
  const [entities] = useState<EntityEntry[]>(DEMO_ENTITIES);
  const [selectedEntityId, setSelectedEntityId] = useState<string | null>(null);

  const togglePanel = useCallback((id: PanelId) => {
    setPanels((prev) => ({ ...prev, [id]: !prev[id] }));
  }, []);

  const selectEntity = useCallback((id: string | null) => {
    setSelectedEntityId(id);
  }, []);

  return (
    <EditorContext
      value={{
        panels,
        entities,
        selectedEntityId,
        togglePanel,
        selectEntity,
      }}
    >
      {children}
    </EditorContext>
  );
}

export function useEditor(): EditorState {
  const ctx = useContext(EditorContext);
  if (!ctx) throw new Error("useEditor must be used within EditorProvider");
  return ctx;
}
