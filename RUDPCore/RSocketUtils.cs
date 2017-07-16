using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RUDPCore
{
    #region CRC16
    public class Crc16
    {
        const ushort polynomial = 0xA001;
        ushort[] table = new ushort[256];
        public ushort ComputeChecksum(byte[] bytes)
        {
            return ComputeChecksum(bytes, 0, bytes.Length);
        }
        public ushort ComputeChecksum(byte[] bytes,int offset, int len)
        {
            ushort crc = 0;
            for (int i = offset; i < len + offset; ++i)
            {
                byte index = (byte)(crc ^ bytes[i]);
                crc = (ushort)((crc >> 8) ^ table[index]);
            }
            return crc;
        }
        public byte[] ComputeChecksumBytes(byte[] bytes)
        {
            ushort crc = ComputeChecksum(bytes);
            return BitConverter.GetBytes(crc);
        }

        public Crc16()
        {
            ushort value;
            ushort temp;
            for (ushort i = 0; i < table.Length; ++i)
            {
                value = 0;
                temp = i;
                for (byte j = 0; j < 8; ++j)
                {
                    if (((value ^ temp) & 0x0001) != 0)
                    {
                        value = (ushort)((value >> 1) ^ polynomial);
                    }
                    else
                    {
                        value >>= 1;
                    }
                    temp >>= 1;
                }
                table[i] = value;
            }
        }
    }
    #endregion
    public static class RSocketUtils
    {
        #region CRC16
        private static Crc16 crc16Tools = new Crc16();
        public static ushort CalcCrc16(byte[] bytes)
        {
            return crc16Tools.ComputeChecksum(bytes);
        }
        public static ushort CalcCrc16(byte[] bytes, int offset,int len)
        {
            return crc16Tools.ComputeChecksum(bytes,offset,len);
        }
        #endregion
    }
}
