using RUDPCore;
using System;

namespace TestRUDP
{
    class TestNetPacket
    {

        public void Run()
        {
            var msg = NetPacket.Create();
            msg.Protcol = 16;
            msg.MsgNum = 3;
            msg.Piece = 2;
            msg.Session = 100;
            var data = RSocketUtils.StrToBytes("HelloWorld");
            msg.SetData(data);
            msg.Type = PacketType.Reliable;
            msg.ACK = 6;
            msg.SetData(data);
            var bs = msg.Serialize();
            Console.WriteLine(msg.CRC);

            var recvPacket = NetPacket.Create();
            recvPacket.Deserialize(bs);

            Console.WriteLine(msg);
            Console.WriteLine(recvPacket);
            Console.WriteLine(RSocketUtils.BytesToStr(recvPacket.GetData()));
            Console.ReadKey();


        }
    }
}
