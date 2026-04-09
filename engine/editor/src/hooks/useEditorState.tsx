import {
  createContext,
  useContext,
  useState,
  useCallback,
  useEffect,
  useRef,
  type ReactNode,
} from "react";
import { connect } from "nats";
import { World, Entity } from "@engine/client";

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
  properties: PropertyEntry[];
}

export interface PropertyEntry {
  name: string;
  value: string;
}

interface EditorState {
  connected: boolean;
  panels: Record<PanelId, boolean>;
  entities: EntityEntry[];
  selectedEntityId: string | null;
  togglePanel: (id: PanelId) => void;
  selectEntity: (id: string | null) => void;
  createEntity: () => Promise<void>;
  deleteEntity: (id: string) => Promise<void>;
  refresh: () => Promise<void>;
}

const EditorContext = createContext<EditorState | null>(null);

export function EditorProvider({ children }: { children: ReactNode }) {
  const worldRef = useRef<World | null>(null);
  const [connected, setConnected] = useState(false);
  const [panels, setPanels] = useState<Record<PanelId, boolean>>({
    entities: true,
    inspector: true,
  });
  const [entities, setEntities] = useState<EntityEntry[]>([]);
  const [selectedEntityId, setSelectedEntityId] = useState<string | null>(null);

  const fetchEntities = useCallback(async () => {
    const world = worldRef.current;
    if (!world) return;
    try {
      const entityList = await world.listEntities();

      const entries: EntityEntry[] = [];
      for (const entity of entityList) {
        const components = await entity.getComponentEntries();
        entries.push({ id: entity.id, components });
      }
      setEntities(entries);
    } catch (e) {
      console.error("Failed to fetch entities:", e);
    }
  }, []);

  useEffect(() => {
    let disposed = false;
    (async () => {
      try {
        const natsUrl = window.__ENV__?.NATS_URL ?? import.meta.env.VITE_NATS_URL;
        const nc = await connect({ servers: natsUrl });
        if (disposed) {
          await nc.close();
          return;
        }
        worldRef.current = new World(nc);
        setConnected(true);
        await fetchEntities();
      } catch (e) {
        console.error("NATS connection failed:", e);
      }
    })();
    return () => {
      disposed = true;
    };
  }, [fetchEntities]);

  const togglePanel = useCallback((id: PanelId) => {
    setPanels((prev) => ({ ...prev, [id]: !prev[id] }));
  }, []);

  const selectEntity = useCallback((id: string | null) => {
    setSelectedEntityId(id);
  }, []);

  const createEntityFn = useCallback(async () => {
    const world = worldRef.current;
    if (!world) return;
    await world.createEntity();
    await fetchEntities();
  }, [fetchEntities]);

  const deleteEntityFn = useCallback(
    async (id: string) => {
      const world = worldRef.current;
      if (!world) return;
      await world.deleteEntity(id as Entity["id"]);
      if (selectedEntityId === id) {
        setSelectedEntityId(null);
      }
      await fetchEntities();
    },
    [fetchEntities, selectedEntityId],
  );

  return (
    <EditorContext
      value={{
        connected,
        panels,
        entities,
        selectedEntityId,
        togglePanel,
        selectEntity,
        createEntity: createEntityFn,
        deleteEntity: deleteEntityFn,
        refresh: fetchEntities,
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
