﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
namespace RUDPCore
{
    public class RSocket
    {
        #region 各种回调
        public delegate void  RecvDataHandler(byte[] data);
        public delegate void MsgOverFlowHandler();

        public RecvDataHandler OnRecvData;
        public MsgOverFlowHandler OnSendQueueOverFlow();
        #endregion

        #region 属性
        public int maxSendQueue = 256;
        public int maxWaitQueue = 256;


        public int MTU = 1400;
        public int maxRTO = 100;
        public UInt32 maxBuffLeght = 1024;
        public UInt32 maxRecvQueueLength = 1024;

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
        /// 已确认的包
        /// </summary>
        private Queue<NetPacket> recvQueue;


        private UInt16 NextPacketNum
        {
            get { return (UInt16)(CurSendMsgNum + 1); }
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
                OnSendQueueOverFlow();
            }
        }

        private void PushDataToSendQueue(byte[] rawData, int piece)
        {
            int curOffset = 0;
            for (int i = 1; i <= piece && curOffset < rawData.Length; i++)
            {
                var len = Math.Min(MTU, rawData.Length - curOffset);
                var msg = PackMsg(rawData, curOffset, len, piece);
                sendQueue.Enqueue(msg);

                curOffset += len;
            }
        }

        private NetPacket PackMsg(byte[] rawData,int offset, int len, int piece)
        {
            var msgPacket = NetPacket.Create();
            msgPacket.Type = PacketType.Reliable;
            CurSendMsgNum = NextPacketNum;
            msgPacket.MsgNum = CurSendMsgNum;
            msgPacket.Legth = rawData.Length;
            msgPacket.Piece = (UInt16)piece;
            msgPacket.SetData(rawData, offset, len);
            return msgPacket;
        }

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
            int availableLen = socket.Available;
            if (availableLen <= 0)
                return;
            var buff = new byte[availableLen];
            int len = socket.ReceiveFrom(buff,0,availableLen,SocketFlags.None,ref remoteEP);
            var recvPackage = NetPacket.Create();
            recvPackage.Deserialize(buff, 0, len);
            recvQueue.Enqueue(recvPackage);
       
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
        public void Close()
        {
            isRunning = false;
            if (socket != null)
                socket.Close();
        }

    }
}