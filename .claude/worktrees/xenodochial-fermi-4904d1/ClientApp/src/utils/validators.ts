export const maxExcelBytes = 5 * 1024 * 1024
export function validateExcelFile(selected: File | null) { if (!selected) return 'Selecciona un archivo .xlsx.'; if (!selected.name.toLowerCase().endsWith('.xlsx')) return 'Solo se permite formato .xlsx.'; if (selected.size > maxExcelBytes) return 'El archivo supera el limite permitido de 5 MB.'; return null }
