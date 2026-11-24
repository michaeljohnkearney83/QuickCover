"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ErrorMessage } from "@/components/ErrorMessage";
import { PolicyCard } from "@/components/PolicyCard";
import { api, type Policy, type Quote } from "@/lib/api";
import { useCustomerId } from "@/lib/customer";
import { formatDate, formatMoney, toDateInputValue } from "@/lib/format";

const MAX_DAYS_AHEAD = 30; // Matches PurchaseQuoteHandler.
const POLL_INTERVAL_MS = 1500;
const POLL_GIVE_UP_MS = 30_000;

type PolicyState =
  | { kind: "idle" }
  | { kind: "waiting"; paymentReference?: string }
  | { kind: "issued"; policy: Policy }
  | { kind: "slow" };

export function QuoteDetails() {
  const { quoteId } = useParams<{ quoteId: string }>();
  const customerId = useCustomerId();

  // Results are tagged with the request they answer, so switching customer or quote shows
  // "loading" again without resetting state inside the effect.
  const requestKey = customerId ? `${customerId}:${quoteId}` : null;
  const [loaded, setLoaded] = useState<{ key: string; quote?: Quote; error?: unknown } | null>(null);
  const [policyState, setPolicyState] = useState<PolicyState>({ kind: "idle" });

  // Load the quote. A purchased quote goes straight to looking up its policy.
  useEffect(() => {
    if (!requestKey) return;
    let cancelled = false;
    api
      .getQuote(quoteId)
      .then((quote) => {
        if (cancelled) return;
        setLoaded({ key: requestKey, quote });
        setPolicyState(quote.status === "Purchased" ? { kind: "waiting" } : { kind: "idle" });
      })
      .catch((error) => !cancelled && setLoaded({ key: requestKey, error }));
    return () => {
      cancelled = true;
    };
  }, [requestKey, quoteId]);

  const current = loaded?.key === requestKey ? loaded : null;
  const quote = current?.quote ?? null;
  const loadError = current?.error ?? null;

  // The policy worker issues the policy asynchronously, so poll until it appears.
  const waiting = policyState.kind === "waiting";
  useEffect(() => {
    if (!waiting) return;
    let cancelled = false;
    const startedAt = Date.now();
    let timer: ReturnType<typeof setTimeout>;

    const poll = async () => {
      try {
        const policy = await api.getPolicyForQuote(quoteId);
        if (cancelled) return;
        if (policy) return setPolicyState({ kind: "issued", policy });
      } catch {
        // Transient errors: keep polling until the give-up time.
      }
      if (cancelled) return;
      if (Date.now() - startedAt > POLL_GIVE_UP_MS) return setPolicyState({ kind: "slow" });
      timer = setTimeout(poll, POLL_INTERVAL_MS);
    };

    poll();
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [waiting, quoteId]);

  if (loadError) {
    return (
      <div className="space-y-4">
        <ErrorMessage error={loadError} />
        <Link href="/" className="btn-secondary">Get a new quote</Link>
      </div>
    );
  }

  if (!quote) return <p className="text-muted">Loading your quote…</p>;

  const expired = new Date(quote.expiresUtc) < new Date();

  return (
    <div className="space-y-6">
      <div className="card flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm text-muted">Your annual premium</p>
          <p className="text-4xl font-semibold tracking-tight">{formatMoney(quote.annualPremium)}</p>
          <p className="mt-1 text-sm text-muted">
            or about {formatMoney(quote.annualPremium / 12)} a month
          </p>
        </div>
        <dl className="text-right text-sm">
          <dt className="text-muted">Quote reference</dt>
          <dd className="font-mono">{quote.quoteId.slice(0, 8).toUpperCase()}</dd>
          <dt className="mt-2 text-muted">{expired ? "Expired" : "Valid until"}</dt>
          <dd>{formatDate(quote.expiresUtc)}</dd>
        </dl>
      </div>

      {policyState.kind === "idle" && !expired && (
        <BuyForm quote={quote} onPurchased={(paymentReference) => setPolicyState({ kind: "waiting", paymentReference })} />
      )}

      {policyState.kind === "idle" && expired && (
        <div className="card space-y-3">
          <p>This quote has expired. Prices can change, so please get a new one.</p>
          <Link href="/" className="btn">Get a new quote</Link>
        </div>
      )}

      {policyState.kind === "waiting" && (
        <div className="card flex items-center gap-3" role="status">
          <span className="size-4 animate-spin rounded-full border-2 border-accent border-t-transparent" />
          <div>
            <p className="font-medium">Payment taken. Issuing your policy…</p>
            {policyState.paymentReference && (
              <p className="text-sm text-muted">Payment reference {policyState.paymentReference}</p>
            )}
          </div>
        </div>
      )}

      {policyState.kind === "slow" && (
        <div className="card space-y-2">
          <p className="font-medium">Your payment went through, but the policy is taking longer than usual.</p>
          <p className="text-sm text-muted">
            It will appear in <Link href="/policies" className="underline">My policies</Link> as soon as it is issued.
          </p>
        </div>
      )}

      {policyState.kind === "issued" && (
        <div className="space-y-3">
          <h2 className="text-lg font-semibold">You’re covered</h2>
          <PolicyCard policy={policyState.policy} />
          <Link href="/policies" className="btn-secondary">View all my policies</Link>
        </div>
      )}
    </div>
  );
}

function BuyForm({ quote, onPurchased }: { quote: Quote; onPurchased: (paymentReference: string) => void }) {
  const today = new Date();
  const lastStart = new Date(today);
  lastStart.setDate(today.getDate() + MAX_DAYS_AHEAD);

  const [startDate, setStartDate] = useState(toDateInputValue(today));
  const [error, setError] = useState<unknown>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const purchase = await api.purchaseQuote(quote.quoteId, startDate);
      onPurchased(purchase.paymentReference);
    } catch (err) {
      setError(err);
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={onSubmit} className="card space-y-4">
      <h2 className="text-lg font-semibold">Buy this policy</h2>
      <div className="max-w-xs">
        <label htmlFor="startDate" className="label">Cover starts on</label>
        <input
          id="startDate"
          type="date"
          required
          min={toDateInputValue(today)}
          max={toDateInputValue(lastStart)}
          value={startDate}
          onChange={(e) => setStartDate(e.target.value)}
          className="input"
        />
        <p className="hint">Any day in the next {MAX_DAYS_AHEAD} days. Cover lasts 12 months.</p>
      </div>
      <ErrorMessage error={error} />
      <button type="submit" disabled={submitting} className="btn">
        {submitting ? "Taking payment…" : `Pay ${formatMoney(quote.annualPremium)}`}
      </button>
      <p className="text-xs text-muted">Payment is simulated. No card details are needed.</p>
    </form>
  );
}
