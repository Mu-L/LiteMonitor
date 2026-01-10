using System;
using System.IO;
using System.Threading; // 必须引用：用于 Mutex
using System.Windows.Forms;

namespace LiteMonitor
{
    internal static class Program
    {
        // 保持 Mutex 引用，防止被 GC 回收
        private static Mutex? _mutex = null;

        [STAThread]
        static void Main()
        {
            // =================================================================
            // ★★★ 1. 单实例互斥锁 (基于文件路径的版本) ★★★
            // =================================================================
            bool createNew;
            string mutexName;

            try
            {
                // 获取当前程序的可执行文件路径
                string exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location;

                if (string.IsNullOrEmpty(exePath))
                {
                    // 如果无法获取程序路径，使用默认的全局唯一 Key
                    mutexName = "Global\\LiteMonitor_SingleInstance_Mutex_UniqueKey";
                }
                else
                {
                    // 获取程序所在的文件夹路径
                    string appFolderPath = Path.GetDirectoryName(exePath);

                    // 1. 统一转小写 (Windows路径不区分大小写，避免 C:\App 和 c:\app 被识别为不同实例)
                    // 2. 将路径中的特殊字符（特别是反斜杠）替换为下划线，因为 Mutex 名称中不能包含 '\' (除了 Global\)
                    string sanitizedPath = appFolderPath?.ToLower()
                                                        .Replace('\\', '_')
                                                        .Replace(':', '_')
                                                        .Replace('/', '_')
                                                        .Replace(' ', '_');

                    // 创建基于文件夹路径的互斥锁名称
                    mutexName = $"Global\\LiteMonitor_SingleInstance_{sanitizedPath}_Mutex";
                }

                // 尝试创建/获取锁
                // out createNew: 如果是第一个创建的，返回 true；如果锁已存在，返回 false
                _mutex = new Mutex(true, mutexName, out createNew);
            }
            catch
            {
                // 如果路径获取或处理出现异常，回退到原来的逻辑，保证程序至少能以单实例模式运行
                mutexName = "Global\\LiteMonitor_SingleInstance_Mutex_UniqueKey";
                _mutex = new Mutex(true, mutexName, out createNew);
            }

            if (!createNew)
            {
                // 检测到程序已经在运行：直接 return 结束，不弹窗，不报错。
                return; 
            }

            // =================================================================
            // ★★★ 2. 注册全局异常捕获事件 (保留你的原始逻辑) ★★★
            // =================================================================
            // 捕获 UI 线程的异常
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            
            // 捕获非 UI 线程（后台线程）的异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // =================================================================
            // ★★★ 3. 启动应用 ★★★
            // =================================================================
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());

            // 显式释放锁 (好习惯，虽然进程结束也会释放)
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
            }
        }

        // --- 异常处理委托 ---
        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            LogCrash(e.Exception, "UI_Thread");
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogCrash(e.ExceptionObject as Exception, "Background_Thread");
        }

        // --- 写入 crash.log 的核心方法 ---
        static void LogCrash(Exception? ex, string source)
        {
            if (ex == null) return;

            try
            {
                // 日志文件保存在程序运行目录下
                string logPath = Path.Combine(AppContext.BaseDirectory, "LiteMonitor_Error.log");
                
                string errorMsg = "==================================================\n" +
                                  $"[Time]: {DateTime.Now}\n" +
                                  $"[Source]: {source}\n" +
                                  $"[Message]: {ex.Message}\n" +
                                  $"[Stack]:\n{ex.StackTrace}\n" +
                                  "==================================================\n\n";

                File.AppendAllText(logPath, errorMsg);

                // 只有真的崩了才弹窗提示用户
                MessageBox.Show($"程序遇到致命错误！\n错误日志已保存至：{logPath}\n\n原因：{ex.Message}", 
                                "LiteMonitor Crash", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch 
            {
                // 如果日志都写不进去，通常是磁盘满了或权限极度受限，只能忽略
            }
        }
    }
}