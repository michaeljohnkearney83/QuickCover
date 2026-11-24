"use client";

import { useState } from "react";
import { api, type Policy } from "@/lib/api";

/**
 * Downloads the policy schedule PDF. A plain link can't send the X-Customer-Id header,
 * so this fetches the file and hands the browser a temporary object URL to save.
 */
export function DownloadPolicyButton({ policy }: { policy: Policy }) {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function download() {
    setBusy(true);
    setMessage(null);
    try {
      const pdf = await api.getPolicyDocument(policy.policyId);
      const url = URL.createObjectURL(pdf);
      const link = document.createElement("a");
      link.href = url;
      link.download = `${policy.policyNumber}.pdf`;
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 10_000);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Could not download the document.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-3">
      <button type="button" onClick={download} disabled={busy} className="btn-secondary py-1.5 text-sm">
        {busy ? "Preparing…" : "Download policy schedule (PDF)"}
      </button>
      {message && (
        <p role="status" className="text-sm text-muted">
          {message}
        </p>
      )}
    </div>
  );
}
