import { getJson, postJson, sendJson } from './http'
import type { ExtractorConfig, ExtractorResult, ExtractorTestRequest } from '../types/extractor'
export function getExtractorConfig() { return getJson<ExtractorConfig>('/api/extractor-avanzado/configuracion') }
export function saveExtractorConfig(config: ExtractorConfig) { return sendJson('/api/extractor-avanzado/configuracion', 'PUT', config) }
export function testExtractor(request: ExtractorTestRequest) { return postJson<ExtractorResult>('/api/extractor-avanzado/probar', request) }
