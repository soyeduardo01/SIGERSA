export interface OptionalLocation {
  latitude: number
  longitude: number
  accuracyMeters: number
}

export function getOptionalLocation(timeoutMs = 4_000): Promise<OptionalLocation | undefined> {
  if (!('geolocation' in navigator)) return Promise.resolve(undefined)

  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      ({ coords }) =>
        resolve({
          latitude: coords.latitude,
          longitude: coords.longitude,
          accuracyMeters: coords.accuracy,
        }),
      () => resolve(undefined),
      { enableHighAccuracy: true, maximumAge: 60_000, timeout: timeoutMs },
    )
  })
}
