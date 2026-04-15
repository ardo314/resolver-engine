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
import { World, Entity, type RegisteredComponent } from "@engine/client";

export type PanelId = "entities" | "inspector" | "components";

export const PANEL_LABELS: Record<PanelId, string> = {
  entities: "Entities",
  inspector: "Inspector",
  components: "Components",
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
  registeredComponents: RegisteredComponent[];
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
    components: true,
  });
  const [entities, setEntities] = useState<EntityEntry[]>([]);
  const [registeredComponents, setRegisteredComponents] = useState<RegisteredComponent[]>([]);
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

  const fetchComponents = useCallback(async () => {
    const world = worldRef.current;
    if (!world) return;
    try {
      const components = await world.listComponents();
      setRegisteredComponents(components);
    } catch (e) {
      console.error("Failed to fetch components:", e);
    }
  }, []);

  useEffect(() => {
    let disposed = false;
    (async () => {
      try {
        const raw = window.__ENV__?.NATS_URL ?? import.meta.env.VITE_NATS_URL;
        const natsUrl = raw?.startsWith("/")
          ? `${location.protocol === "https:" ? "wss:" : "ws:"}//${location.host}${raw}`
          : raw;
        const nc = await connect({ servers: natsUrl });
        if (disposed) {
          await nc.close();
          return;
        }
        worldRef.current = new World(nc);
        setConnected(true);
        await Promise.all([fetchEntities(), fetchComponents()]);
      } catch (e) {
        console.error("NATS connection failed:", e);
      }
    })();
    return () => {
      disposed = true;
    };
  }, [fetchEntities, fetchComponents]);

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
        registeredComponents,
        selectedEntityId,
        togglePanel,
        selectEntity,
        createEntity: createEntityFn,
        deleteEntity: deleteEntityFn,
        refresh: async () => {
          await Promise.all([fetchEntities(), fetchComponents()]);
        },
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
