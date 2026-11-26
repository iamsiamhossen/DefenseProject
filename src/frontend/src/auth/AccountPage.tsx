import { useAuth } from "./AuthContext";

export function AccountPage() {
  const { state } = useAuth();
  if (state.status !== "authenticated") {
    return null;
  }

  const { user } = state;
  const details = [
    { term: "Full name", value: user.fullName },
    { term: "Email", value: user.email },
    ...(user.studentId ? [{ term: "Student ID", value: user.studentId }] : []),
    { term: user.roles.length > 1 ? "Roles" : "Role", value: user.roles.join(", ") },
  ];

  return (
    <section className="max-w-xl">
      <h1 className="text-3xl font-bold">Your account</h1>
      <dl className="mt-6 divide-y divide-rule rounded-md border border-rule bg-surface">
        {details.map((detail) => (
          <div key={detail.term} className="grid gap-1 px-4 py-3 sm:grid-cols-[10rem_1fr]">
            <dt className="text-muted">{detail.term}</dt>
            <dd className="font-semibold break-words">{detail.value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
