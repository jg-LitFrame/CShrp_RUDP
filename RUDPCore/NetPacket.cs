using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RUDPCore
{
    public enum PacketType : byte
    {
        Unreliable = 0,
        Reliable = 1,
    }

    public class NetPacket
    {
        public byte protcol;
        public ushort CRC;
        public UInt32 session;
        public PacketType Type;
        public UInt16 MsgNum;
        public UInt16 Piece;
        public int Legth;

        private byte[] rawData;
        private byte[] data;
        private int offset;
        private int len;
        public void SetData(byte[] data)
        {
            SetData(data, 0, data.Length);
        }
        public void SetData(byte[] data, int offset, int len)
        {
            rawData = data;
            this.offset = offset;
            this.len = len;
            data = null;
        }
        public byte[] GetData()
        {
            if (data != null)
                return data;
            data = new byte[len];
            Array.Copy(rawData, offset, data, 0, len);
            rawData = null;
            return data;
        }






        public static NetPacket Create()
        {
            return new NetPacket();
        }

        public byte[] Serialize()
        {
            return rawData;
        }

        public void Deserialize(byte[] data)
        {

        }

        public void Deserialize(byte[] buff, int offset, int len)
        {
            rawData = buff;
        }
    }
}
