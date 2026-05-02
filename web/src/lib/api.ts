const baseUrl =
  process.env.NEXT_PUBLIC_API_URL ?? "https://localhost:7272";

export function getApiBaseUrl(): string {
  return baseUrl.replace(/\/$/, "");
}

export async function apiJson<T>(
  path: string,
  init: RequestInit & { accessToken?: string }
): Promise<T> {
  const url = `${getApiBaseUrl()}${path.startsWith("/") ? "" : "/"}${path}`;
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && init.body)
    headers.set("Content-Type", "application/json");
  if (init.accessToken)
    headers.set("Authorization", `Bearer ${init.accessToken}`);

  const res = await fetch(url, { ...init, headers });
  if (!res.ok) {
    const text = await res.text();
    let message = text;
    try {
      const j = JSON.parse(text) as { error?: string; detail?: string };
      if (typeof j.error === "string") message = j.error;
      else if (typeof j.detail === "string") message = j.detail;
    } catch {
      /* response body is not JSON */
    }
    throw new Error(message || `Request failed: ${res.status}`);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}
