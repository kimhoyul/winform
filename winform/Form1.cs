using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            
            this.HandleCreated += (s, e) =>
            {
                Task.Factory.StartNew(() => StartPipeServer(), TaskCreationOptions.LongRunning);
            };

            //try
            //{
            //    process = new Process();
            //    process.StartInfo.FileName = @"bin\Simul-WaferHBM.exe";

            //    process.StartInfo.Arguments = "-parentHWND " + panel1.Handle.ToInt32() + " " + Environment.CommandLine;
            //    process.StartInfo.UseShellExecute = true;
            //    process.StartInfo.CreateNoWindow = true;

            //    process.Start();

            //    process.WaitForInputIdle();

            //    // 실행이 되지 않는다면 아래 sleep 주어 Unity Game 가 로드되는 시간을 늘려 주면됩니다.
            //    // 시간을 주어야 실행이 가능하다.
            //    Thread.Sleep(3000);

            //    //unityHWND = process.MainWindowHandle;
            //    EnumChildWindows(panel1.Handle, WindowEnum, IntPtr.Zero);

            //    //unityHWNDLabel.Text = "Unity HWND: 0x" + unityHWND.ToString("X8");
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message + ".\nCheck if Container.exe is placed next to Child.exe.");
            //}

            //Task.Factory.StartNew(() => StartPipeServer(), TaskCreationOptions.LongRunning);
        }

        private void StartPipeServer()
        {
            pipeServer = new NamedPipeServerStream("MyPipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            // Console.WriteLine("파이프 서버: Unity 연결 대기 중...");
            BeginInvoke(() => textBox1.Text = "파이프 서버: Unity 연결 대기 중...");

            pipeServer.WaitForConnection();
            reader = new StreamReader(pipeServer, Encoding.UTF8);
            writer = new StreamWriter(pipeServer, Encoding.UTF8) { AutoFlush = true };

            //Console.WriteLine("파이프 서버: Unity 연결됨");
            BeginInvoke(() => textBox1.Text = "파이프 서버: Unity 연결됨");

            // 읽기 루프 시작
            Task.Factory.StartNew(() => ReadLoop(), TaskCreationOptions.LongRunning);
        }

        private void ReadLoop()
        {
            BeginInvoke(() => textBox1.Text = "ReadLoop 시작됨");

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
                            if (line.Contains("GetStackMap"))
                            {
                                string noinkMapJsonContent = GetJsonData("noinkmapgradecolor_list_data");
                                if (noinkMapJsonContent == "error")
                                    return;

                                Send(noinkMapJsonContent);

                                string stackMapJsonContent = GetJsonData("stackmap");
                                if (stackMapJsonContent == "error")
                                    return;

                                Send(stackMapJsonContent);

                                string stackNoinkMapJsonContent = GetJsonData("stacknoinkmap");
                                if (stackNoinkMapJsonContent == "error")
                                    return;

                                Send(stackNoinkMapJsonContent);
                            }

                            if (line.StartsWith("GetNoinkMap"))
                            {
                                string[] parts = line.Split(':');
                                if (parts.Length == 2)
                                {
                                    string chipData = parts[1].Trim(); // 공백 제거
                                    string noinkMapJsonContent = GetNoinkMapData(chipData);

                                    Send(noinkMapJsonContent);
                                }
                            }
                        });
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                BeginInvoke(() => MessageBox.Show("Unity와의 파이프 연결이 종료되었습니다.\n"));
                BeginInvoke(() => textBox1.Text = "Unity와의 파이프 연결이 종료되었습니다.\n");
                pipeServer?.Dispose();
                pipeServer = null;

                // start over
                Task.Factory.StartNew(() => StartPipeServer(), TaskCreationOptions.LongRunning);
            }
            catch (IOException ex)
            {
                BeginInvoke(() => MessageBox.Show("Unity와의 파이프 연결이 끊어졌습니다.\n" + ex.Message));
            }
        }

        private string GetJsonData(string fileName)
        {
            string jsonFilePath = $@"Data\{fileName}.json";

            if (!File.Exists(jsonFilePath))
            {
                ShowError($"JSON 파일이 존재하지 않습니다:\n{jsonFilePath}");
                return "error";
            }

            string jsonContent = File.ReadAllText(jsonFilePath, Encoding.UTF8);

            return jsonContent;
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
            public string DIE_X_COORDINATE { get; set; }
            public string DIE_Y_COORDINATE { get; set; }
        }

        public class NoInkMapItemList
        {
            public List<NoInkMapItem> noinkmap_list { get; set; }
        }

        private string GetNoinkMapData(string chipData)
        {
            string jsonText = GetJsonData("noinkmap");
            if (jsonText == "error")
                return "";

            string wfId = chipData.Substring(7, 2);
            string xPos = int.Parse(chipData.Substring(9, 3)).ToString();
            string yPos = int.Parse(chipData.Substring(12, 3)).ToString();

            var jsonRoot = JsonConvert.DeserializeObject<NoInkMapItemList>(jsonText);

            var targetItem = jsonRoot.noinkmap_list.FirstOrDefault(item =>
                item.WF_ID == wfId &&
                item.X_POSITION == xPos &&
                item.Y_POSITION == yPos);

            if (targetItem == null)
                return "";

            var filteredItems = jsonRoot.noinkmap_list
                .Where(item => item.WF_ID == targetItem.WF_ID)
                .ToList();

            var result = new NoInkMapItemList { noinkmap_list = filteredItems };

            return JsonConvert.SerializeObject(result, Formatting.Indented);
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

            try
            {
                writer?.Close();
                reader?.Close();
                pipeServer?.Dispose();
                pipeServer = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Named Pipe 정리 중 오류:\n" + ex.Message);
            }
        }

        private void ContainerForm_Activated(object sender, EventArgs e)
        {
            ActivateUnityWindow();
        }

        private void ContainerForm_Deactivate(object sender, EventArgs e)
        {
            DeactivateUnityWindow();
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
                ShowError($"파일 읽기 또는 전송 중 오류 발생:\n{ex.Message}");
                return false;
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "전송 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            string jsonContent = GetJsonData("wafer_data");

            if (jsonContent == "error")
                return;

            Send(jsonContent);
        }
    }
}