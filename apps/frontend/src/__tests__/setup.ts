import '@testing-library/jest-dom/vitest'

class ResizeObserverMock {
  observe() {}
  disconnect() {}
  unobserve() {}
}

globalThis.ResizeObserver = ResizeObserverMock as typeof ResizeObserver
globalThis.requestAnimationFrame = ((callback: FrameRequestCallback) => {
  return window.setTimeout(() => callback(performance.now()), 0)
}) as typeof requestAnimationFrame
globalThis.cancelAnimationFrame = ((handle: number) => {
  window.clearTimeout(handle)
}) as typeof cancelAnimationFrame
