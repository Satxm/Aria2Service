using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceProcess;

namespace Aria2Service
{
    public partial class Aria2Service : ServiceBase
    {

        public Aria2Service()
        {
            InitializeComponent();
        }

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct Security_Attributes
        {
            public int Length;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct StartUpInfo
        {
            public int cb;
            public String lpReserved;
            public String lpDesktop;
            public String lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Process_Information
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        #endregion

        #region Enumerations
        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        enum Token_Type : int
        {
            TokenPrimary = 1,
            TokenImpersonation = 2
        }

        enum Security_Impersonation_Level : int
        {
            SecurityAnonymous = 0,
            SecurityIdentification = 1,
            SecurityImpersonation = 2,
            SecurityDelegation = 3,
        }

        enum WTSInfoClass
        {
            InitialProgram,
            ApplicationName,
            WorkingDirectory,
            OEMId,
            SessionId,
            UserName,
            WinStationName,
            DomainName,
            ConnectState,
            ClientBuildNumber,
            ClientName,
            ClientDirectory,
            ClientProductId,
            ClientHardwareId,
            ClientAddress,
            ClientDisplay,
            ClientProtocolType
        }
        #endregion

        #region Constants

        public const int TOKEN_DUPLICATE = 0x0002;
        public const uint MAXIMUM_ALLOWED = 0x2000000;
        public const int CREATE_NEW_CONSOLE = 0x00000010;
        public const int CREATE_NO_WINDOW = 0x08000000;
        public const int STARTF_USESHOWWINDOW = 0x00000001;

        public const int IDLE_PRIORITY_CLASS = 0x40;
        public const int NORMAL_PRIORITY_CLASS = 0x20;
        public const int HIGH_PRIORITY_CLASS = 0x80;
        public const int REALTIME_PRIORITY_CLASS = 0x100;

        private string dir = AppDomain.CurrentDomain.BaseDirectory;
        private string cmd = "aria2c.exe";
        private string arg = " --conf-path=aria2.conf";
        private Process_Information pi;

        #endregion

        #region Win32 API Imports

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hSnapshot);


        [DllImport("kernel32.dll")]
        static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        static extern bool WTSQuerySessionInformation(System.IntPtr hServer, int sessionId, WTSInfoClass wtsInfoClass, out System.IntPtr ppBuffer, out uint pBytesReturned);

        [DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUser", SetLastError = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public extern static bool CreateProcessAsUser(IntPtr hToken, String lpApplicationName, String lpCommandLine, ref Security_Attributes lpProcessAttributes,
            ref Security_Attributes lpThreadAttributes, bool bInheritHandle, int dwCreationFlags, IntPtr lpEnvironment,
            String lpCurrentDirectory, ref StartUpInfo lpStartUpInfo, out Process_Information lpProcessInformation);

        [DllImport("kernel32.dll")]
        static extern bool ProcessIdToSessionId(uint dwProcessId, ref uint pSessionId);

        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx")]
        public extern static bool DuplicateTokenEx(IntPtr ExistingTokenHandle, uint dwDesiredAccess,
            ref Security_Attributes lpThreadAttributes, int TokenType,
            int ImpersonationLevel, ref IntPtr DuplicateTokenHandle);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("advapi32", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, ref IntPtr TokenHandle);

        #endregion

        public static bool StartProcess(String cmd, String arg, String dir, out Process_Information pi)
        {
            //登陆进程的id
            uint winlogonPid = 0;
            //     用户的访问令牌               登陆进程的访问令牌     登陆进程的句柄
            IntPtr hUserTokenDup = IntPtr.Zero, hPToken = IntPtr.Zero, hProcess = IntPtr.Zero;
            // 进程信息
            pi = new Process_Information();

            //   获取当前活动会话id；系统中每个登录的用户都有一个唯一的会话id
            uint dwSessionId = WTSGetActiveConsoleSessionId();

            //获取当前活动会话中运行的winlogon进程(身份认证)的进程id即用户登陆窗口进程id
            Process[] processes = Process.GetProcessesByName("winlogon");
            foreach (Process p in processes)
            {
                if ((uint)p.SessionId == dwSessionId)
                {
                    winlogonPid = (uint)p.Id;
                }
            }

            // 获取winlogon进程的句柄
            hProcess = OpenProcess(MAXIMUM_ALLOWED, false, winlogonPid);

            // 获取winlogon进程访问令牌的句柄
            if (!OpenProcessToken(hProcess, TOKEN_DUPLICATE, ref hPToken))
            {
                // 使登陆进程句柄无效
                CloseHandle(hProcess);
                return false;
            }

            // DuplicateTokenEx和CreateProcessAsUser中使用的安全属性结构我希望不必使用安全属性变量
            //只需传递null并继承（默认情况下）安全属性现有令牌的。然而，在C语言中，结构是值类型，因此无法分配空值。
            // 初始化安全属性结构体
            Security_Attributes sa = new Security_Attributes();
            sa.Length = Marshal.SizeOf(sa);

            // 复制winlogon进程的访问令牌；新创建的令牌将是主令牌
            if (!DuplicateTokenEx(hPToken, MAXIMUM_ALLOWED, ref sa, (int)Security_Impersonation_Level.SecurityIdentification, (int)Token_Type.TokenPrimary, ref hUserTokenDup))
            {
                // 使登陆进程句柄无效
                CloseHandle(hProcess);
                // 使登陆进程的令牌句柄无效
                CloseHandle(hPToken);
                return false;
            }

            // 默认情况下，CreateProcessAsUser在非交互式窗口站上创建进程，这意味着窗口站有一个不可见的桌面，进程无法接收用户输入。
            // 为了解决这个问题，我们设置了lpDesktop参数来表示我们想要启用用户与新流程的互动。
            // 用于指定新进程的主视窗特性的结构体
            StartUpInfo si = new StartUpInfo();
            si.cb = (int)Marshal.SizeOf(si);

            // 交互式窗口站参数；基本上，这表明创建的进程可以在桌面上显示GUI
            si.lpDesktop = @"winsta0\default";

            // 指定进程的优先级和创建方法的标志
            // dwCreationFlags = NORMAL_PRIORITY_CLASS | CREATE_NEW_CONSOLE;

            // 进程在没有控制台窗口的情况下运行
            int dwCreationFlags = NORMAL_PRIORITY_CLASS | CREATE_NO_WINDOW;

            // 在当前用户的登录会话中创建新进程
            bool result = CreateProcessAsUser(hUserTokenDup,        // 用户的访问令牌
                                            cmd,                    // 要执行的文件
                                            arg,                    // 命令行参数
                                            ref sa,                 // 指向进程安全属性的指针 
                                            ref sa,                 // 指向线程安全属性的指针
                                            false,                  // 句柄不可继承
                                            dwCreationFlags,        // 创建标志
                                            IntPtr.Zero,            // 指向新环境块的指针  
                                            dir,                    // 当前目录的名称
                                            ref si,                 // 指向StartUpInfo结构的指针
                                            out pi                  // 接收有关新进程的信息
                                            );

            // 使句柄无效
            CloseHandle(hProcess);
            CloseHandle(hPToken);
            CloseHandle(hUserTokenDup);

            return result; // 返回结果
        }

        // 结束进程树
        public static void KillProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                Console.WriteLine(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                /* process already exited */
            }
        }

        // 服务启动执行
        protected override void OnStart(string[] args)
        {
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            if (!File.Exists(Path.Combine(dir, "aria2.session"))) File.Create(Path.Combine(dir, "aria2.session"));
            StartProcess(Path.Combine(dir, cmd), arg, dir, out pi);

            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        // 服务停止执行
        protected override void OnStop()
        {
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            KillProcessAndChildren((int)pi.dwProcessId);

            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

    }
}
