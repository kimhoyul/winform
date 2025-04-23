using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace winform
{
    public partial class Form1 : Form
    {
        private NamedPipeServerStream? pipeServer;

        [DllImport("User32.dll")]
        static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool redraw);

        internal delegate int WindowEnumProc(IntPtr hwnd, IntPtr lparam);
        [DllImport("user32.dll")]
        internal static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc func, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private Process? process;
        private IntPtr unityHWND = IntPtr.Zero;

        private const int WM_ACTIVATE = 0x0006;
        private readonly IntPtr WA_ACTIVE = new IntPtr(1);
        private readonly IntPtr WA_INACTIVE = new IntPtr(0);

        private StreamWriter? writer;
        private StreamReader? reader;

        public Form1()
        {
            InitializeComponent();

            try
            {
                process = new Process();
                process.StartInfo.FileName = @"D:\Unity\Project\sk\bin\Simul-WaferHBM.exe";

                process.StartInfo.Arguments = "-parentHWND " + panel1.Handle.ToInt32() + " " + Environment.CommandLine;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                process.WaitForInputIdle();

                // 실행이 되지 않는다면 아래 sleep 주어 Unity Game 가 로드되는 시간을 늘려 주면됩니다.
                // 시간을 주어야 실행이 가능하다.
                Thread.Sleep(3000);

                // Doesn't work for some reason ?!
                //unityHWND = process.MainWindowHandle;
                EnumChildWindows(panel1.Handle, WindowEnum, IntPtr.Zero);

                //unityHWNDLabel.Text = "Unity HWND: 0x" + unityHWND.ToString("X8");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ".\nCheck if Container.exe is placed next to Child.exe.");
            }

            Task.Factory.StartNew(() => StartPipeServer(), TaskCreationOptions.LongRunning);
        }

        private void StartPipeServer()
        {
            pipeServer = new NamedPipeServerStream("MyPipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            Console.WriteLine("파이프 서버: Unity 연결 대기 중...");

            pipeServer.WaitForConnection();
            reader = new StreamReader(pipeServer, Encoding.UTF8);
            writer = new StreamWriter(pipeServer, Encoding.UTF8) { AutoFlush = true };

            Console.WriteLine("파이프 서버: Unity 연결됨");

            // 읽기 루프 시작
            Task.Factory.StartNew(() => ReadLoop(), TaskCreationOptions.LongRunning);
        }

        private void ReadLoop()
        {
            Console.WriteLine("ReadLoop 시작됨");

            try
            {
                while (pipeServer != null && pipeServer.IsConnected)
                {
                    Console.WriteLine("ReadLine() 호출 직전");
                    string? line = reader?.ReadLine();
                    Console.WriteLine("ReadLine() 반환: " + line);
                    if (!string.IsNullOrEmpty(line))
                    {
                        BeginInvoke(() =>
                        {
                            label1.Text = "[Unity] " + line;
                        });
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
            }
            catch (IOException ex)
            {
                BeginInvoke(() => MessageBox.Show("Unity와의 파이프 연결이 끊어졌습니다.\n" + ex.Message));
            }
        }

        private void ActivateUnityWindow()
        {
            SendMessage(unityHWND, WM_ACTIVATE, WA_ACTIVE, IntPtr.Zero);
        }

        private void DeactivateUnityWindow()
        {
            SendMessage(unityHWND, WM_ACTIVATE, WA_INACTIVE, IntPtr.Zero);
        }

        private int WindowEnum(IntPtr hwnd, IntPtr lparam)
        {
            unityHWND = hwnd;
            ActivateUnityWindow();
            return 0;
        }

        private void ContainerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                process?.CloseMainWindow();

                Thread.Sleep(1000);
                while (process != null && !process.HasExited)
                    process.Kill();
            }
            catch (Exception) { }
        }

        private void ContainerForm_Activated(object sender, EventArgs e)
        {
            ActivateUnityWindow();
        }

        private void ContainerForm_Deactivate(object sender, EventArgs e)
        {
            DeactivateUnityWindow();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            if (pipeServer != null && pipeServer.IsConnected && writer != null)
            {
                try
                {
                    string jsonFilePath = @"D:\Unity\Project\sk\bin\wafer_data.json";

                    if (!File.Exists(jsonFilePath))
                    {
                        MessageBox.Show("JSON 파일이 존재하지 않습니다: " + jsonFilePath);
                        return;
                    }

                    string jsonContent = File.ReadAllText(jsonFilePath, Encoding.UTF8);

                    writer.WriteLine(jsonContent);
                    writer.WriteLine("<END>");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("파일 읽기 또는 전송 중 오류 발생:\n" + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Unity와 아직 연결되지 않았습니다.");
            }
        }
    }
}