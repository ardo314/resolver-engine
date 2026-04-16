import {
  createContext,
  useContext,
  useState,
  useCallback,
  useEffect,
  useRef,
  type ReactNode,
  type RefObject,
} from "react";
import { connect } from "nats";
import { World, Entity, type RegisteredComponent } from "@engine/client";
import type DockLayout from "rc-dock";
import type { TabData } from "rc-dock";

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
  dockLayoutRef: RefObject<DockLayout | null>;
  loadTabRef: RefObject<((tab: TabData) => TabData) | null>;
  layoutVersion: number;
  entities: EntityEntry[];
  registeredComponents: RegisteredComponent[];
  selectedEntityId: string | null;
  isPanelOpen: (id: PanelId) => boolean;
  togglePanel: (id: PanelId) => void;
  onDockLayoutChange: () => void;
  selectEntity: (id: string | null) => void;
  createEntity: () => Promise<void>;
  deleteEntity: (id: string) => Promise<void>;
  addComponentToEntity: (entityId: string, componentId: string) => Promise<void>;
  refresh: () => Promise<void>;
}

const EditorContext = createContext<EditorState | null>(null);

export function EditorProvider({ children }: { children: ReactNode }) {
  const worldRef = useRef<World | null>(null);
  const dockLayoutRef = useRef<DockLayout | null>(null);
  const loadTabRef = useRef<((tab: TabData) => TabData) | null>(null);
  const [connected, setConnected] = useState(false);
  const [layoutVersion, setLayoutVersion] = useState(0);
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

  const isPanelOpen = useCallback(
    (id: PanelId) => !!dockLayoutRef.current?.find(id),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [layoutVersion],
  );

  const togglePanel = useCallback((id: PanelId) => {
    const dock = dockLayoutRef.current;
    if (!dock) return;
    const existing = dock.find(id);
    if (existing) {
      dock.dockMove(existing as Parameters<DockLayout["dockMove"]>[0], null, "remove");
    } else {
      const tab = loadTabRef.current?.({ id } as TabData) ?? ({ id } as TabData);
      dock.dockMove(tab as Parameters<DockLayout["dockMove"]>[0], null, "float");
    }
    setLayoutVersion((v) => v + 1);
  }, []);

  const onDockLayoutChange = useCallback(() => {
    setLayoutVersion((v) => v + 1);
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

  const addComponentToEntityFn = useCallback(
    async (entityId: string, componentId: string) => {
      const world = worldRef.current;
      if (!world) return;
      await world.addComponentById(entityId as Entity["id"], componentId);
      await fetchEntities();
    },
    [fetchEntities],
  );

  return (
    <EditorContext
      value={{
        connected,
        dockLayoutRef,
        loadTabRef,
        layoutVersion,
        entities,
        registeredComponents,
        selectedEntityId,
        isPanelOpen,
        togglePanel,
        onDockLayoutChange,
        selectEntity,
        createEntity: createEntityFn,
        deleteEntity: deleteEntityFn,
        addComponentToEntity: addComponentToEntityFn,
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
