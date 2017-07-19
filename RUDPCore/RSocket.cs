using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
namespace RUDPCore
{
    public class RSocket
    {
        #region 各种回调
        public enum QueuOverflowType
        {
            SendQueue,RecvBuff,
        }
        public delegate void  RecvDataHandler(byte[] data);
        public delegate void MsgOverFlowHandler(QueuOverflowType type);
        public delegate void RecvException(Exception e);

        public RecvDataHandler OnRecvData;
        public MsgOverFlowHandler OnQueueOverFlow;
        public RecvException OnReceptionError;
        public RecvDataHandler OnRecvIllegalData;
        #endregion

        #region 属性
        public const UInt16 SESSION = 1024;
        public const byte PROTOCOL = 128;
        public const int CRC_OFFSET = 3;

        public int maxSendQueue = 256;
        public int maxWaitQueue = 256;

        public int MTU = 1400;
        public int maxRTO = 100;
        public int maxRecvBuffLeght = 1024;
        public int maxRecvQueueLength = 1024;

        private Socket socket;
        private EndPoint remoteEP;
        private IPEndPoint localEP;
        private bool isRunning;

        /// <summary>
        /// 当前发送成功最大帧号
        /// </summary>
        private UInt16 CurSendMsgNum;
        /// <summary>
        /// 等待发帧队列
        /// </summary>
        private Queue<NetPacket> sendQueue;
        /// <summary>
        /// 已发送但未确认队列
        /// </summary>
        private Queue<NetPacket> waitAckQueue;

        
        /// <summary>
        /// 当前收到包的最大帧号
        /// </summary>
        private UInt16 CurRecvNum;
        /// <summary>
        /// 收包缓存，其中包不保证顺序
        /// </summary>
        private List<NetPacket> recvBuff;
        /// <summary>
        /// 分片包队列（等待合包）
        /// TODO : 也许可以用优先队列优化
        /// </summary>
        private List<NetPacket> recvPiecesBuff;
        /// <summary>
        /// 已确认的包
        /// </summary>
        private Queue<NetPacket> recvQueue;


        private UInt16 NextPacketNum
        {
            get { return (UInt16)(CurSendMsgNum + 1); }
        }
        private UInt16 DesireNextMsgNum
        {
            get { return (UInt16)(CurRecvNum + 1); }
        }
        #endregion

        #region 初始化
        public RSocket(string ip, int port)
        {
            remoteEP = new IPEndPoint(IPAddress.Parse(ip), port);
            InitSocket();
        }

        public RSocket(int port)
        {
            localEP = new IPEndPoint(IPAddress.Any, port);
            remoteEP = new IPEndPoint(IPAddress.Any, 0);
            InitSocket();
            socket.Bind(localEP);
        }

        private void InitSocket()
        {
            isRunning = true;
            CurSendMsgNum = 0;
            sendQueue = new Queue<NetPacket>();
            recvQueue = new Queue<NetPacket>();
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            socket.Blocking = false;
        }
        #endregion

        #region 可靠UDP
        public void Send(byte[] data, PacketType msgType = PacketType.Reliable)
        {
            if (!isRunning || data == null || data.Length <= 0)
                return;
            switch(msgType)
            {
                case PacketType.Reliable:
                    SendRaliableData(data);
                    break;
                case PacketType.Unreliable:
                    SendData(data);
                    break;
            }
        }

        private void SendRaliableData(byte[] rawData)
        {
            //数据过大时采用分片处理，防止过大的包丢失重传整包问题
            int piece = (rawData.Length-1) / MTU + 1;
            //尝试进入发送队列
            if (sendQueue.Count + piece < maxSendQueue)
            {
                PushDataToSendQueue(rawData, piece);
                return;
            }
            else
            {
                //发送队列溢出
                OnQueueOverFlow(QueuOverflowType.SendQueue);
            }
        }

        private void PushDataToSendQueue(byte[] rawData, int piece)
        {
            if(piece <= 1)
            {
                var msg = PackMsg(PacketType.Reliable, rawData, 0, rawData.Length, 0);
                sendQueue.Enqueue(msg);
                return;
            }
            int curOffset = 0;
            for (int i = 1; i <= piece && curOffset < rawData.Length; i++)
            {
                var len = Math.Min(MTU, rawData.Length - curOffset);
                var msg = PackMsg(PacketType.Reliable, rawData, curOffset, len, piece - i + 1);
                sendQueue.Enqueue(msg);
                curOffset += len;
            }
        }
        /// <summary>
        /// Piece == 0 : 不需要分片的包
        /// Piece >= 1 : 分片包中处于第几个分片,其中1表示最后一个分片
        /// </summary>
        private NetPacket PackMsg(PacketType type, byte[] rawData,
            int offset, int len, int piece)
        {
            var msgPacket = NetPacket.Create();
            msgPacket.Type = type;
            CurSendMsgNum = NextPacketNum;
            msgPacket.MsgNum = CurSendMsgNum;
            msgPacket.Legth = (UInt16)rawData.Length;
            msgPacket.Piece = (UInt16)piece;
            msgPacket.Session = SESSION;
            msgPacket.ACK = DesireNextMsgNum;
            msgPacket.SetData(rawData, offset, len);
            return msgPacket;
        }
        #endregion

        public void Tick()
        {
            if (!isRunning || socket == null)
                return;
            if (sendQueue.Count > 0)
                HandleSend();
            TryRecvData();
            if (recvQueue.Count > 0)
                HandleRecvQueue();
        }

     

        #region 发送
        private void HandleSend()
        {
            if (sendQueue.Count <= 0)
                return;
            while (sendQueue.Count > 0)
            {
                var msg = sendQueue.Dequeue();
                var data = msg.Serialize();
                SendData(data);
                waitAckQueue.Enqueue(msg);
            }
        }

        private void SendData(byte[] data)
        {
            socket.SendTo(data, remoteEP);
        }

        #endregion

        #region 接收
        private void TryRecvData()
        {
            try
            {
                int availableLen = socket.Available;
                if (availableLen <= 0)
                    return;
                var buff = new byte[availableLen];
                int len = socket.ReceiveFrom(buff, 0, availableLen, SocketFlags.None, ref remoteEP);
                HandleRecvData(buff, 0, len);
            }
            catch(Exception e)
            {
                //All Error and Exception will trigger this callback func
                OnReceptionError(e);
            }
    
        }

        private void HandleRecvData(byte[] buff, int offset, int len)
        {
            if (!CheckMsg(buff, offset, len))
            {
                if (OnRecvIllegalData != null)
                    OnRecvIllegalData(buff);
                return;
            }
            var recvPackage = NetPacket.Create();
            recvPackage.Deserialize(buff, 0, len);
            switch (recvPackage.Type)
            {
                case PacketType.AckMsg:
                    RecvAckMsg(recvPackage);
                    break;
                case PacketType.Reliable:
                    RecvReliableMsg(recvPackage);
                    break;
                case PacketType.Unreliable:
                    RecvUnReliableMsg(recvPackage);
                    break;
                default:
                    RecvUnknowMsg(recvPackage);
                    break;
            }
        }

        private bool CheckMsg(byte[] data, int offset, int len)
        {
            //Protocol Check
            var br = new BinaryReader(new MemoryStream(data, offset, len - offset));
            byte protocol = br.ReadByte();
            bool isProtocol = PROTOCOL == protocol;
            if (!isProtocol)
                return isProtocol;

            //CRC Check
            int crcOffset = offset + CRC_OFFSET;
            UInt16 _crc = RSocketUtils.CalcCrc16(data, crcOffset, len - CRC_OFFSET);
            var msgCRC = br.ReadUInt16();
            bool isCRC = msgCRC == _crc;

            //Repeat Check


            return isCRC;
        }

        private void RecvAckMsg(NetPacket msg)
        {
            if (msg == null || msg.Type != PacketType.AckMsg)
                return;
            int ackNum = msg.ACK;
            while(waitAckQueue.Peek().MsgNum < ackNum)
            {
                var packet = waitAckQueue.Dequeue();
                packet.GC();
            }
            msg.GC();
        }
        /// <summary>
        /// recv reliable msg
        /// 1.判断分片
        /// 2.处理不需要分片的包
        /// 3.处理合包
        /// 4.处理收包队列
        /// </summary>
        private void RecvReliableMsg(NetPacket msg)
        {
            if (msg == null || msg.Type != PacketType.Reliable)
                return;
            if(msg.Piece <= 0) //收到单个包
            {
                RecvSingleReliablePacket(msg);
            }
            else // 分片的包
            {
                TryRecvPiecePacket(msg);
            }
        }


        private void RecvSingleReliablePacket(NetPacket msg)
        {
            if(isExpectRecvMsgNum(msg.MsgNum)) // 正确的顺序包
            {
                CurRecvNum++;
                recvQueue.Enqueue(msg);
                CheckBuffQueue();   //检查收包缓冲是否有期望的包
            }
            else // 乱序包
            {
                if (recvBuff.Count < maxRecvBuffLeght)
                    recvBuff.Add(msg);
                else
                    OnQueueOverFlow(QueuOverflowType.RecvBuff);
            }
        }

        private void TryRecvPiecePacket(NetPacket msg)
        {
            if (isExpectRecvMsgNum(msg.MsgNum))
            {
                recvPiecesBuff.Add(msg);
                TryMergePiecePacket();
            }
            else
            {
                //乱序分片包
                recvPiecesBuff.Add(msg);
            }
        }

        private void TryMergePiecePacket()
        {
            
        }

    

        private void CheckBuffQueue()
        {
            int index = -1;
            for (int i = 0; i < recvBuff.Count; i++)
            {
                if (isExpectRecvMsgNum(recvBuff[i].MsgNum))
                {
                    index = i;
                    break;
                }
            }
            if (index != -1)
            {
                CurRecvNum++;
                recvQueue.Enqueue(recvBuff[index]);
                RSocketUtils.ListRemoveAt<NetPacket>(recvBuff, index);
                CheckBuffQueue();
            }
        }

        private bool isExpectRecvMsgNum(UInt16 seq)
        {
            return Math.Abs(seq - CurRecvNum) % UInt16.MaxValue == 1;
        }


        //Recv common UDP packet,no need send ack msg
        private void RecvUnReliableMsg(NetPacket msg)
        {
            if (msg == null || msg.Type != PacketType.Unreliable)
                return;
            OnRecvData(msg.GetData());
            msg.GC();
        }
        private void RecvUnknowMsg(NetPacket msg)
        {
            throw new RSocketException(msg.ToString());
        }

        private void HandleRecvQueue()
        {
            while(recvQueue.Count > 0)
            {
                var msg = recvQueue.Dequeue();
                var data = msg.GetData();
                if (OnRecvData != null)
                {
                    OnRecvData(data);
                }
            }
        }

        #endregion

        #region 关闭
        public void Close()
        {
            isRunning = false;
            if (socket != null)
                socket.Close();
        }
        #endregion

    }
}
