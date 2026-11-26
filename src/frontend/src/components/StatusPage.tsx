import { ButtonLink } from "./Button";

export function StatusPage({ title, message }: { title: string; message: string }) {
  return (
    <section className="max-w-xl py-12">
      <h1 className="text-3xl font-bold">{title}</h1>
      <p className="mt-3 text-muted">{message}</p>
      <ButtonLink to="/" variant="secondary" className="mt-6">
        Go to the home page
      </ButtonLink>
    </section>
  );
}
