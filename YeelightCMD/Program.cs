using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace YeelightCMD
{
    class Program
    {
        // 组播主机
        private static string MULTICAST_HOST = "239.255.255.250";
        // 组播端口
        private static int MULTICAST_PORT = 1982;
        // 组播地址对象
        private static IPEndPoint MULTICAST_IP_END_POINT = new IPEndPoint(IPAddress.Parse(MULTICAST_HOST), MULTICAST_PORT);
        // 搜索设备广播内容
        private static string SEARCH_DEVICE_MULTCAST_CONTENT = "M-SEARCH * HTTP/1.1\r\nHOST:239.255.255.250:1982\r\nMAN:\"ssdp:discover\"\r\nST:wifi_bulb\r\n";
        // 地址正则
        private static Regex LOCATION_REGEX = new Regex(@"Location: yeelight://(.+):(.+)\r\n");
        // ID正则
        private static Regex ID_REGEX = new Regex(@"id: (.+)\r\n");

        // 操作超时时间
        private static int RECV_TIMEOUT = 5000;

        static void Main(string[] args)
        {
            // 创建线程
            Thread timeout = new Thread(new ThreadStart(TimeoutHandle));
            // 启动线程
            timeout.Start();

            // 获取本机IP地址列表
            List<string> ips = GetLocalIPs();

            // 遍历本机IP
            foreach (string localIP in ips)
            {
                // 启动开关灯线程
                new Thread(new ParameterizedThreadStart(Toggle)).Start(localIP);
            }
        }

        /// <summary>
        /// 获取本机IP地址
        /// </summary>
        /// <returns>IP地址列表</returns>
        static List<string> GetLocalIPs()
        {
            IPHostEntry localhost = Dns.GetHostEntry(Dns.GetHostName());
            var localIPs = localhost.AddressList.Where<IPAddress>(ip => ip.AddressFamily.ToString().Equals("InterNetwork"));
            List<string> results = new List<string>();

            foreach (var item in localIPs)
            {
                results.Add(item.ToString());
            }

            return results;
        }

        /// <summary>
        /// 搜索设备
        /// </summary>
        /// <returns>Yeelight对象</returns>
        static Yeelight SearchDevice(string localIP)
        {
            try
            {
                // 创建UDP客户端
                UdpClient client = new UdpClient(new IPEndPoint(IPAddress.Parse(localIP), 0));

                // 将组播内容转为byte
                byte[] contentBuf = Encoding.Default.GetBytes(SEARCH_DEVICE_MULTCAST_CONTENT);
                // 发送组播
                client.Send(contentBuf, contentBuf.Length, MULTICAST_IP_END_POINT);
                // 接收回应
                byte[] resBuf = client.Receive(ref MULTICAST_IP_END_POINT);
                // 处理回应数据
                string resStr = Encoding.Default.GetString(resBuf);

                Debug.WriteLine(resStr);

                // 处理
                var idGroups = ID_REGEX.Match(resStr).Groups;
                var locationGroups = LOCATION_REGEX.Match(resStr).Groups;
                string id = idGroups[1].ToString();
                string ip = locationGroups[1].ToString();
                int port = Convert.ToInt32(locationGroups[2].ToString());

                // 创建对象
                return new Yeelight(id, ip, port);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return null;
            }

        }

        /// <summary>
        /// 开/关灯
        /// </summary>
        /// <param name="localIP">本机IP</param>
        static void Toggle(object localIP)
        {
            // 搜索设备
            Yeelight device = SearchDevice((string)localIP);

            if (device != null)
            {
                // 开/关灯
                bool success = device.Toggle();

                // 发送成功
                if (success)
                {
                    // 退出
                    System.Environment.Exit(0);
                }
            }
        }

        /// <summary>
        /// 超时处理
        /// </summary>
        static void TimeoutHandle()
        {
            // 等待超时时间
            Thread.Sleep(RECV_TIMEOUT);
            // 退出
            System.Environment.Exit(0);
        }
    }
}
