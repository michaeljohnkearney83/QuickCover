import { ApiError } from "@/lib/api";

export function ErrorMessage({ error }: { error: unknown }) {
  if (!error) return null;

  const title = error instanceof Error ? error.message : "Something went wrong.";
  const details = error instanceof ApiError ? error.errors : [];

  return (
    <div role="alert" className="rounded-lg border border-danger/40 bg-danger/5 px-4 py-3 text-sm text-danger">
      <p className="font-medium">{title}</p>
      {details.length > 0 && (
        <ul className="mt-1 list-disc pl-5">
          {details.map((d) => (
            <li key={d}>{d}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
