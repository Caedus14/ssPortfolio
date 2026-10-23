import { cn } from '../lib/cn.ts';

function FilterChip({
  pressed,
  onClick,
  children,
}: {
  pressed: boolean;
  onClick: () => void;
  children: string;
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={onClick}
      className={cn(
        'min-h-10 rounded-full px-3 py-1.5 text-sm font-medium motion-safe:transition-colors',
        pressed
          ? 'bg-accent text-accent-ink'
          : 'bg-chip text-accent hover:bg-accent/15',
      )}
    >
      {children}
    </button>
  );
}

export function ProjectFilter({
  tags,
  active,
  onChange,
}: {
  tags: readonly string[];
  active: string | null;
  onChange: (tag: string | null) => void;
}) {
  return (
    <div>
      <p id="project-filter-label" className="text-muted text-sm font-medium">
        Filter by technology
      </p>
      <div
        role="group"
        aria-labelledby="project-filter-label"
        className="mt-3 flex flex-wrap gap-2"
      >
        <FilterChip pressed={active === null} onClick={() => onChange(null)}>
          All
        </FilterChip>
        {tags.map((tag) => (
          <FilterChip key={tag} pressed={active === tag} onClick={() => onChange(tag)}>
            {tag}
          </FilterChip>
        ))}
      </div>
    </div>
  );
}
