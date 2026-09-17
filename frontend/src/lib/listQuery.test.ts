import { describe, expect, it } from 'vitest'
import { serializeListQuery } from './listQuery'

function paramsOf(query: string): URLSearchParams {
  const marker = query.indexOf('?')
  return new URLSearchParams(marker === -1 ? '' : query.slice(marker + 1))
}

describe('UT-UI-001 list query serializer omits defaults', () => {
  it('serializes the empty list state to the bare endpoint', () => {
    expect(serializeListQuery({})).toBe('/api/inquiries')
  })

  it('emits no status, page, pageSize, or sort parameter for the omitted state', () => {
    const params = paramsOf(serializeListQuery({}))
    expect(params.has('status')).toBe(false)
    expect(params.has('page')).toBe(false)
    expect(params.has('pageSize')).toBe(false)
    expect(params.has('sort')).toBe(false)
  })

  it('omits the status parameter for the All filter', () => {
    expect(serializeListQuery({ status: 'All' })).toBe('/api/inquiries')
  })

  it('never serializes an empty-string filter as status=', () => {
    const query = serializeListQuery({ status: '' })
    const params = paramsOf(query)
    expect(params.has('status')).toBe(false)
    expect(query).not.toContain('status=')
  })
})

describe('UT-UI-002 list query serializer emits canonical parameters', () => {
  it('serializes an explicit state to exactly the four canonical parameters', () => {
    const query = serializeListQuery({
      status: 'Contacted',
      page: 3,
      pageSize: 50,
      sort: 'createdDateAsc',
    })
    const params = paramsOf(query)
    expect(params.get('status')).toBe('Contacted')
    expect(params.get('page')).toBe('3')
    expect(params.get('pageSize')).toBe('50')
    expect(params.get('sort')).toBe('createdDateAsc')
    expect([...params.keys()].sort()).toEqual(['page', 'pageSize', 'sort', 'status'])
  })

  it('emits both exact sort spellings when explicitly chosen', () => {
    expect(serializeListQuery({ sort: 'createdDateAsc' })).toContain('sort=createdDateAsc')
    expect(serializeListQuery({ sort: 'createdDateDesc' })).toContain('sort=createdDateDesc')
  })
})
