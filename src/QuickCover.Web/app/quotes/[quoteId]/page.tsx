import { Suspense } from "react";
import { QuoteDetails } from "./QuoteDetails";

export default function QuoteDetailsPage() {
  // The quote id is only known at request time, so the details stream in behind this boundary.
  return (
    <Suspense fallback={<p className="text-muted">Loading your quote…</p>}>
      <QuoteDetails />
    </Suspense>
  );
}
