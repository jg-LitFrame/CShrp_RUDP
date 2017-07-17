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
        const UInt16 polynomial = 0xA001;
        UInt16[] table = new UInt16[256];
        public UInt16 ComputeChecksum(byte[] bytes)
        {
            return ComputeChecksum(bytes, 0, bytes.Length);
        }
        public UInt16 ComputeChecksum(byte[] bytes,int offset, int len)
        {
            UInt16 crc = 0;
            for (int i = offset; i < len + offset; ++i)
            {
                byte index = (byte)(crc ^ bytes[i]);
                crc = (UInt16)((crc >> 8) ^ table[index]);
            }
            return crc;
        }
        public byte[] ComputeChecksumBytes(byte[] bytes)
        {
            UInt16 crc = ComputeChecksum(bytes);
            return BitConverter.GetBytes(crc);
        }

        public Crc16()
        {
            UInt16 value;
            UInt16 temp;
            for (UInt16 i = 0; i < table.Length; ++i)
            {
                value = 0;
                temp = i;
                for (byte j = 0; j < 8; ++j)
                {
                    if (((value ^ temp) & 0x0001) != 0)
                    {
                        value = (UInt16)((value >> 1) ^ polynomial);
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
        public static UInt16 CalcCrc16(byte[] bytes)
        {
            return crc16Tools.ComputeChecksum(bytes);
        }
        public static UInt16 CalcCrc16(byte[] bytes, int offset,int len)
        {
            return crc16Tools.ComputeChecksum(bytes,offset,len);
        }
        #endregion

        public static T[] CreateArray<T>(int len)
        {
            var arr = new T[len];
            return arr;
        }


        public static byte[] StrToBytes(string info)
        {
            return System.Text.Encoding.Default.GetBytes(info);
        }
        public static string BytesToStr(byte[] bs)
        {
            return System.Text.Encoding.Default.GetString(bs);
        }
    }
}
