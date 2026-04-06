import type { ReactNode } from "react";
import type { PanelId } from "../hooks/useEditorState";
import { useEditor } from "../hooks/useEditorState";

interface PanelProps {
  id: PanelId;
  title: string;
  children: ReactNode;
}

export function Panel({ id, title, children }: PanelProps) {
  const { togglePanel } = useEditor();

  return (
    <section className="panel">
      <div className="panel-header">
        <span className="panel-title">{title}</span>
        <button
          className="panel-close"
          onClick={() => togglePanel(id)}
          aria-label={`Close ${title}`}
        >
          ×
        </button>
      </div>
      <div className="panel-content">{children}</div>
    </section>
  );
}
