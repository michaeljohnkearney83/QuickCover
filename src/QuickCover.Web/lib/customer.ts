"use client";

import { useSyncExternalStore } from "react";

// Stand-in for sign-in until Entra ID arrives: a customer id kept in localStorage and sent as
// the X-Customer-Id header. Anyone can type any id, so this is for local demos only.

const STORAGE_KEY = "quickcover.customerId";
const CHANGE_EVENT = "quickcover:customer-changed";

function newCustomerId() {
  return `cust-${crypto.randomUUID().slice(0, 8)}`;
}

export function getCustomerId(): string {
  let id = localStorage.getItem(STORAGE_KEY);
  if (!id) {
    id = newCustomerId();
    localStorage.setItem(STORAGE_KEY, id);
  }
  return id;
}

export function setCustomerId(id: string) {
  localStorage.setItem(STORAGE_KEY, id.trim() || newCustomerId());
  window.dispatchEvent(new Event(CHANGE_EVENT));
}

function subscribe(onChange: () => void) {
  window.addEventListener(CHANGE_EVENT, onChange);
  window.addEventListener("storage", onChange);
  return () => {
    window.removeEventListener(CHANGE_EVENT, onChange);
    window.removeEventListener("storage", onChange);
  };
}

/** The current customer id, or null during server rendering. */
export function useCustomerId(): string | null {
  return useSyncExternalStore(subscribe, getCustomerId, () => null);
}
