export type DocumentType = 'incoming' | 'outgoing' | 'internal';

export interface User {
  id: number;
  username: string;
  role: 'admin' | 'user';
  last_login?: string;
}

export interface Document {
  id: number;
  type: DocumentType;
  doc_number: string;
  symbol: string;
  doc_date: string;
  received_date?: string;
  sender?: string;
  receiver?: string;
  doc_type_id: number;
  type_name?: string;
  summary: string;
  security_level: string;
  urgency: string;
  handler_id?: number;
  deadline?: string;
  status: string;
  department_id: number;
  department_name?: string;
  notes?: string;
  created_at: string;
  updated_at: string;
}

export interface MasterData {
  id: number;
  name: string;
  description?: string;
}

export interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
}
