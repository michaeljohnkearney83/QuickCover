import type { NextRequest } from "next/server";

// Backend-for-frontend proxy. The browser only ever talks to this app; this handler forwards
// /api/quotes/* and /api/policies/* to the Quote API. That means:
//  - no CORS configuration on the API,
//  - the API can stay on an internal address (a ClusterIP service in AKS),
//  - API_URL is read per request, so one Docker image works in every environment.
// When Entra sign-in arrives, this is where the access token gets attached.

const ALLOWED_ROOTS = new Set(["quotes", "policies"]);
const FORWARDED_REQUEST_HEADERS = ["content-type", "accept", "x-customer-id"];
const FORWARDED_RESPONSE_HEADERS = ["content-type", "content-length", "content-disposition", "location"];

async function proxy(request: NextRequest, ctx: RouteContext<"/api/[...path]">) {
  const { path } = await ctx.params;

  if (!ALLOWED_ROOTS.has(path[0])) {
    return Response.json({ title: "Not found", status: 404 }, { status: 404 });
  }

  const apiUrl = process.env.API_URL ?? "http://localhost:5080";
  const target = new URL(`/api/${path.map(encodeURIComponent).join("/")}`, apiUrl);
  target.search = request.nextUrl.search;

  const headers = new Headers();
  for (const name of FORWARDED_REQUEST_HEADERS) {
    const value = request.headers.get(name);
    if (value) headers.set(name, value);
  }

  let upstream: Response;
  try {
    upstream = await fetch(target, {
      method: request.method,
      headers,
      body: request.method === "GET" || request.method === "HEAD" ? undefined : await request.text(),
      cache: "no-store",
      redirect: "manual",
    });
  } catch {
    return Response.json(
      { title: "Quote service unavailable", status: 502, detail: "Could not reach the Quote API." },
      { status: 502 },
    );
  }

  const responseHeaders = new Headers();
  for (const name of FORWARDED_RESPONSE_HEADERS) {
    const value = upstream.headers.get(name);
    if (value) responseHeaders.set(name, value);
  }

  return new Response(upstream.body, { status: upstream.status, headers: responseHeaders });
}

export { proxy as GET, proxy as POST };
