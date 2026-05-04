import type { StatusTone } from '../types/common'
export function StatusPill({ text, tone }: { text: string; tone: StatusTone }) { return <span className={`pill ${tone}`}>{text}</span> }
