import type { ButtonHTMLAttributes } from "react";
import { Link, type LinkProps } from "react-router";

type Variant = "primary" | "secondary" | "danger";

const base =
  "inline-flex items-center justify-center rounded-md px-4 py-2 text-base font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-60";

const variants: Record<Variant, string> = {
  primary: "bg-action text-white hover:bg-action-strong",
  secondary: "border border-rule bg-surface text-ink hover:border-action hover:text-action-strong",
  danger: "bg-danger text-white hover:brightness-90",
};

function classes(variant: Variant, className?: string): string {
  return [base, variants[variant], className].filter(Boolean).join(" ");
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  busy?: boolean;
}

export function Button({ variant = "primary", busy = false, disabled, className, type = "button", ...props }: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled || busy}
      aria-busy={busy || undefined}
      className={classes(variant, className)}
      {...props}
    />
  );
}

interface ButtonLinkProps extends LinkProps {
  variant?: Variant;
}

export function ButtonLink({ variant = "primary", className, ...props }: ButtonLinkProps) {
  return <Link className={classes(variant, className)} {...props} />;
}
