import { gsap } from 'gsap'
import './presentation.css'

const slides = Array.from(document.querySelectorAll<HTMLElement>('.slide'))
const nextButtons = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-next]'))
const restartButton = document.querySelector<HTMLButtonElement>('[data-restart]')
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
let current = 0
let animating = false

function updateControls() {
  history.replaceState(null, '', `#${current + 1}`)
}

function animateContents(slide: HTMLElement) {
  const items = slide.querySelectorAll<HTMLElement>('.reveal')
  if (reducedMotion) {
    gsap.set(items, { clearProps: 'all' })
    return
  }
  gsap.fromTo(
    items,
    { autoAlpha: 0, y: 28 },
    {
      autoAlpha: 1,
      y: 0,
      duration: 0.65,
      stagger: 0.075,
      ease: 'power3.out',
      clearProps: 'transform',
    },
  )
  const techCards = slide.querySelectorAll('.tech-card')
  if (techCards.length) {
    gsap.fromTo(
      techCards,
      { rotateX: 8, scale: 0.96 },
      { rotateX: 0, scale: 1, duration: 0.75, stagger: 0.09, ease: 'back.out(1.25)' },
    )
  }
  const nodes = slide.querySelectorAll('.architecture-node')
  if (nodes.length) {
    gsap.fromTo(
      nodes,
      { scale: 0.9, autoAlpha: 0 },
      { scale: 1, autoAlpha: 1, duration: 0.55, stagger: 0.08, ease: 'back.out(1.4)' },
    )
  }
}

function showSlide(target: number) {
  if (animating || target === current || target < 0 || target >= slides.length) return
  animating = true
  const outgoing = slides[current]
  const incoming = slides[target]
  const direction = target > current ? 1 : -1
  incoming.hidden = false
  incoming.classList.add('is-active')

  if (reducedMotion) {
    outgoing.hidden = true
    outgoing.classList.remove('is-active')
    current = target
    updateControls()
    animateContents(incoming)
    animating = false
    return
  }

  const timeline = gsap.timeline({
    onComplete: () => {
      outgoing.hidden = true
      outgoing.classList.remove('is-active')
      gsap.set(outgoing, { clearProps: 'all' })
      current = target
      updateControls()
      animateContents(incoming)
      animating = false
    },
  })
  timeline
    .to(outgoing, {
      autoAlpha: 0,
      xPercent: -5 * direction,
      scale: 0.985,
      duration: 0.34,
      ease: 'power2.in',
    })
    .fromTo(
      incoming,
      { autoAlpha: 0, xPercent: 6 * direction },
      { autoAlpha: 1, xPercent: 0, duration: 0.48, ease: 'power3.out' },
    )
}

nextButtons.forEach((button) => button.addEventListener('click', () => showSlide(current + 1)))
restartButton?.addEventListener('click', () => showSlide(0))

window.addEventListener('keydown', (event) => {
  if (event.key === 'ArrowRight' || event.key === 'PageDown' || event.key === ' ') {
    event.preventDefault()
    showSlide(current + 1)
  }
  if (event.key === 'ArrowLeft' || event.key === 'PageUp') {
    event.preventDefault()
    showSlide(current - 1)
  }
  if (event.key === 'Home') showSlide(0)
  if (event.key === 'End') showSlide(slides.length - 1)
})

let touchStartX = 0
window.addEventListener(
  'touchstart',
  (event) => {
    touchStartX = event.changedTouches[0].screenX
  },
  { passive: true },
)
window.addEventListener(
  'touchend',
  (event) => {
    const delta = event.changedTouches[0].screenX - touchStartX
    if (Math.abs(delta) > 55) showSlide(current + (delta < 0 ? 1 : -1))
  },
  { passive: true },
)

const initialHash = Number(window.location.hash.slice(1)) - 1
if (Number.isInteger(initialHash) && initialHash > 0 && initialHash < slides.length) {
  slides[0].hidden = true
  slides[0].classList.remove('is-active')
  current = initialHash
  slides[current].hidden = false
  slides[current].classList.add('is-active')
}
updateControls()
animateContents(slides[current])
