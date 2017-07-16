using RUDPCore;
using System;
using System.Threading;

namespace TestRUDP
{
    public class TestCommonTrans
    {
        private RSocket client;
        private RSocket server;
        private bool isRunning = true;
        public void Test()
        {
            server = new RSocket(43210);
            client = new RSocket("127.0.0.1", 43210);
            server.OnRecvData = ServerRecv;
            client.OnRecvData = ClientRecv;

            Thread tick = new Thread(TickSocket);
            tick.Start();
            string info = "";
            while ((info = Console.ReadLine()).Length > 0)
            {
                client.Send(StrToBytes(info));
            }
            isRunning = false;
            client.Close();
            server.Close();
        }

        private void ClientRecv(byte[] data)
        {
            Console.WriteLine("Client : {0}", BytesToStr(data));
        }

        private void ServerRecv(byte[] data)
        {
            Console.WriteLine("Server : {0}", BytesToStr(data));
            server.Send(data);
        }

        private byte[] StrToBytes(string info)
        {
            return System.Text.Encoding.Default.GetBytes(info);
        }
        private string BytesToStr(byte[] bs)
        {
            return System.Text.Encoding.Default.GetString(bs);
        }

        public void TickSocket()
        {
            while (isRunning)
            {
                server.Tick();
                client.Tick();
                Thread.Sleep(100);
            }
        }


    }
}
