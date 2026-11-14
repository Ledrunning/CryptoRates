const regular = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

// Coins priced below one dollar need more digits to be readable.
const fine = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  minimumFractionDigits: 2,
  maximumFractionDigits: 8,
});

export const formatUsd = (value: number): string =>
  (value >= 1 ? regular : fine).format(value);

export function formatAgo(from: Date, now: number): string {
  const seconds = Math.max(0, Math.round((now - from.getTime()) / 1000));
  if (seconds < 5) return "just now";
  if (seconds < 60) return `${seconds} s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  return `${Math.floor(minutes / 60)} h ago`;
}
