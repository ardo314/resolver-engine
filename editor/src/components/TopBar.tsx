import { useState, useRef, useEffect } from "react";
import {
  type PanelId,
  PANEL_LABELS,
  useEditor,
} from "../hooks/useEditorState";

export function TopBar() {
  const { isPanelOpen, togglePanel } = useEditor();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menuOpen) return;
    function handleClick(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, [menuOpen]);

  return (
    <header className="topbar">
      <span className="topbar-title">Component Engine</span>
      <nav className="topbar-menus">
        <div className="topbar-menu" ref={menuRef}>
          <button
            className="topbar-menu-trigger"
            onClick={() => setMenuOpen((v) => !v)}
          >
            Window
          </button>
          {menuOpen && (
            <ul className="topbar-dropdown">
              {(Object.keys(PANEL_LABELS) as PanelId[]).map((id) => (
                <li key={id}>
                  <button
                    className="topbar-dropdown-item"
                    onClick={() => {
                      togglePanel(id);
                      setMenuOpen(false);
                    }}
                  >
                    <span className="check">{isPanelOpen(id) ? "✓" : ""}</span>
                    {PANEL_LABELS[id]}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </nav>
    </header>
  );
}
