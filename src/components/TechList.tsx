export function TechList({ items }: { items: readonly string[] }) {
  if (items.length === 0) return null;
  return (
    <ul className="flex list-none flex-wrap gap-2 p-0">
      {items.map((item) => (
        <li key={item}>
          <span className="bg-chip text-accent inline-flex rounded-full px-3 py-1 text-xs font-medium">
            {item}
          </span>
        </li>
      ))}
    </ul>
  );
}
