import { Link } from "react-router";
import { formatDateTime, modeLabels, phaseLabels, registrationStatus } from "./contestFormat";
import type { ContestSummary } from "./contestsApi";

interface ContestTableProps {
  caption: string;
  contests: ContestSummary[];
  linkTo: (contest: ContestSummary) => string;
  showPhase?: boolean;
}

export function ContestTable({ caption, contests, linkTo, showPhase = false }: ContestTableProps) {
  return (
    <div className="overflow-x-auto rounded-md border border-rule bg-surface">
      <table className="w-full min-w-[40rem] text-left tabular-nums">
        <caption className="sr-only">{caption}</caption>
        <thead>
          <tr className="border-b border-rule text-sm text-muted">
            <th scope="col" className="px-4 py-2 font-semibold">Contest</th>
            {showPhase && <th scope="col" className="px-4 py-2 font-semibold">Status</th>}
            <th scope="col" className="px-4 py-2 font-semibold">Mode</th>
            <th scope="col" className="px-4 py-2 font-semibold">Starts</th>
            <th scope="col" className="px-4 py-2 font-semibold">Registration</th>
          </tr>
        </thead>
        <tbody>
          {contests.map((contest) => (
            <tr key={contest.id} className="border-b border-rule last:border-b-0">
              <td className="px-4 py-3">
                <Link to={linkTo(contest)} className="font-semibold text-action hover:text-action-strong">
                  {contest.title}
                </Link>
              </td>
              {showPhase && <td className="px-4 py-3">{phaseLabels[contest.phase]}</td>}
              <td className="px-4 py-3">{modeLabels[contest.mode]}</td>
              <td className="px-4 py-3">{formatDateTime(contest.startsAt)}</td>
              <td className="px-4 py-3 text-muted">{registrationStatus(contest)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
