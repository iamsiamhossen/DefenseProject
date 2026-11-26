const balloonClasses = [
  "bg-balloon-a",
  "bg-balloon-b",
  "bg-balloon-c",
  "bg-balloon-d",
  "bg-balloon-e",
  "bg-balloon-f",
  "bg-balloon-g",
  "bg-balloon-h",
  "bg-balloon-i",
  "bg-balloon-j",
] as const;

export function balloonClass(label: string): string {
  const index = (label.trim().toUpperCase().charCodeAt(0) - 65) % balloonClasses.length;
  return balloonClasses[index >= 0 ? index : 0] ?? balloonClasses[0];
}

export function ProblemBalloon({ label, title }: { label: string; title?: string }) {
  return (
    <span
      title={title}
      className={`inline-flex size-7 shrink-0 items-center justify-center rounded-full text-sm font-bold text-white ${balloonClass(label)}`}
    >
      {label}
    </span>
  );
}
