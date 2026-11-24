"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ErrorMessage } from "@/components/ErrorMessage";
import { api, type VehicleUse } from "@/lib/api";

export default function QuotePage() {
  const router = useRouter();
  const [error, setError] = useState<unknown>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    setSubmitting(true);
    setError(null);
    try {
      const quote = await api.createQuote({
        driverAge: Number(form.get("driverAge")),
        yearsLicensed: Number(form.get("yearsLicensed")),
        claimsLast5Years: Number(form.get("claimsLast5Years")),
        vehicleValue: Number(form.get("vehicleValue")),
        use: form.get("use") as VehicleUse,
        postcodeArea: String(form.get("postcodeArea")).trim().toUpperCase(),
      });
      router.push(`/quotes/${quote.quoteId}`);
    } catch (err) {
      setError(err);
      setSubmitting(false);
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Get a taxi insurance quote</h1>
        <p className="mt-1 text-muted">Six questions. Your price is valid for 30 days.</p>
      </div>

      <form onSubmit={onSubmit} className="card grid gap-5 sm:grid-cols-2">
        <div>
          <label htmlFor="driverAge" className="label">Driver age</label>
          <input id="driverAge" name="driverAge" type="number" min={21} max={80} required defaultValue={34} className="input" />
          <p className="hint">21 to 80</p>
        </div>

        <div>
          <label htmlFor="yearsLicensed" className="label">Years with a full licence</label>
          <input id="yearsLicensed" name="yearsLicensed" type="number" min={0} max={63} required defaultValue={12} className="input" />
        </div>

        <div>
          <label htmlFor="claimsLast5Years" className="label">Claims in the last 5 years</label>
          <input id="claimsLast5Years" name="claimsLast5Years" type="number" min={0} max={10} required defaultValue={0} className="input" />
        </div>

        <div>
          <label htmlFor="vehicleValue" className="label">Vehicle value (£)</label>
          <input id="vehicleValue" name="vehicleValue" type="number" min={1000} max={150000} step={100} required defaultValue={18000} className="input" />
          <p className="hint">£1,000 to £150,000</p>
        </div>

        <fieldset>
          <legend className="label">Type of taxi work</legend>
          <div className="flex gap-4 pt-1">
            <label className="flex items-center gap-2">
              <input type="radio" name="use" value="PrivateHire" defaultChecked className="accent-accent" />
              Private hire
            </label>
            <label className="flex items-center gap-2">
              <input type="radio" name="use" value="PublicHire" className="accent-accent" />
              Public hire
            </label>
          </div>
        </fieldset>

        <div>
          <label htmlFor="postcodeArea" className="label">Postcode area</label>
          <input
            id="postcodeArea"
            name="postcodeArea"
            required
            pattern="[A-Za-z]{1,2}"
            maxLength={2}
            defaultValue="L"
            className="input uppercase"
          />
          <p className="hint">The letters at the start of your postcode, e.g. L or CH</p>
        </div>

        <div className="space-y-4 sm:col-span-2">
          <ErrorMessage error={error} />
          <button type="submit" disabled={submitting} className="btn w-full sm:w-auto">
            {submitting ? "Pricing…" : "Get my price"}
          </button>
        </div>
      </form>
    </div>
  );
}
