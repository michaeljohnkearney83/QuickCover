"use client";

import { useState } from "react";
import { setCustomerId, useCustomerId } from "@/lib/customer";

/** Shows the demo customer id and lets you switch to another one (stand-in for sign-in). */
export function CustomerSwitcher() {
  const customerId = useCustomerId();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState("");

  if (customerId === null) return null;

  if (!editing) {
    return (
      <button
        type="button"
        onClick={() => {
          setDraft(customerId);
          setEditing(true);
        }}
        className="text-sm text-muted hover:text-foreground"
        title="Demo sign-in: switch customer"
      >
        Signed in as <span className="font-mono text-foreground">{customerId}</span>
      </button>
    );
  }

  return (
    <form
      className="flex items-center gap-2"
      onSubmit={(e) => {
        e.preventDefault();
        setCustomerId(draft);
        setEditing(false);
      }}
    >
      <label htmlFor="customer-id" className="sr-only">
        Customer id
      </label>
      <input
        id="customer-id"
        autoFocus
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        className="input w-40 py-1 font-mono text-sm"
      />
      <button type="submit" className="btn py-1 text-sm">
        Switch
      </button>
      <button type="button" onClick={() => setEditing(false)} className="text-sm text-muted hover:text-foreground">
        Cancel
      </button>
    </form>
  );
}
