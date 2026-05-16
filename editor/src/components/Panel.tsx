import type { ReactNode } from "react";

interface PanelProps {
  children: ReactNode;
}

export function Panel({ children }: PanelProps) {
  return (
    <section className="panel">
      <div className="panel-content">{children}</div>
    </section>
  );
}
