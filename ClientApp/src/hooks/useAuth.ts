import { useContext } from 'react'
import { AuthContext } from '../components/AuthContext'

export function useAuth() {
  return useContext(AuthContext)
}
