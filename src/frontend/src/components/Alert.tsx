import type { ReactNode } from "react";

type Tone = "error" | "success";

const tones: Record<Tone, string> = {
  error: "border-danger text-danger",
  success: "border-success text-success",
};

export function Alert({ tone, children }: { tone: Tone; children: ReactNode }) {
  return (
    <div role={tone === "error" ? "alert" : "status"} className={`border-l-4 bg-surface px-4 py-3 ${tones[tone]}`}>
      {children}
    </div>
  );
}
