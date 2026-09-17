// Controllable fetch boundary for component tests (catalog §2.1): a FIFO
// queue of programmed reactions, every issued request recorded. Responses are
// real `Response` objects; deferred reactions let tests resolve in a
// deterministic order for stale-response coverage.

export interface RecordedRequest {
  url: string
  method: string
  headers: Record<string, string>
  body: string | null
}

export interface DeferredHandle {
  readonly request: RecordedRequest
  resolve(response: Response): void
  reject(error: Error): void
}

type Reaction =
  | { kind: 'respond'; make: (request: RecordedRequest) => Response }
  | { kind: 'defer'; handle: DeferredHandle }
  | { kind: 'reject'; error: Error }

export interface FetchDouble {
  /** Every request the component issued, oldest first. */
  readonly calls: readonly RecordedRequest[]
  /** Queue a ready-made response for the next request. */
  queueResponse(response: Response): void
  /** Queue a rejection (e.g. network TypeError) for the next request. */
  queueRejection(error: Error): void
  /**
   * Queue a manually-resolved response for the next request; the handle
   * completes once the request arrives.
   */
  queueDeferred(): DeferredHandle
  /** True when every programmed reaction has been consumed. */
  readonly idle: boolean
  uninstall(): void
}

export function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

export function htmlResponse(status: number, html: string): Response {
  return new Response(html, {
    status,
    headers: { 'Content-Type': 'text/html' },
  })
}

export function emptyResponse(status: number): Response {
  return new Response(null, { status })
}

export function installFetchDouble(): FetchDouble {
  const calls: RecordedRequest[] = []
  const queue: Reaction[] = []
  const originalWindowFetch = window.fetch
  const originalGlobalFetch = globalThis.fetch

  const takeReaction = (): Reaction =>
    queue.shift() ?? {
      kind: 'reject' as const,
      error: new Error(`UNEXPECTED FETCH — no reaction queued for this request`),
    }

  // jsdom provides no fetch; bare `fetch` in component code resolves through
  // globalThis, so both references must point at the double.
  const fetchImpl = ((input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
    const method = (init?.method ?? (input instanceof Request ? input.method : 'GET')).toUpperCase()
    const headers: Record<string, string> = {}
    const headerRecord =
      init?.headers ?? (input instanceof Request ? input.headers : undefined)
    if (headerRecord) {
      for (const [name, value] of new Headers(headerRecord)) headers[name] = value
    }
    const body = typeof init?.body === 'string' ? init.body : null
    const request: RecordedRequest = { url, method, headers, body }
    calls.push(request)

    const reaction = takeReaction()
    if (reaction.kind === 'respond') {
      return Promise.resolve(reaction.make(request))
    }
    if (reaction.kind === 'reject') {
      return Promise.reject(reaction.error)
    }
    return new Promise<Response>((resolve, reject) => {
      reaction.handle.resolve = (response) => resolve(response)
      reaction.handle.reject = (error) => reject(error)
      ;(reaction.handle as { request: RecordedRequest }).request = request
    })
  }) as typeof fetch

  window.fetch = fetchImpl
  globalThis.fetch = fetchImpl

  const stub = {
    get calls() {
      return calls
    },
    get idle() {
      return queue.length === 0
    },
    queueResponse(response: Response) {
      queue.push({ kind: 'respond', make: () => response })
    },
    queueRejection(error: Error) {
      queue.push({ kind: 'reject', error })
    },
    queueDeferred(): DeferredHandle {
      const handle = {
        request: null as unknown as RecordedRequest,
        resolve(_response: Response) {
          throw new Error('Deferred resolved before the request arrived.')
        },
        reject(_error: Error) {
          throw new Error('Deferred rejected before the request arrived.')
        },
      }
      queue.push({ kind: 'defer', handle })
      return handle
    },
    uninstall() {
      window.fetch = originalWindowFetch
      globalThis.fetch = originalGlobalFetch
    },
  } satisfies FetchDouble

  return stub
}
