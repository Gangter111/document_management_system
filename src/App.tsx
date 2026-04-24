import React, { useState, useEffect, createContext, useContext } from 'react';
import { 
  LayoutDashboard, 
  FileText, 
  FileInput, 
  FileOutput, 
  FileSearch, 
  Settings, 
  LogOut, 
  Plus, 
  Search,
  FileUp,
  ShieldCheck,
  History,
  Database
} from 'lucide-react';
import { Toaster, toast } from 'sonner';
import { api } from './lib/api';
import { User, Document, MasterData } from './types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Badge } from '@/components/ui/badge';
import { Sidebar, SidebarContent, SidebarHeader, SidebarMenu, SidebarMenuItem, SidebarMenuButton, SidebarProvider, SidebarTrigger } from '@/components/ui/sidebar';

// --- Contexts ---
const AuthContext = createContext<{
  user: User | null;
  login: (u: string, p: string) => Promise<void>;
  logout: () => void;
} | null>(null);

const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within AuthProvider');
  return context;
};

// --- Components ---

const LoginPage = ({ onInitAdmin, showInitAdmin }: { onInitAdmin: () => void, showInitAdmin: boolean }) => {
  const { login } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await login(username, password);
      toast.success('Đăng nhập thành công');
    } catch (err) {
      toast.error('Sai tài khoản hoặc mật khẩu');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex items-center justify-center min-h-screen">
      <Card className="w-[400px] glass border-white/10 shadow-2xl">
        <CardHeader>
          <CardTitle className="text-2xl text-center text-blue-400">Hệ thống Quản lý Văn bản</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-400">Tên đăng nhập</label>
              <Input className="bg-black/20 border-white/10 text-white" value={username} onChange={e => setUsername(e.target.value)} required />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-400">Mật khẩu</label>
              <Input className="bg-black/20 border-white/10 text-white" type="password" value={password} onChange={e => setPassword(e.target.value)} required />
            </div>
            <Button type="submit" className="w-full bg-blue-600 hover:bg-blue-500 text-white" disabled={loading}>
              {loading ? 'Đang xử lý...' : 'Đăng nhập'}
            </Button>
            
            {showInitAdmin && (
              <div className="pt-4 border-t border-white/10 mt-4 space-y-3">
                <p className="text-[10px] text-center text-amber-400/80 uppercase tracking-widest">Chưa có tài khoản quản trị</p>
                <Button type="button" variant="outline" className="w-full border-blue-500/30 bg-blue-500/10 text-blue-400 hover:bg-blue-500 hover:text-white" onClick={onInitAdmin}>
                  Khởi tạo Admin ngay
                </Button>
                <div className="bg-white/5 p-3 rounded-lg border border-white/5 text-[10px] text-slate-400 space-y-1">
                  <p>Tài khoản mặc định (nếu đã seed):</p>
                  <p>User: <span className="text-white font-mono">admin</span></p>
                  <p>Pass: <span className="text-white font-mono">admin123</span></p>
                </div>
              </div>
            )}
          </form>
        </CardContent>
      </Card>
    </div>
  );
};

const Dashboard = () => {
  return (
    <div className="space-y-6">
      <div className="flex justify-between items-end">
        <div>
          <h1 className="text-3xl font-bold text-white">Chào buổi sáng, Quản trị viên</h1>
          <p className="text-sm text-slate-400">Thứ Hai, 23 Tháng 10, 2023</p>
        </div>
        <div className="bg-white/10 px-4 py-2 rounded-full border border-white/10 flex items-center gap-3 text-sm">
          <div className="w-6 h-6 bg-indigo-500 rounded-full" />
          <span>Admin</span>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="glass border-white/10">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-medium text-slate-400 uppercase tracking-wider">Văn bản đến (Tháng)</CardTitle>
            <FileInput className="h-4 w-4 text-blue-400" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-white">1,248</div>
            <p className="text-xs text-emerald-400">+12% so với tháng trước</p>
          </CardContent>
        </Card>
        <Card className="glass border-white/10">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-medium text-slate-400 uppercase tracking-wider">Văn bản ban hành</CardTitle>
            <FileOutput className="h-4 w-4 text-emerald-400" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-white">512</div>
            <p className="text-xs text-emerald-400">+5% so với tháng trước</p>
          </CardContent>
        </Card>
        <Card className="glass border-white/10">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-medium text-slate-400 uppercase tracking-wider">Chờ OCR xử lý</CardTitle>
            <FileText className="h-4 w-4 text-amber-400" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-amber-400">08</div>
            <p className="text-xs text-slate-400">Cần xử lý ngay</p>
          </CardContent>
        </Card>
        <Card className="glass border-white/10">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-medium text-slate-400 uppercase tracking-wider">Dung lượng DB</CardTitle>
            <Database className="h-4 w-4 text-slate-400" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-white">1.2 GB</div>
            <p className="text-xs text-slate-400">78% trống</p>
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <DocumentList type="incoming" title="Văn bản gần đây" hideHeader />
        </div>
        <div className="glass-panel rounded-2xl p-6 space-y-6">
          <h3 className="text-lg font-semibold text-white">Xác nhận Trích xuất (AI)</h3>
          <div className="bg-black/30 h-32 rounded-xl border border-dashed border-white/10 flex items-center justify-center">
            <span className="text-xs text-slate-500">PREVIEW: CV_45_STC.pdf</span>
          </div>
          <div className="space-y-4">
            <div className="space-y-1">
              <div className="flex justify-between text-xs">
                <span className="text-slate-400">Số văn bản</span>
                <span className="text-emerald-400 font-bold">98%</span>
              </div>
              <Input className="bg-black/20 border-white/10 text-sm h-9" defaultValue="45/CV-STC" />
            </div>
            <div className="space-y-1">
              <div className="flex justify-between text-xs">
                <span className="text-slate-400">Ngày ban hành</span>
                <span className="text-emerald-400 font-bold">95%</span>
              </div>
              <Input className="bg-black/20 border-white/10 text-sm h-9" defaultValue="20/10/2023" />
            </div>
            <div className="space-y-1">
              <div className="flex justify-between text-xs">
                <span className="text-slate-400">Trích yếu</span>
                <span className="text-emerald-400 font-bold">92%</span>
              </div>
              <textarea className="w-full bg-black/20 border border-white/10 rounded-md p-2 text-sm h-20 resize-none outline-none" defaultValue="Về việc quyết toán ngân sách quý III năm 2023..." />
            </div>
          </div>
          <div className="flex gap-3">
            <Button variant="outline" className="flex-1 border-white/10 bg-white/5 hover:bg-white/10">Bỏ qua</Button>
            <Button className="flex-1 bg-blue-600 hover:bg-blue-500">Xác nhận</Button>
          </div>
        </div>
      </div>
    </div>
  );
};

const DocumentList = ({ type, title, hideHeader = false }: { type: string, title?: string, hideHeader?: boolean }) => {
  const [docs, setDocs] = useState<Document[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getDocuments({ type }).then(setDocs).finally(() => setLoading(false));
  }, [type]);

  const getTypeName = (t: string) => {
    switch(t) {
      case 'incoming': return 'Văn bản đến';
      case 'outgoing': return 'Văn bản đi';
      case 'internal': return 'Văn bản nội bộ';
      default: return '';
    }
  };

  return (
    <div className="space-y-4">
      {!hideHeader && (
        <div className="flex justify-between items-center">
          <h1 className="text-2xl font-bold text-white">{getTypeName(type)}</h1>
          <Button className="gap-2 bg-blue-600 hover:bg-blue-500">
            <Plus className="h-4 w-4" /> Thêm mới
          </Button>
        </div>
      )}
      
      <Card className="glass border-white/10 overflow-hidden">
        {title && <CardHeader><CardTitle className="text-lg text-white">{title}</CardTitle></CardHeader>}
        <CardContent className="p-0">
          <Table>
            <TableHeader className="bg-white/5">
              <TableRow className="border-white/10 hover:bg-transparent">
                <TableHead className="text-slate-400 font-medium">Số hiệu</TableHead>
                <TableHead className="text-slate-400 font-medium">Ngày VB</TableHead>
                <TableHead className="text-slate-400 font-medium">Trích yếu</TableHead>
                <TableHead className="text-slate-400 font-medium">Nơi gửi/nhận</TableHead>
                <TableHead className="text-slate-400 font-medium">Trạng thái</TableHead>
                <TableHead className="text-slate-400 font-medium text-right">Thao tác</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading ? (
                <TableRow><TableCell colSpan={6} className="text-center py-10 text-slate-500">Đang tải...</TableCell></TableRow>
              ) : docs.length === 0 ? (
                <TableRow><TableCell colSpan={6} className="text-center py-10 text-slate-500">Không có dữ liệu</TableCell></TableRow>
              ) : docs.map(doc => (
                <TableRow key={doc.id} className="border-white/5 hover:bg-white/5 transition-colors">
                  <TableCell className="font-medium text-blue-400">{doc.doc_number}</TableCell>
                  <TableCell className="text-slate-300">{doc.doc_date}</TableCell>
                  <TableCell className="max-w-xs truncate text-slate-300">{doc.summary}</TableCell>
                  <TableCell className="text-slate-300">{doc.type === 'incoming' ? doc.sender : doc.receiver}</TableCell>
                  <TableCell>
                    <Badge className={doc.status === 'completed' ? 'bg-emerald-500/20 text-emerald-400 border-emerald-500/20' : 'bg-amber-500/20 text-amber-400 border-amber-500/20'}>
                      {doc.status === 'completed' ? 'Đã xử lý' : 'Chờ xử lý'}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-right">
                    <Button variant="ghost" size="sm" className="text-slate-400 hover:text-white hover:bg-white/10">Chi tiết</Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
};

const ImportModule = () => {
  const [file, setFile] = useState<File | null>(null);
  const [extracting, setExtracting] = useState(false);
  const [metadata, setMetadata] = useState<any>(null);

  const handleExtract = async () => {
    if (!file) return;
    setExtracting(true);
    try {
      const filePath = (file as any).path;
      if (!filePath) {
        toast.error('Không thể lấy đường dẫn tệp. Hãy thử kéo thả tệp.');
        return;
      }
      const data = await api.extractMetadata(filePath);
      setMetadata(data);
      toast.success('Trích xuất dữ liệu thành công');
    } catch (err) {
      toast.error('Trích xuất thất bại');
    } finally {
      setExtracting(false);
    }
  };

  const renderField = (label: string, field: any, key: string, isTextArea = false) => (
    <div className="space-y-1">
      <div className="flex justify-between items-center text-xs">
        <span className="text-slate-400">{label}</span>
        <div className="flex items-center gap-2">
          {field.needsReview && <span className="text-[10px] bg-amber-500/20 text-amber-400 px-1.5 rounded border border-amber-500/20">Cần kiểm tra</span>}
          <span className={`font-bold ${field.confidence > 0.8 ? 'text-emerald-400' : 'text-amber-400'}`}>
            {(field.confidence * 100).toFixed(0)}%
          </span>
        </div>
      </div>
      {isTextArea ? (
        <textarea 
          className="w-full bg-black/20 border border-white/10 rounded-md p-2 text-sm h-20 resize-none outline-none text-white focus:border-blue-500 transition-colors"
          value={field.value || ''} 
          onChange={e => setMetadata({...metadata, [key]: { ...field, value: e.target.value }})}
        />
      ) : (
        <Input 
          className="bg-black/20 border-white/10 text-sm h-9 text-white focus:border-blue-500" 
          value={field.value || ''} 
          onChange={e => setMetadata({...metadata, [key]: { ...field, value: e.target.value }})}
        />
      )}
    </div>
  );

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold text-white">Nhập văn bản (AI OCR)</h1>
      
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card className="glass border-white/10">
          <CardContent className="p-10 space-y-6">
            <div 
              className="border-2 border-dashed border-white/10 rounded-2xl p-12 text-center space-y-4 hover:bg-white/5 transition-colors cursor-pointer group"
              onClick={() => document.getElementById('file-upload')?.click()}
            >
              <div className="h-16 w-16 mx-auto bg-blue-500/10 rounded-full flex items-center justify-center group-hover:scale-110 transition-transform">
                <FileUp className="h-8 w-8 text-blue-400" />
              </div>
              <div>
                <p className="text-xl font-semibold text-white">Kéo thả file vào đây</p>
                <p className="text-sm text-slate-400">Hỗ trợ PDF, DOCX, Ảnh quét (Scan)</p>
              </div>
              <Input type="file" className="hidden" id="file-upload" onChange={e => setFile(e.target.files?.[0] || null)} />
              <Button className="bg-blue-600 hover:bg-blue-500">Chọn file từ máy tính</Button>
            </div>
            
            {file && (
              <div className="flex items-center justify-between bg-white/5 p-4 rounded-xl border border-white/10">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-blue-500/20 rounded-lg">
                    <FileText className="h-5 w-5 text-blue-400" />
                  </div>
                  <span className="text-sm font-medium text-white">{file.name}</span>
                </div>
                <Button size="sm" onClick={handleExtract} disabled={extracting} className="bg-emerald-600 hover:bg-emerald-500">
                  {extracting ? 'Đang xử lý...' : 'Bắt đầu trích xuất'}
                </Button>
              </div>
            )}
          </CardContent>
        </Card>

        {metadata && (
          <div className="glass-panel rounded-2xl p-8 space-y-6 animate-in fade-in slide-in-from-right-4 duration-500">
            <div className="flex justify-between items-center">
              <h3 className="text-xl font-bold text-white">Kết quả trích xuất</h3>
              <div className="px-3 py-1 bg-blue-500/20 rounded-full border border-blue-500/20 text-blue-400 text-xs font-bold">
                Độ tin cậy: {(metadata.overall_confidence * 100).toFixed(0)}%
              </div>
            </div>

            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                {renderField('Số văn bản', metadata.doc_number, 'doc_number')}
                {renderField('Ngày ban hành', metadata.doc_date, 'doc_date')}
              </div>
              {renderField('Cơ quan ban hành', metadata.department, 'department')}
              {renderField('Người ký', metadata.signer, 'signer')}
              {renderField('Trích yếu', metadata.summary, 'summary', true)}
            </div>

            <div className="flex gap-3 pt-4">
              <Button variant="outline" className="flex-1 border-white/10 bg-white/5 hover:bg-white/10" onClick={() => setMetadata(null)}>Hủy bỏ</Button>
              <Button className="flex-1 bg-blue-600 hover:bg-blue-500">Xác nhận & Lưu hệ thống</Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

const SettingsModule = () => {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Cài đặt hệ thống</h1>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Database className="h-5 w-5" /> Sao lưu & Phục hồi
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">Sao lưu toàn bộ cơ sở dữ liệu và tệp đính kèm vào một tệp nén.</p>
            <div className="flex gap-2">
              <Button className="flex-1">Sao lưu ngay</Button>
              <Button variant="outline" className="flex-1">Phục hồi từ file</Button>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <History className="h-5 w-5" /> Nhật ký hệ thống (Audit Log)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Button variant="outline" className="w-full">Xem nhật ký</Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
};

// --- Main App ---

export default function App() {
  const [user, setUser] = useState<User | null>(null);
  const [activeTab, setActiveTab] = useState('dashboard');
  const [isAuthReady, setIsAuthReady] = useState(false);
  const [userCount, setUserCount] = useState<number>(0);

  useEffect(() => {
    const checkAuth = async () => {
      const savedUser = localStorage.getItem('user');
      if (savedUser) {
        setUser(JSON.parse(savedUser));
      }
      
      try {
        const count = await api.getUserCount();
        setUserCount(count);
      } catch (err) {
        console.error('Failed to get user count', err);
      }
      
      setIsAuthReady(true);
    };
    
    checkAuth();
  }, []);

  const login = async (u: string, p: string) => {
    const res = await api.login(u, p);
    localStorage.setItem('token', res.token);
    localStorage.setItem('user', JSON.stringify(res.user));
    setUser(res.user);
  };

  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    setUser(null);
  };

  const handleInitAdmin = async () => {
    const username = prompt('Tên đăng nhập Admin mới:');
    if (!username) return;
    const password = prompt('Mật khẩu Admin mới:');
    if (!password) return;
    const confirmPassword = prompt('Xác nhận mật khẩu:');
    
    if (password !== confirmPassword) {
      toast.error('Mật khẩu xác nhận không khớp');
      return;
    }

    try {
      await api.initAdmin(username, password);
      toast.success('Đã khởi tạo Admin thành công. Hãy đăng nhập.');
      const count = await api.getUserCount();
      setUserCount(count);
    } catch (err) {
      toast.error('Không thể khởi tạo Admin. Có thể tài khoản đã tồn tại.');
    }
  };

  if (!isAuthReady) return null;

  if (!user) {
    return (
      <AuthContext.Provider value={{ user, login, logout }}>
        <LoginPage onInitAdmin={handleInitAdmin} showInitAdmin={userCount === 0} />
        <Toaster position="top-right" theme="dark" richColors />
      </AuthContext.Provider>
    );
  }

  const renderContent = () => {
    switch(activeTab) {
      case 'dashboard': return <Dashboard />;
      case 'incoming': return <DocumentList type="incoming" />;
      case 'outgoing': return <DocumentList type="outgoing" />;
      case 'internal': return <DocumentList type="internal" />;
      case 'import': return <ImportModule />;
      case 'settings': return <SettingsModule />;
      default: return <Dashboard />;
    }
  };

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      <SidebarProvider>
        <div className="flex min-h-screen w-full relative overflow-hidden">
          <Sidebar className="bg-slate-950/50 border-r border-white/10">
            <SidebarHeader className="p-6">
              <div className="flex items-center gap-3 font-extrabold text-xl text-blue-400 tracking-tight">
                <ShieldCheck className="h-6 w-6" />
                <span>DOCUMANAGE</span>
              </div>
            </SidebarHeader>
            <SidebarContent className="px-3">
              <SidebarMenu className="gap-1">
                <SidebarMenuItem>
                  <SidebarMenuButton 
                    onClick={() => setActiveTab('dashboard')} 
                    active={activeTab === 'dashboard'}
                    className={`gap-3 py-6 px-4 rounded-xl transition-all ${activeTab === 'dashboard' ? 'bg-white/10 text-white border border-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'}`}
                  >
                    <LayoutDashboard className="h-5 w-5" /> <span className="font-medium">Bảng điều khiển</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton 
                    onClick={() => setActiveTab('incoming')} 
                    active={activeTab === 'incoming'}
                    className={`gap-3 py-6 px-4 rounded-xl transition-all ${activeTab === 'incoming' ? 'bg-white/10 text-white border border-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'}`}
                  >
                    <FileInput className="h-5 w-5" /> <span className="font-medium">Văn bản đến</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton 
                    onClick={() => setActiveTab('outgoing')} 
                    active={activeTab === 'outgoing'}
                    className={`gap-3 py-6 px-4 rounded-xl transition-all ${activeTab === 'outgoing' ? 'bg-white/10 text-white border border-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'}`}
                  >
                    <FileOutput className="h-5 w-5" /> <span className="font-medium">Văn bản đi</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton 
                    onClick={() => setActiveTab('internal')} 
                    active={activeTab === 'internal'}
                    className={`gap-3 py-6 px-4 rounded-xl transition-all ${activeTab === 'internal' ? 'bg-white/10 text-white border border-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'}`}
                  >
                    <FileText className="h-5 w-5" /> <span className="font-medium">Văn bản nội bộ</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton 
                    onClick={() => setActiveTab('import')} 
                    active={activeTab === 'import'}
                    className={`gap-3 py-6 px-4 rounded-xl transition-all ${activeTab === 'import' ? 'bg-white/10 text-white border border-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'}`}
                  >
                    <FileUp className="h-5 w-5" /> <span className="font-medium">Nhập văn bản</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
                <SidebarMenuItem>
                  <SidebarMenuButton 
                    onClick={() => setActiveTab('settings')} 
                    active={activeTab === 'settings'}
                    className={`gap-3 py-6 px-4 rounded-xl transition-all ${activeTab === 'settings' ? 'bg-white/10 text-white border border-white/10' : 'text-slate-400 hover:bg-white/5 hover:text-white'}`}
                  >
                    <Settings className="h-5 w-5" /> <span className="font-medium">Cài đặt hệ thống</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              </SidebarMenu>
            </SidebarContent>
            <div className="mt-auto p-6 border-t border-white/10 bg-black/20">
              <div className="flex items-center justify-between">
                <div className="text-sm">
                  <p className="font-bold text-white">{user.username}</p>
                  <p className="text-xs text-slate-400 capitalize">{user.role}</p>
                </div>
                <Button variant="ghost" size="icon" onClick={logout} className="text-slate-400 hover:text-white hover:bg-white/10">
                  <LogOut className="h-4 w-4" />
                </Button>
              </div>
            </div>
          </Sidebar>
          
          <main className="flex-1 p-8 overflow-auto">
            <div className="max-w-6xl mx-auto pb-12">
              {renderContent()}
            </div>
            
            <div className="fixed bottom-4 right-8 flex gap-6 text-[10px] text-slate-500 font-medium">
              <span className="flex items-center gap-1.5"><div className="w-2 h-2 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]" /> SQLite: Connected</span>
              <span>Windows Desktop App v1.2.0</span>
              <span>Audit Log: Active</span>
              <span>Disk: 78% Free</span>
            </div>
          </main>
        </div>
      </SidebarProvider>
      <Toaster position="top-right" theme="dark" closeButton richColors />
    </AuthContext.Provider>
  );
}
