// ══════════════════════════════════════════════════════════
// Generic HTTP client
// ══════════════════════════════════════════════════════════

export class HttpError extends Error {
  constructor(
    public readonly status: number,
    public readonly statusText: string,
    public readonly body: string,
  ) {
    super(`HTTP ${status} ${statusText}`)
    this.name = 'HttpError'
  }
}

/**
 * Thin wrapper around fetch that:
 * - Throws HttpError on non-2xx responses
 * - Parses JSON automatically
 * - Allows a base URL prefix
 */
export async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, options)
  if (!res.ok) {
    const body = await res.text().catch(() => '')
    throw new HttpError(res.status, res.statusText, body)
  }
  return res.json() as Promise<T>
}

/** POST JSON helper */
export async function postJson<T>(url: string, payload: unknown): Promise<T> {
  return fetchJson<T>(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  })
}
