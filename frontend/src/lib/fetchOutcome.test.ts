import { describe, expect, it } from 'vitest'
import { htmlResponse, jsonResponse } from '../test/fetchDouble'
import {
  envelope,
  fixInq1,
  fixInq2,
  problem400Validation,
  problem404,
  problem500,
} from '../test/fixtures'
import { classifyFailure, classifyResponse, messageFor } from './fetchOutcome'

describe('UT-UI-005 fetch-outcome classifier', () => {
  it('(a) classifies a 200 envelope as data', async () => {
    const body = envelope([fixInq1(), fixInq2()])
    await expect(classifyResponse(jsonResponse(200, body))).resolves.toEqual({
      kind: 'data',
      envelope: body,
    })
  })

  it('(b) classifies a 400 ValidationProblemDetails by field keys only', async () => {
    await expect(classifyResponse(jsonResponse(400, problem400Validation()))).resolves.toEqual({
      kind: 'validation',
      fields: ['status'],
    })
  })

  it('(c) classifies a 404 ProblemDetails as gone', async () => {
    await expect(classifyResponse(jsonResponse(404, problem404()))).resolves.toEqual({
      kind: 'gone',
    })
  })

  it('(d) classifies a 500 ProblemDetails as a titled problem', async () => {
    await expect(classifyResponse(jsonResponse(500, problem500()))).resolves.toEqual({
      kind: 'problem',
      title: 'An unexpected error occurred.',
    })
  })

  it('(e) classifies a 502 text/html body as generic without exposing the body', async () => {
    await expect(
      classifyResponse(htmlResponse(502, '<html><body>boom</body></html>')),
    ).resolves.toEqual({ kind: 'generic' })
  })

  it('(f) classifies a network rejection as generic', () => {
    expect(classifyFailure(new TypeError('Failed to fetch'))).toEqual({ kind: 'generic' })
  })

  it('maps every failure kind to a fixed message variant', async () => {
    const validation = await classifyResponse(jsonResponse(400, problem400Validation()))
    expect(messageFor(validation)).toMatch(/status/i)
    const gone = await classifyResponse(jsonResponse(404, problem404()))
    expect(messageFor(gone)).toMatch(/no longer exists|not found|gone/i)
    const problem = await classifyResponse(jsonResponse(500, problem500()))
    expect(messageFor(problem)).toMatch(/unexpected error/i)
    const generic = classifyFailure(new TypeError('Failed to fetch'))
    expect(messageFor(generic)).toMatch(/could not|went wrong/i)
  })
})
