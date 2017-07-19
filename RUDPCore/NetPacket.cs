using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RUDPCore
{
    public enum PacketType : byte
    {
        Unreliable = 0,
        Reliable = 1,
        AckMsg = 2,
    }

    /**
     *      MsgHeader  
     *      | protocol(1) | CRC(2) | session(2) | Type(1) | MsgNum(2) | ACK(2) | Piece(2) | Length(2) |
     */

    public class NetPacket
    {
        public const int RUDP_HEADER_LENGTH = 14;

        public byte Protcol;
        public UInt16 CRC;
        public UInt16 Session;
        public PacketType Type;
        public UInt16 MsgNum;
        public UInt16 ACK;
        public UInt16 Piece;
        public UInt16 Legth;

        //为避免重复的内存gc，多个NetPacket可能共享一个byte[]
        private byte[] data;
        private byte[] rawData;
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
            this.len = (UInt16)len;
            this.Legth = (UInt16)len;
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
        public override string ToString()
        {
            return string.Format("Protcol = {0}, CRC = {1}, Session = {2}, Type = {3}, MsgNum = {4}, ACK = {5}, Piece = {6}, Legth = {7}",
                Protcol, CRC, Session, Type, MsgNum,ACK, Piece, Legth);
        }
   

        public void Append(NetPacket nextPacket)
        {

        }

        #region 序列化相关操作
        public byte[] Serialize()
        {
            int totalLen = Legth + RUDP_HEADER_LENGTH;
            var bs = RSocketUtils.CreateArray<byte>(totalLen);
            Stream stream = new MemoryStream(bs);
            var bW = new BinaryWriter(stream);
            bW.Seek(0, SeekOrigin.Begin);

            bW.Write(Protcol);
            bW.Write(CRC);
            bW.Write(Session);
            bW.Write((byte)Type);
            bW.Write(MsgNum);
            bW.Write(ACK);
            bW.Write(Piece);
            bW.Write(Legth);
            bW.Write(rawData, offset, len);

            CRC = RSocketUtils.CalcCrc16(bs, 3, totalLen - 3);
            bW.Seek(1,SeekOrigin.Begin);
            bW.Write(CRC);

            bW.Close();
            return bs;
        }

        public void Deserialize(byte[] data)
        {
            Deserialize(data, 0, data.Length);
        }

        public void Deserialize(byte[] buff, int offset, int len)
        {
            Stream stream = new MemoryStream(buff,0,buff.Length);
            var br = new BinaryReader(stream);
            Protcol = br.ReadByte();
            CRC = br.ReadUInt16();
            Session = br.ReadUInt16();
            Type = (PacketType)br.ReadByte();
            MsgNum = br.ReadUInt16();
            ACK = br.ReadUInt16();
            Piece = br.ReadUInt16();
            Legth = br.ReadUInt16();
            br.ReadUInt16();
            data = br.ReadBytes(Legth);

            br.Close();
        }
        #endregion

        public static NetPacket Create()
        {
            return new NetPacket();
        }

        internal void GC()
        {
            throw new NotImplementedException();
        }
    }
}
