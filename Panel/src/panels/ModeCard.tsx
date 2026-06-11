import { useState } from "react";
import Card from "../components/Card";
import Segmented from "../components/Segmented";
import { useToast } from "../components/Toast";
import { useStore } from "../state/store";
import { useT } from "../i18n";
import { api } from "../lib/api";
import type { GameMode } from "../lib/api";
import "./ModeCard.css";

export default function ModeCard() {
  const { mode, config, csgoPath, applyMode, reportError, modePending } = useStore();
  const toast = useToast();
  const t = useT();
  const [pending, setPending] = useState<GameMode | null>(null);
  const OPTIONS: { value: GameMode; label: string }[] = [
    { value: "online", label: t("mode.online") },
    { value: "bots", label: t("mode.bot") },
  ];

  // Optimistic: show the clicked option immediately; revert if the op fails.
  const current: GameMode | null =
    pending ?? mode?.current ?? ((config?.mode as GameMode | null) ?? null);

  // Yellow only if the mode was changed while CS2 is running (pending restart).
  const tone = mode?.cs2_running && modePending ? "yellow" : "green";

  const onChange = async (m: GameMode) => {
    setPending(m);
    const info = await applyMode(m);
    setPending(null);
    if (!info) return;
    toast.show(m === "bots" ? "-insecure on" : "-insecure off", "green");
  };

  const launch = async () => {
    toast.show(t("mode.launching"));
    try {
      await api.launchCs2();
    } catch (e) {
      reportError(e);
    }
  };

  return (
    <Card title={t("mode.title")}>
      <Segmented
        ariaLabel="Game mode"
        value={current}
        onChange={onChange}
        disabled={!csgoPath}
        options={OPTIONS.map((o) => ({
          ...o,
          tone: o.value === current ? (tone as "green" | "yellow") : undefined,
        }))}
      />
      <button className="mode__launch" disabled={!csgoPath} onClick={launch}>
        {t("mode.launch")}
      </button>
    </Card>
  );
}
