import type { ReactNode } from "react";
import { formatDateTime, formatDuration, localTimeZone, modeLabels, phaseLabels, registrationStatus } from "./contestFormat";
import type { ContestDetails } from "./contestsApi";

export function ContestDetailsView({ contest, extraDetails = [] }: { contest: ContestDetails; extraDetails?: { term: string; value: ReactNode }[] }) {
  const details: { term: string; value: ReactNode }[] = [
    { term: "Status", value: phaseLabels[contest.phase] },
    { term: "Participation", value: modeLabels[contest.mode] },
    { term: "Registration", value: `${formatDateTime(contest.registrationOpensAt)} to ${formatDateTime(contest.registrationClosesAt)}` },
    { term: "Starts", value: formatDateTime(contest.startsAt) },
    { term: "Duration", value: formatDuration(contest.durationMinutes) },
    { term: "Ends", value: formatDateTime(contest.endsAt) },
    { term: "Standings freeze", value: contest.freezeAt ? formatDateTime(contest.freezeAt) : "No freeze" },
    ...extraDetails,
  ];

  return (
    <>
      <p className="mt-2 text-lg text-muted">{registrationStatus(contest)}</p>
      {contest.description && <p className="mt-6 max-w-prose whitespace-pre-line">{contest.description}</p>}
      <dl className="mt-6 max-w-2xl divide-y divide-rule rounded-md border border-rule bg-surface tabular-nums">
        {details.map((detail) => (
          <div key={detail.term} className="grid gap-1 px-4 py-3 sm:grid-cols-[12rem_1fr]">
            <dt className="text-muted">{detail.term}</dt>
            <dd className="font-semibold">{detail.value}</dd>
          </div>
        ))}
      </dl>
      <p className="mt-3 text-sm text-muted">Times are shown in your time zone ({localTimeZone()}).</p>
    </>
  );
}
