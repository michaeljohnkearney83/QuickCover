# QuickCover.Web

Next.js frontend for QuickCover: get a quote, buy it, see "My policies". See the repository README for the whole system.

```bash
npm install
npm run dev        # http://localhost:3080, expects the Quote API on http://localhost:5080
```

`API_URL` sets where the Quote API lives (default `http://localhost:5080`). It is read at request time by the proxy
route `app/api/[...path]/route.ts`, so the browser never calls the API directly and the API needs no CORS settings.

The signed-in customer is a demo id kept in localStorage and sent as `X-Customer-Id`. Click it in the header to switch.
