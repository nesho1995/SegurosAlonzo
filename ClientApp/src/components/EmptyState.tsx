export function EmptyState({ text = 'No hay datos para mostrar.' }: { text?: string }) { return <div className="empty">{text}</div> }
