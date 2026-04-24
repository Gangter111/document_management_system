import Database from 'better-sqlite3';
import path from 'path';
import fs from 'fs';

const dataDir = path.join(process.cwd(), 'data');
const dbDir = path.join(dataDir, 'db');
const filesDir = path.join(dataDir, 'files');
const backupDir = path.join(dataDir, 'backup');
const logsDir = path.join(dataDir, 'logs');

const subFilesDirs = ['incoming', 'outgoing', 'internal'];

// Ensure directory structure exists
[dataDir, dbDir, filesDir, backupDir, logsDir].forEach(dir => {
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
});

subFilesDirs.forEach(sub => {
  const subPath = path.join(filesDir, sub);
  if (!fs.existsSync(subPath)) fs.mkdirSync(subPath, { recursive: true });
});

const dbPath = path.join(dbDir, 'app.db');
const db = new Database(dbPath);

// Initialize database schema
export function initDb() {
  db.exec(`
    CREATE TABLE IF NOT EXISTS users (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      username TEXT UNIQUE NOT NULL,
      password_hash TEXT NOT NULL,
      role TEXT DEFAULT 'user',
      last_login DATETIME,
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP
    );

    CREATE TABLE IF NOT EXISTS departments (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      name TEXT UNIQUE NOT NULL,
      description TEXT
    );

    CREATE TABLE IF NOT EXISTS document_types (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      name TEXT UNIQUE NOT NULL,
      description TEXT
    );

    CREATE TABLE IF NOT EXISTS documents (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      type TEXT NOT NULL, -- 'incoming', 'outgoing', 'internal'
      doc_number TEXT,
      symbol TEXT,
      doc_date DATE,
      received_date DATE,
      sender TEXT,
      receiver TEXT,
      doc_type_id INTEGER,
      summary TEXT,
      security_level TEXT,
      urgency TEXT,
      handler_id INTEGER,
      deadline DATE,
      status TEXT DEFAULT 'pending',
      department_id INTEGER,
      notes TEXT,
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      deleted_at DATETIME,
      FOREIGN KEY (doc_type_id) REFERENCES document_types(id),
      FOREIGN KEY (department_id) REFERENCES departments(id),
      FOREIGN KEY (handler_id) REFERENCES users(id)
    );

    CREATE TABLE IF NOT EXISTS document_files (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      document_id INTEGER NOT NULL,
      file_path TEXT NOT NULL,
      file_name TEXT NOT NULL,
      file_type TEXT,
      file_size INTEGER,
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      FOREIGN KEY (document_id) REFERENCES documents(id)
    );

    CREATE TABLE IF NOT EXISTS audit_logs (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      user_id INTEGER,
      action TEXT NOT NULL,
      details TEXT,
      ip_address TEXT,
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      FOREIGN KEY (user_id) REFERENCES users(id)
    );

    CREATE TABLE IF NOT EXISTS extraction_logs (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      file_name TEXT NOT NULL,
      extracted_data TEXT,
      confidence REAL,
      status TEXT,
      user_id INTEGER,
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      FOREIGN KEY (user_id) REFERENCES users(id)
    );

    CREATE TABLE IF NOT EXISTS settings (
      key TEXT PRIMARY KEY,
      value TEXT
    );
  `);

  // Seed initial data if needed
  const userCount = db.prepare('SELECT COUNT(*) as count FROM users').get() as { count: number };
  if (userCount.count === 0) {
    console.log('[DEBUG] No users found. Seeding default admin...');
    // Default admin: admin / admin123
    const bcrypt = require('bcryptjs');
    const hashedPassword = bcrypt.hashSync('admin123', 10);
    db.prepare('INSERT INTO users (username, password_hash, role) VALUES (?, ?, ?)').run('admin', hashedPassword, 'admin');
    console.log('[DEBUG] Default admin seeded: admin / admin123');
  } else {
    console.log(`[DEBUG] Database already has ${userCount.count} users. Skipping seed.`);
  }

  // Seed document types
  const typeCount = db.prepare('SELECT COUNT(*) as count FROM document_types').get() as { count: number };
  if (typeCount.count === 0) {
    const types = ['Công văn', 'Quyết định', 'Thông báo', 'Tờ trình', 'Hợp đồng', 'Khác'];
    const insert = db.prepare('INSERT INTO document_types (name) VALUES (?)');
    types.forEach(t => insert.run(t));
  }

  // Seed departments
  const deptCount = db.prepare('SELECT COUNT(*) as count FROM departments').get() as { count: number };
  if (deptCount.count === 0) {
    const depts = ['Ban Giám đốc', 'Phòng Hành chính', 'Phòng Kế toán', 'Phòng Kỹ thuật', 'Phòng Kinh doanh'];
    const insert = db.prepare('INSERT INTO departments (name) VALUES (?)');
    depts.forEach(d => insert.run(d));
  }
}

export default db;
