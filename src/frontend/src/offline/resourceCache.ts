import { offlineDb } from './database'

function resourceKey(ownerUserId: string, path: string) {
  return `${ownerUserId}:${path}`
}

export async function cacheResourceSnapshot(ownerUserId: string, path: string, snapshot: unknown) {
  if (!ownerUserId) return
  await offlineDb.resourceSnapshots.put({
    key: resourceKey(ownerUserId, path),
    ownerUserId,
    path,
    snapshot,
    updatedAt: new Date().toISOString(),
  })
}

export async function readResourceSnapshot<T>(ownerUserId: string, path: string) {
  if (!ownerUserId) return undefined
  const cached = await offlineDb.resourceSnapshots.get(resourceKey(ownerUserId, path))
  return cached?.snapshot as T | undefined
}
