"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { ErrorMessage } from "@/components/ErrorMessage";
import { PolicyCard } from "@/components/PolicyCard";
import { api, type Policy } from "@/lib/api";
import { useCustomerId } from "@/lib/customer";

export default function PoliciesPage() {
  const customerId = useCustomerId();
  // Results are tagged with the customer they belong to, so switching customer shows "loading" again.
  const [loaded, setLoaded] = useState<{ customerId: string; policies?: Policy[]; error?: unknown } | null>(null);

  useEffect(() => {
    if (!customerId) return;
    let cancelled = false;
    api
      .listPolicies()
      .then((policies) => !cancelled && setLoaded({ customerId, policies }))
      .catch((error) => !cancelled && setLoaded({ customerId, error }));
    return () => {
      cancelled = true;
    };
  }, [customerId]);

  const current = loaded?.customerId === customerId ? loaded : null;
  const policies = current?.policies ?? null;
  const error = current?.error ?? null;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold tracking-tight">My policies</h1>

      <ErrorMessage error={error} />

      {!error && policies === null && <p className="text-muted">Loading…</p>}

      {policies?.length === 0 && (
        <div className="card space-y-3">
          <p>You don’t have any policies yet.</p>
          <Link href="/" className="btn">Get a quote</Link>
        </div>
      )}

      {policies && policies.length > 0 && (
        <ul className="space-y-4">
          {policies.map((p) => (
            <li key={p.policyId}>
              <PolicyCard policy={p} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
