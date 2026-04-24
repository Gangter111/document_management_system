import { User, Document, MasterData } from '../types';

// Extend window interface for TypeScript
declare global {
  interface Window {
    electronAPI: {
      login: (u: string, p: string) => Promise<{ token: string; user: User }>;
      initAdmin: (u: string, p: string) => Promise<void>;
      getDocuments: (params?: any) => Promise<Document[]>;
      createDocument: (data: any, filePaths: string[]) => Promise<{ id: number }>;
      getDocumentTypes: () => Promise<MasterData[]>;
      getDepartments: () => Promise<MasterData[]>;
      getUsers: () => Promise<User[]>;
      getUserCount: () => Promise<number>;
      extractMetadata: (filePath: string) => Promise<any>;
      backup: () => Promise<{ success: boolean; path?: string }>;
    };
  }
}

// Helper to safely call electron API with web fallback
const callElectron = async (fnName: string, ...args: any[]) => {
  if (window.electronAPI && (window.electronAPI as any)[fnName]) {
    return (window.electronAPI as any)[fnName](...args);
  }
  
  console.warn(`[WEB PREVIEW] Electron API "${fnName}" called. Returning mock data.`);
  
  // Mock implementations for web preview
  switch (fnName) {
    case 'getUserCount': return 0;
    case 'login': return { token: 'mock-token', user: { id: 1, username: args[0], role: 'admin' } };
    case 'initAdmin': return { success: true };
    case 'getDocuments': return [];
    case 'getDocumentTypes': return [{ id: 1, name: 'Công văn' }];
    case 'getDepartments': return [{ id: 1, name: 'Phòng Hành chính' }];
    case 'extractMetadata': return {
      doc_number: { value: '123/CV-ABC', confidence: 0.9, needsReview: false },
      doc_date: { value: '2023-10-23', confidence: 0.85, needsReview: false },
      summary: { value: 'Văn bản thử nghiệm hệ thống', confidence: 0.95, needsReview: false },
      signer: { value: 'Nguyễn Văn A', confidence: 0.8, needsReview: false },
      department: { value: 'Phòng Hành chính', confidence: 0.9, needsReview: false },
      overall_confidence: 0.88
    };
    default: return null;
  }
};

export const api = {
  async login(username: string, password: string) {
    return callElectron('login', username, password);
  },

  async initAdmin(username: string, password: string) {
    return callElectron('initAdmin', username, password);
  },

  async getDocuments(params: { type?: string; search?: string; status?: string } = {}) {
    return callElectron('getDocuments', params);
  },

  async createDocument(data: any, filePaths: string[]) {
    return callElectron('createDocument', data, filePaths);
  },

  async extractMetadata(filePath: string) {
    return callElectron('extractMetadata', filePath);
  },

  async getDocumentTypes() {
    return callElectron('getDocumentTypes');
  },

  async getDepartments() {
    return callElectron('getDepartments');
  },

  async getUsers() {
    return callElectron('getUsers');
  },

  async getUserCount() {
    return callElectron('getUserCount');
  },

  async backup() {
    return callElectron('backup');
  }
};
