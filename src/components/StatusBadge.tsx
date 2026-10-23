import type { ProjectStatus } from '../data/types.ts';

const labels: Record<ProjectStatus, string> = {
  live: 'Live',
  'in-progress': 'In progress',
  archived: 'Archived',
};

export function StatusBadge({ status }: { status: ProjectStatus }) {
  return (
    <span className="badge" data-status={status}>
      <span className="badge-dot" aria-hidden="true" />
      {labels[status]}
    </span>
  );
}
