import { useEffect, useRef } from 'react'

export function useAutoRefresh(callback: () => Promise<void> | void, intervalMs = 15000, enabled = true) {
  const callbackRef = useRef(callback)
  const runningRef = useRef(false)

  useEffect(() => {
    callbackRef.current = callback
  }, [callback])

  useEffect(() => {
    if (!enabled) return

    const tick = async () => {
      if (document.visibilityState !== 'visible' || runningRef.current) return
      runningRef.current = true
      try {
        await callbackRef.current()
      } finally {
        runningRef.current = false
      }
    }

    const intervalId = window.setInterval(() => {
      void tick()
    }, intervalMs)

    return () => window.clearInterval(intervalId)
  }, [enabled, intervalMs])
}
