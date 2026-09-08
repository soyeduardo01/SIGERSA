import { createClient, type SupabaseClient } from '@supabase/supabase-js'

const supabaseUrl = import.meta.env.VITE_SUPABASE_URL?.trim()
const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY?.trim()

export const isSupabaseConfigured = Boolean(supabaseUrl && supabaseAnonKey)
export const evidenceBucket = import.meta.env.VITE_SUPABASE_EVIDENCE_BUCKET?.trim() || 'evidencias'

let client: SupabaseClient | undefined

export function getSupabaseClient(): SupabaseClient {
  if (!supabaseUrl || !supabaseAnonKey) {
    throw new Error(
      'Supabase no está configurado. Define VITE_SUPABASE_URL y VITE_SUPABASE_ANON_KEY.',
    )
  }

  client ??= createClient(supabaseUrl, supabaseAnonKey, {
    auth: {
      autoRefreshToken: true,
      persistSession: true,
      detectSessionInUrl: true,
    },
  })

  return client
}
