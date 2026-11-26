import { useAuth } from "../auth/AuthContext";
import { ButtonLink } from "../components/Button";
import { ProblemBalloon } from "../contests/ProblemBalloon";

const problems = ["A", "B", "C", "D"] as const;

const exampleStandings = [
  { rank: 1, name: "Null Pointers", solved: ["A", "B", "D"], penalty: 214 },
  { rank: 1, name: "Off By One", solved: ["A", "C", "D"], penalty: 214 },
  { rank: 3, name: "Segfault Squad", solved: ["A", "B"], penalty: 98 },
];

export function HomePage() {
  return (
    <>
      <section className="max-w-2xl">
        <h1 className="text-4xl leading-tight font-bold text-balance">Programming contests for KYAU Programming Club</h1>
        <p className="mt-4 text-lg text-muted">
          Take part on your own or in a team of up to three. Write and submit solutions in the browser, and follow the
          standings while the contest runs.
        </p>
        <HomeActions />
      </section>

      <section aria-labelledby="standings-heading" className="mt-14 grid gap-8 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <div>
          <h2 id="standings-heading" className="text-2xl font-bold">
            How standings work
          </h2>
          <div className="mt-4 overflow-x-auto rounded-md border border-rule bg-surface">
            <table className="w-full min-w-[30rem] text-left tabular-nums">
              <caption className="px-4 pt-3 text-left text-sm text-muted">Example standings</caption>
              <thead>
                <tr className="border-b border-rule text-sm text-muted">
                  <th scope="col" className="px-4 py-2 font-semibold">Rank</th>
                  <th scope="col" className="px-4 py-2 font-semibold">Team</th>
                  <th scope="col" className="px-4 py-2 text-right font-semibold">Solved</th>
                  <th scope="col" className="px-4 py-2 text-right font-semibold">Penalty</th>
                  <th scope="col" className="px-4 py-2 font-semibold">Balloons</th>
                </tr>
              </thead>
              <tbody>
                {exampleStandings.map((row) => (
                  <tr key={row.name} className="border-b border-rule last:border-b-0">
                    <td className="px-4 py-3">{row.rank}</td>
                    <td className="px-4 py-3 font-semibold">{row.name}</td>
                    <td className="px-4 py-3 text-right">{row.solved.length}</td>
                    <td className="px-4 py-3 text-right">{row.penalty}</td>
                    <td className="px-4 py-3">
                      <span className="flex gap-1.5" aria-label={`Solved ${row.solved.join(", ")}`}>
                        {problems.map((label) =>
                          row.solved.includes(label) ? (
                            <ProblemBalloon key={label} label={label} />
                          ) : (
                            <span key={label} aria-hidden="true" className="size-7 rounded-full border border-dashed border-muted/60" />
                          ),
                        )}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <ul className="space-y-3 text-muted lg:mt-12">
          <li>More solved problems ranks higher. Ties go to the lower total penalty.</li>
          <li>
            Penalty is the minutes from the contest start to each accepted solution, plus 20 minutes for every rejected
            attempt before it. Compilation errors don't count.
          </li>
          <li>Teams with the same solved count and penalty share a rank, and the next rank is skipped.</li>
          <li>Every problem has its own balloon colour, the same wherever that problem appears.</li>
        </ul>
      </section>
    </>
  );
}

function HomeActions() {
  const { state } = useAuth();

  if (state.status === "loading" || state.status === "unavailable") {
    return null;
  }

  if (state.status === "authenticated") {
    return (
      <p className="mt-6">
        Signed in as <span className="font-semibold">{state.user.fullName}</span>.
      </p>
    );
  }

  return (
    <div className="mt-6 flex flex-wrap gap-3">
      <ButtonLink to="/register">Create student account</ButtonLink>
      <ButtonLink to="/sign-in" variant="secondary">
        Sign in
      </ButtonLink>
    </div>
  );
}
