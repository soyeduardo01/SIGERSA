import { getSession, getSessionIdentity, type ParameterControl } from '../lib/api'
import { offlineDb } from './database'

function ownerUserId() {
  const session = getSession()
  return session ? (getSessionIdentity(session).id ?? '') : ''
}

function cacheKey(owner: string, parametersId: number) {
  return `${owner}:${parametersId}`
}

function sortParameters(values: ParameterControl[]) {
  return [...values].sort(
    (left, right) =>
      (left.companyCode ?? Number.MIN_SAFE_INTEGER) -
        (right.companyCode ?? Number.MIN_SAFE_INTEGER) ||
      (left.numericData ?? Number.MAX_SAFE_INTEGER) -
        (right.numericData ?? Number.MAX_SAFE_INTEGER) ||
      (left.oCode ?? Number.MAX_SAFE_INTEGER) - (right.oCode ?? Number.MAX_SAFE_INTEGER) ||
      left.parametersId - right.parametersId,
  )
}

export async function replaceCachedParameters(values: ParameterControl[]) {
  const owner = ownerUserId()
  if (!owner) return
  const updatedAt = new Date().toISOString()
  await offlineDb.transaction('rw', offlineDb.parameterControls, async () => {
    await offlineDb.parameterControls.where('ownerUserId').equals(owner).delete()
    await offlineDb.parameterControls.bulkPut(
      values.map((value) => ({
        key: cacheKey(owner, value.parametersId),
        ownerUserId: owner,
        parametersId: value.parametersId,
        keyWord: value.keyWord,
        companyCode: value.companyCode,
        snapshot: value,
        updatedAt,
      })),
    )
  })
}

export async function replaceCachedParameterCatalog(
  keyWord: string,
  companyCode: number | undefined,
  values: ParameterControl[],
) {
  const owner = ownerUserId()
  if (!owner) return
  const updatedAt = new Date().toISOString()
  await offlineDb.transaction('rw', offlineDb.parameterControls, async () => {
    const existing = await offlineDb.parameterControls
      .where('[ownerUserId+keyWord]')
      .equals([owner, keyWord])
      .toArray()
    const matchingKeys = existing
      .filter(
        (value) =>
          value.companyCode === null ||
          (companyCode !== undefined && value.companyCode === companyCode),
      )
      .map((value) => value.key)
    await offlineDb.parameterControls.bulkDelete(matchingKeys)
    await offlineDb.parameterControls.bulkPut(
      values.map((value) => ({
        key: cacheKey(owner, value.parametersId),
        ownerUserId: owner,
        parametersId: value.parametersId,
        keyWord: value.keyWord,
        companyCode: value.companyCode,
        snapshot: value,
        updatedAt,
      })),
    )
  })
}

export async function readCachedParameters(keyWord: string, companyCode?: number) {
  const owner = ownerUserId()
  if (!owner) return []
  const values = await offlineDb.parameterControls
    .where('[ownerUserId+keyWord]')
    .equals([owner, keyWord])
    .toArray()
  return sortParameters(
    values
      .map((value) => value.snapshot)
      .filter(
        (value) =>
          value.status &&
          (value.companyCode === null ||
            (companyCode !== undefined && value.companyCode === companyCode)),
      ),
  )
}

export async function readAllCachedParameters(search = '') {
  const owner = ownerUserId()
  if (!owner) return []
  const normalizedSearch = search.trim().toLocaleLowerCase('es')
  const values = await offlineDb.parameterControls.where('ownerUserId').equals(owner).toArray()
  return values
    .map((value) => value.snapshot)
    .filter(
      (value) =>
        !normalizedSearch ||
        [value.keyWord, value.cCode, value.stringData].some((candidate) =>
          candidate?.toLocaleLowerCase('es').includes(normalizedSearch),
        ),
    )
    .sort(
      (left, right) =>
        left.keyWord.localeCompare(right.keyWord, 'es') ||
        (left.numericData ?? Number.MAX_SAFE_INTEGER) -
          (right.numericData ?? Number.MAX_SAFE_INTEGER) ||
        left.parametersId - right.parametersId,
    )
}
