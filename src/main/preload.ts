import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('electronAPI', {
  // Auth
  login: (username, password) => ipcRenderer.invoke('auth:login', { username, password }),
  initAdmin: (username, password) => ipcRenderer.invoke('auth:init-admin', { username, password }),
  
  // Documents
  getDocuments: (params) => ipcRenderer.invoke('docs:get-all', params),
  createDocument: (data, filePaths) => ipcRenderer.invoke('docs:create', { data, filePaths }),
  
  // Master Data
  getDocumentTypes: () => ipcRenderer.invoke('master:get-types'),
  getDepartments: () => ipcRenderer.invoke('master:get-departments'),
  getUsers: () => ipcRenderer.invoke('master:get-users'),
  getUserCount: () => ipcRenderer.invoke('auth:get-user-count'),
  
  // Extraction
  extractMetadata: (filePath) => ipcRenderer.invoke('extract:metadata', filePath),
  
  // System
  backup: () => ipcRenderer.invoke('sys:backup'),
});
