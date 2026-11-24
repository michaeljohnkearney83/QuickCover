"use client";

import { getCustomerId } from "./customer";

// Shapes returned by the Quote API (see src/QuickCover.QuoteApi/QuoteEndpoints.cs).

export type VehicleUse = "PrivateHire" | "PublicHire";

export interface CreateQuoteRequest {
  driverAge: number;
  yearsLicensed: number;
  claimsLast5Years: number;
  vehicleValue: number;
  use: VehicleUse;
  postcodeArea: string;
}

export interface Quote {
  quoteId: string;
  annualPremium: number;
  status: "Open" | "Purchased";
  createdUtc: string;
  expiresUtc: string;
}

export interface Purchase {
  purchaseId: string;
  quoteId: string;
  paymentReference: string;
  policyStatus: string;
}

export interface Policy {
  policyId: string;
  policyNumber: string;
  quoteId: string;
  annualPremium: number;
  startDate: string;
  endDate: string;
  issuedUtc: string;
}

/** An RFC 7807 problem response from the API, flattened into an Error. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    title: string,
    public readonly errors: string[] = [],
  ) {
    super(title);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      "X-Customer-Id": getCustomerId(),
      ...init?.headers,
    },
    cache: "no-store",
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    const errors: string[] = Array.isArray(problem.errors) ? problem.errors : [];
    throw new ApiError(
      response.status,
      problem.detail ?? problem.message ?? problem.title ?? `Request failed (${response.status})`,
      errors,
    );
  }

  return response.json() as Promise<T>;
}

export const api = {
  createQuote: (body: CreateQuoteRequest) =>
    request<Quote>("/quotes", { method: "POST", body: JSON.stringify(body) }),

  getQuote: (quoteId: string) => request<Quote>(`/quotes/${quoteId}`),

  purchaseQuote: (quoteId: string, startDate: string) =>
    request<Purchase>(`/quotes/${quoteId}/purchase`, { method: "POST", body: JSON.stringify({ startDate }) }),

  /** Resolves to null while the policy worker is still issuing the policy (API returns 404). */
  getPolicyForQuote: async (quoteId: string) => {
    try {
      return await request<Policy>(`/quotes/${quoteId}/policy`);
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) return null;
      throw e;
    }
  },

  listPolicies: () => request<Policy[]>("/policies"),
};
