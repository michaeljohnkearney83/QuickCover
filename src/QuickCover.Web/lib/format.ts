const gbp = new Intl.NumberFormat("en-GB", { style: "currency", currency: "GBP" });
const longDate = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric" });

export const formatMoney = (amount: number) => gbp.format(amount);

/** Formats an ISO date ("2026-12-01") or timestamp. Plain dates are read as calendar dates, not UTC midnight. */
export function formatDate(value: string) {
  const plain = /^\d{4}-\d{2}-\d{2}$/.test(value);
  const date = plain ? new Date(`${value}T00:00:00`) : new Date(value);
  return longDate.format(date);
}

/** yyyy-mm-dd in the browser's local time zone, for <input type="date">. */
export function toDateInputValue(date: Date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}
