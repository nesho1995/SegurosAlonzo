import type { LucideIcon } from 'lucide-react'
import type { MetricTone } from '../types/common'
export function Metric({ title, value, hint, tone, icon: Icon }: { title: string; value: string | number; hint: string; tone: MetricTone; icon: LucideIcon }) { return (<article className={`metric ${tone}`}><Icon size={20} /><span>{title}</span><strong>{value}</strong><small>{hint}</small></article>) }
