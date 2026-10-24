/** "Eau Claire, WI". City and region only — this helper has no street or ZIP slot. */
export function formatLocation(location: { city: string; region: string }): string {
  return `${location.city}, ${location.region}`;
}
