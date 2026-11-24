import type { Policy } from "@/lib/api";
import { formatDate, formatMoney } from "@/lib/format";

export function PolicyCard({ policy }: { policy: Policy }) {
  const today = new Date().toISOString().slice(0, 10);
  const status = policy.startDate > today ? "Starts soon" : policy.endDate < today ? "Expired" : "Active";

  return (
    <div className="card grid gap-4 sm:grid-cols-4">
      <div className="sm:col-span-2">
        <p className="text-sm text-muted">Policy number</p>
        <p className="font-mono text-lg">{policy.policyNumber}</p>
      </div>
      <div>
        <p className="text-sm text-muted">Annual premium</p>
        <p className="font-medium">{formatMoney(policy.annualPremium)}</p>
      </div>
      <div>
        <p className="text-sm text-muted">Status</p>
        <p className="font-medium">{status}</p>
      </div>
      <div className="sm:col-span-4 text-sm text-muted">
        Cover from {formatDate(policy.startDate)} to {formatDate(policy.endDate)}
      </div>
    </div>
  );
}
