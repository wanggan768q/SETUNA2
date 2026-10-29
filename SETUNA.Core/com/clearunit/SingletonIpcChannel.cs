using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace com.clearunit
{
    /// <summary>
    /// 单实例跨进程通知通道：第一实例开 NamedPipeServer 监听，第二实例连上后
    /// 发送 version + args（JSON 行）触发第一实例的 SingletonAppRemoteObject.Event。
    /// 替代 .NET Core 已移除的 IpcChannel/Remoting（master 语义：二次启动激活已运行实例）。
    /// </summary>
    public static class SingletonIpcChannel
    {
        /// <summary>第二实例启动转发回调签名（与 ISingletonForm.DetectExternalStartup 一致）。</summary>
        public delegate void StartupReceivedHandler(string version, string[] args);

        private const string PipeSuffix = ".setuna.singleinstance";
        private static CancellationTokenSource _cts;

        /// <summary>第一实例收到第二实例转发时触发（宿主订阅它处理激活/参数）。</summary>
        public static event StartupReceivedHandler ExternalStartupReceived;

        /// <summary>管道名由产品名派生，两宿主一致。</summary>
        private static string PipeName => $"Global\\{System.Windows.Forms.Application.ProductName}{PipeSuffix}";

        /// <summary>第一实例启动监听；收到消息后回调 SingletonAppRemoteObject.Event（即宿主注册的 listener）。</summary>
        public static void StartServer()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(
                            PipeName, PipeDirection.InOut, 1,
                            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        await server.WaitForConnectionAsync(token);
                        using var reader = new StreamReader(server);
                        var line = reader.ReadLine();
                        if (line != null)
                        {
                            Parse(line, out var version, out var args);
                            SingletonAppRemoteObject.Event?.Invoke(version, args);
                            ExternalStartupReceived?.Invoke(version, args);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // 单条连接失败不断开监听（对齐旧 Remoting 容错语义）
                    }
                }
            }, token);
        }

        /// <summary>第二实例向已运行实例转发 version+args。</summary>
        public static void SendStartup(string version, string[] args)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                // 已运行实例 3 秒内连不上即放弃（服务异常时不悬挂第二实例）
                client.Connect(3000);
                using var writer = new StreamWriter(client);
                writer.WriteLine(Encode(version, args));
                writer.Flush();
            }
            catch
            {
                Console.WriteLine("CreateClient Error");
            }
        }

        private static string Encode(string version, string[] args)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { version, args });
        }

        private static void Parse(string line, out string version, out string[] args)
        {
            version = null;
            args = Array.Empty<string>();
            try
            {
                var o = Newtonsoft.Json.Linq.JObject.Parse(line);
                version = (string)o["version"];
                args = o["args"]?.ToObject<string[]>() ?? Array.Empty<string>();
            }
            catch
            {
            }
        }
    }
}
