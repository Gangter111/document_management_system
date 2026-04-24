import { app, BrowserWindow, ipcMain, dialog } from 'electron';
import path from 'path';
import fs from 'fs';
import isDev from 'electron-is-dev';
import bcrypt from 'bcryptjs';
import jwt from 'jsonwebtoken';
import db, { initDb } from './src/lib/db.ts';
import { extractMetadata } from './src/lib/extraction.ts';

const JWT_SECRET = 'your-secret-key-offline'; // In desktop app, this is less critical but kept for logic consistency

let mainWindow;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 800,
    title: "Hệ thống Quản lý Văn bản Nội bộ (Offline)",
    webPreferences: {
      preload: path.join(process.cwd(), 'dist-electron', 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
    },
  });

  const startUrl = isDev 
    ? 'http://localhost:3000' 
    : `file://${path.join(process.cwd(), 'dist/index.html')}`;

  mainWindow.loadURL(startUrl);

  if (isDev) {
    mainWindow.webContents.openDevTools();
  }
}

// --- IPC Handlers (Replacing Express Routes) ---

ipcMain.handle('auth:login', async (event, { username, password }) => {
  console.log(`[DEBUG] Login attempt for username: ${username}`);
  const user = db.prepare('SELECT * FROM users WHERE username = ?').get(username) as any;
  
  if (!user) {
    console.log(`[DEBUG] User not found: ${username}`);
    throw new Error('User not found');
  }

  console.log(`[DEBUG] User found. Comparing passwords...`);
  const validPassword = bcrypt.compareSync(password, user.password_hash);
  
  if (!validPassword) {
    console.log(`[DEBUG] Invalid password for user: ${username}`);
    throw new Error('Invalid password');
  }

  console.log(`[DEBUG] Login successful for: ${username}`);
  const token = jwt.sign({ id: user.id, username: user.username, role: user.role }, JWT_SECRET, { expiresIn: '8h' });
  db.prepare('UPDATE users SET last_login = CURRENT_TIMESTAMP WHERE id = ?').run(user.id);
  
  return { token, user: { id: user.id, username: user.username, role: user.role } };
});

ipcMain.handle('auth:init-admin', async (event, { username, password }) => {
  console.log(`[DEBUG] Init Admin attempt for username: ${username}`);
  const userCount = db.prepare('SELECT COUNT(*) as count FROM users').get() as { count: number };
  
  if (userCount.count > 0) {
    console.log(`[DEBUG] Admin initialization blocked: ${userCount.count} users already exist.`);
    throw new Error('Admin already exists');
  }

  const hashedPassword = bcrypt.hashSync(password, 10);
  db.prepare('INSERT INTO users (username, password_hash, role) VALUES (?, ?, ?)').run(username, hashedPassword, 'admin');
  console.log(`[DEBUG] Admin created successfully: ${username}`);
  return { success: true };
});

ipcMain.handle('docs:get-all', async (event, { type, search, status } = {}) => {
  let query = 'SELECT d.*, dt.name as type_name, dept.name as department_name FROM documents d LEFT JOIN document_types dt ON d.doc_type_id = dt.id LEFT JOIN departments dept ON d.department_id = dept.id WHERE d.deleted_at IS NULL';
  const params: any[] = [];

  if (type) {
    query += ' AND d.type = ?';
    params.push(type);
  }
  if (status) {
    query += ' AND d.status = ?';
    params.push(status);
  }
  if (search) {
    query += ' AND (d.doc_number LIKE ? OR d.summary LIKE ? OR d.sender LIKE ? OR d.receiver LIKE ?)';
    const searchParam = `%${search}%`;
    params.push(searchParam, searchParam, searchParam, searchParam);
  }

  query += ' ORDER BY d.created_at DESC';
  return db.prepare(query).all(...params);
});

ipcMain.handle('docs:create', async (event, { data, filePaths }) => {
  const info = db.prepare(`
    INSERT INTO documents (
      type, doc_number, symbol, doc_date, received_date, sender, receiver, 
      doc_type_id, summary, security_level, urgency, handler_id, deadline, 
      status, department_id, notes
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
  `).run(
    data.type, data.doc_number, data.symbol, data.doc_date, 
    data.received_date, data.sender, data.receiver, 
    data.doc_type_id, data.summary, data.security_level, 
    data.urgency, data.handler_id, data.deadline, 
    data.status, data.department_id, data.notes
  );

  const docId = info.lastInsertRowid;

  if (filePaths && filePaths.length > 0) {
    const docType = data.type; // 'incoming', 'outgoing', 'internal'
    const targetDir = path.join(process.cwd(), 'data', 'files', docType);
    const insertFile = db.prepare('INSERT INTO document_files (document_id, file_path, file_name, file_type, file_size) VALUES (?, ?, ?, ?, ?)');
    
    for (const filePath of filePaths) {
      const fileName = path.basename(filePath);
      const destPath = path.join(targetDir, `${Date.now()}-${fileName}`);
      fs.copyFileSync(filePath, destPath);
      const stats = fs.statSync(destPath);
      insertFile.run(docId, destPath, fileName, path.extname(fileName), stats.size);
    }
  }

  return { id: docId };
});

ipcMain.handle('master:get-types', async () => db.prepare('SELECT * FROM document_types').all());
ipcMain.handle('master:get-departments', async () => db.prepare('SELECT * FROM departments').all());
ipcMain.handle('master:get-users', async () => db.prepare('SELECT id, username, role FROM users').all());
ipcMain.handle('auth:get-user-count', async () => {
  const res = db.prepare('SELECT COUNT(*) as count FROM users').get() as { count: number };
  return res.count;
});

ipcMain.handle('extract:metadata', async (event, filePath) => {
  return await extractMetadata(filePath);
});

ipcMain.handle('sys:backup', async () => {
  const { filePath } = await dialog.showSaveDialog({
    title: 'Chọn nơi lưu bản sao lưu',
    defaultPath: `backup-${Date.now()}.db`,
    filters: [{ name: 'SQLite Database', extensions: ['db'] }]
  });

  if (filePath) {
    const dbPath = path.join(process.cwd(), 'data', 'db', 'app.db');
    fs.copyFileSync(dbPath, filePath);
    
    // Also copy to internal backup folder
    const internalBackupPath = path.join(process.cwd(), 'data', 'backup', `backup-${Date.now()}.db`);
    fs.copyFileSync(dbPath, internalBackupPath);

    return { success: true, path: filePath };
  }
  return { success: false };
});

// --- Lifecycle ---

app.on('ready', () => {
  console.log(`[DEBUG] App Path: ${app.getAppPath()}`);
  console.log(`[DEBUG] Executable Path: ${process.execPath}`);
  console.log(`[DEBUG] Current Working Directory: ${process.cwd()}`);
  
  initDb();
  
  const dbPath = path.join(process.cwd(), 'data', 'db', 'app.db');
  console.log(`[DEBUG] Database Path: ${dbPath}`);
  
  const userCount = db.prepare('SELECT COUNT(*) as count FROM users').get() as { count: number };
  console.log(`[DEBUG] Total users in DB: ${userCount.count}`);
  
  const adminUser = db.prepare('SELECT username FROM users WHERE username = ?').get('admin');
  console.log(`[DEBUG] User 'admin' exists: ${!!adminUser}`);
  
  createWindow();
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
