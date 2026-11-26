export function LoadingState({ label }: { label: string }) {
  return (
    <div role="status" className="flex items-center gap-3 py-16 text-muted">
      <span
        aria-hidden="true"
        className="size-4 rounded-full border-2 border-rule border-t-action motion-safe:animate-spin"
      />
      {label}
    </div>
  );
}
