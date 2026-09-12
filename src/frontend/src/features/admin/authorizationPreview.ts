export function renderAuthorizationPreview(
  preview: Window,
  objectUrl: string,
  mimeType: string,
  fileName: string,
) {
  const document = preview.document
  try {
    preview.history.replaceState(null, '', '/visor-carta-autorizacion')
  } catch {
    // Some browsers restrict History API changes on a newly opened blank document.
  }
  document.documentElement.lang = 'es'
  document.head.replaceChildren()
  document.body.replaceChildren()

  const charset = document.createElement('meta')
  charset.setAttribute('charset', 'utf-8')
  const viewport = document.createElement('meta')
  viewport.name = 'viewport'
  viewport.content = 'width=device-width, initial-scale=1'
  const styles = document.createElement('style')
  styles.textContent = `
    * { box-sizing: border-box; }
    body { margin: 0; min-height: 100vh; background: #eef6f2; color: #153f34; font-family: system-ui, sans-serif; }
    header { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 16px 24px; background: #fff; border-bottom: 1px solid #dcebe5; }
    h1 { margin: 0; font-size: 18px; }
    p { margin: 4px 0 0; color: #64748b; font-size: 14px; overflow-wrap: anywhere; }
    a { flex: none; border-radius: 10px; background: #125640; padding: 10px 16px; color: #fff; font-weight: 700; text-decoration: none; }
    main { display: grid; min-height: calc(100vh - 82px); place-items: center; padding: 16px; }
    iframe, img { width: 100%; height: calc(100vh - 114px); border: 0; border-radius: 12px; background: #fff; object-fit: contain; box-shadow: 0 12px 32px rgba(18, 86, 64, .12); }
  `
  document.head.append(charset, viewport, styles)
  document.title = `Carta de autorización · ${fileName}`

  const header = document.createElement('header')
  const headingGroup = document.createElement('div')
  const heading = document.createElement('h1')
  heading.textContent = 'Carta de autorización'
  const description = document.createElement('p')
  description.textContent = fileName
  headingGroup.append(heading, description)

  const download = document.createElement('a')
  download.href = objectUrl
  download.download = fileName
  download.textContent = 'Descargar archivo'
  header.append(headingGroup, download)

  const content = document.createElement('main')
  if (mimeType.startsWith('image/')) {
    const image = document.createElement('img')
    image.src = objectUrl
    image.alt = `Vista previa de ${fileName}`
    content.appendChild(image)
  } else {
    const frame = document.createElement('iframe')
    frame.src = objectUrl
    frame.title = `Vista previa de ${fileName}`
    content.appendChild(frame)
  }

  document.body.append(header, content)
}
