using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace winform
{
    public partial class Form1 : Form
    {
        private NamedPipeServerStream? pipeServer;
        private Process? process;
        private IntPtr unityHWND = IntPtr.Zero;
        private StreamWriter? writer;
        private StreamReader? reader;

        [DllImport("user32.dll")]
        static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool redraw);

        internal delegate int WindowEnumProc(IntPtr hwnd, IntPtr lparam);
        [DllImport("user32.dll")]
        internal static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc func, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_ACTIVATE = 0x0006;
        private readonly IntPtr WA_ACTIVE = new IntPtr(1);
        private readonly IntPtr WA_INACTIVE = new IntPtr(0);

        public Form1()
        {
            InitializeComponent();

            // Designer에서 Dock으로 채웠다면 해제
            panel1.Dock = DockStyle.None;
            // 필요하다면 Anchor도 설정
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // 폼 리사이즈 시 항상 패널과 Unity 창 재배치
            this.Resize += Form1_Resize;
            // 폼 생성 직후 레이아웃 및 파이프 서버 시작
            this.HandleCreated += (s, e) =>
            {
                LayoutPanel();
                Form1_Resize(null, null);
                Task.Factory.StartNew(StartPipeServer, TaskCreationOptions.LongRunning);
            };

            // Unity 프로세스 띄우기
            StartUnityProcess();
        }

        /// <summary>
        /// panel1을 label1 바로 아래, 남은 영역 전부 차지하도록 위치·크기 지정
        /// </summary>
        private void LayoutPanel()
        {
            // label1 하단 + 여유 여백 10px
            int y = label1.Bottom + 10;
            panel1.SetBounds(
                /* x */ panel1.Left,
                /* y */ y,
                /* width */ ClientSize.Width - panel1.Left - /* right margin */ panel1.Left,
                /* height */ ClientSize.Height - y
            );
        }

        private void Form1_Resize(object? sender, EventArgs e)
        {
            LayoutPanel();

            // Unity 자식창도 패널 크기에 맞춰 리사이즈
            if (unityHWND != IntPtr.Zero)
                MoveWindow(unityHWND, 0, 0, panel1.Width, panel1.Height, true);
        }

        private void StartUnityProcess()
        {
            try
            {
                process = new Process
                {
                    StartInfo =
                    {
                        FileName        = @"bin\Simul-WaferHBM.exe",
                        Arguments       = "-parentHWND " + panel1.Handle.ToInt32() + " " + Environment.CommandLine,
                        UseShellExecute = true,
                        CreateNoWindow  = true
                    }
                };
                process.Start();
                process.WaitForInputIdle();
                Thread.Sleep(3000);
                EnumChildWindows(panel1.Handle, WindowEnum, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}\nCheck if Container.exe is placed next to Child.exe.");
            }
        }

        private void StartPipeServer()
        {
            pipeServer = new NamedPipeServerStream("MyPipe", PipeDirection.InOut, 1,
                                                   PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            BeginInvoke(() => textBox1.Text = "파이프 서버: Unity 연결 대기 중...");
            pipeServer.WaitForConnection();
            reader = new StreamReader(pipeServer, Encoding.UTF8);
            writer = new StreamWriter(pipeServer, Encoding.UTF8) { AutoFlush = true };
            BeginInvoke(() => textBox1.Text = "파이프 서버: Unity 연결됨");
            Task.Factory.StartNew(ReadLoop, TaskCreationOptions.LongRunning);
        }

        private void ReadLoop()
        {
            BeginInvoke(() => textBox1.Text = "ReadLoop 시작됨");
            try
            {
                while (pipeServer != null && pipeServer.IsConnected)
                {
                    var line = reader?.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        BeginInvoke(() =>
                        {
                            if (line.Contains("GetStackMap"))
                            {
                                SendIfNotError(GetJsonData("noinkmapgradecolor_list_data"));
                                SendIfNotError(GetJsonData("stacknoinkmap"));
                                SendIfNotError(GetJsonData("stackmap"));
                            }
                            else if (line.StartsWith("GetNoinkMap"))
                            {
                                var chipData = line.Split(':').ElementAtOrDefault(1)?.Trim();
                                if (!string.IsNullOrEmpty(chipData))
                                    Send(GetNoinkMapData(chipData));
                            }
                        });
                    }
                    else Thread.Sleep(1);
                }

                BeginInvoke(() => MessageBox.Show("Unity와의 파이프 연결이 종료되었습니다."));
                BeginInvoke(() => textBox1.Text = "Unity와의 파이프 연결이 종료되었습니다.");
                pipeServer?.Dispose();
                pipeServer = null;

                // 재시도
                Task.Factory.StartNew(StartPipeServer, TaskCreationOptions.LongRunning);
            }
            catch (IOException ex)
            {
                BeginInvoke(() => MessageBox.Show("Unity와의 파이프 연결이 끊어졌습니다.\n" + ex.Message));
            }
        }

        private void SendIfNotError(string json)
        {
            if (json != "error")
                Send(json);
        }

        private string GetJsonData(string fileName)
        {
            var path = Path.Combine("Data", fileName + ".json");
            if (!File.Exists(path))
            {
                ShowError($"JSON 파일이 존재하지 않습니다:\n{path}");
                return "error";
            }
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public class NoInkMapItem
        {
            public string LOT_ID { get; set; }
            public string WF_ID { get; set; }
            public string OPER_ID { get; set; }
            public string TSV_TYPE { get; set; }
            public string PASS_DIE_QTY { get; set; }
            public string FLAT_ZONE_TYPE { get; set; }
            public string STACK_NO { get; set; }
            public string X_AXIS { get; set; }
            public string Y_AXIS { get; set; }
            public string X_POSITION { get; set; }
            public string Y_POSITION { get; set; }
            public string DIE_VAL { get; set; }
            public string DIE_THICKNESS { get; set; }
            public string DIE_X_COORDINATE { get; set; }
            public string DIE_Y_COORDINATE { get; set; }
        }

        public class NoInkMapItemList
        {
            public List<NoInkMapItem> noinkmap_list { get; set; }
        }

        private string GetNoinkMapData(string chipData)
        {
            var jsonText = GetJsonData("noinkmap");
            if (jsonText == "error") return "";

            var wfId = chipData.Substring(7, 2);
            var xPos = int.Parse(chipData.Substring(9, 3)).ToString();
            var yPos = int.Parse(chipData.Substring(12, 3)).ToString();
            var root = JsonConvert.DeserializeObject<NoInkMapItemList>(jsonText);

            var target = root.noinkmap_list.FirstOrDefault(item =>
                item.WF_ID == wfId &&
                item.X_POSITION == xPos &&
                item.Y_POSITION == yPos);
            if (target == null) return "";

            var filtered = root.noinkmap_list.Where(item => item.WF_ID == target.WF_ID).ToList();
            return JsonConvert.SerializeObject(
                new NoInkMapItemList { noinkmap_list = filtered },
                Formatting.Indented
            );
        }

        private bool Send(string json)
        {
            if (pipeServer == null || !pipeServer.IsConnected || writer == null)
            {
                ShowError("Unity와 아직 연결되지 않았습니다.");
                return false;
            }
            try
            {
                writer.WriteLine(json);
                writer.WriteLine("<END>");
                return true;
            }
            catch (Exception ex)
            {
                ShowError($"전송 중 오류 발생:\n{ex.Message}");
                return false;
            }
        }

        private void ShowError(string message)
            => MessageBox.Show(message, "전송 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);

        private void button1_Click_1(object sender, EventArgs e)
        {
            var json = GetJsonData("wafer");
            if (json != "error") Send(json);
        }

        private int WindowEnum(IntPtr hwnd, IntPtr lparam)
        {
            unityHWND = hwnd;
            // 활성화 메시지
            SendMessage(unityHWND, WM_ACTIVATE, WA_ACTIVE, IntPtr.Zero);
            // 최초 사이즈 맞춤
            MoveWindow(unityHWND, 0, 0, panel1.Width, panel1.Height, true);
            return 0;
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            // 현재는 빈 핸들러
        }

        private void ContainerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                process?.CloseMainWindow();
                Thread.Sleep(500);
                if (process != null && !process.HasExited)
                    process.Kill();
            }
            catch { }

            try
            {
                writer?.Close();
                reader?.Close();
                pipeServer?.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Named Pipe 정리 중 오류:\n" + ex.Message);
            }
        }

        private void ContainerForm_Activated(object sender, EventArgs e)
            => SendMessage(unityHWND, WM_ACTIVATE, WA_ACTIVE, IntPtr.Zero);

        private void ContainerForm_Deactivate(object sender, EventArgs e)
            => SendMessage(unityHWND, WM_ACTIVATE, WA_INACTIVE, IntPtr.Zero);
    }
}
