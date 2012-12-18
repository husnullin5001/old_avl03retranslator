﻿using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TcpServer.Core.async.common;

namespace TcpServer.Core.async.block
{
    public class BlocksAcceptor : BaseConnector
    {
        private const int BACKLOG = 500;
        private const int BUFFER_SIZE = 512;

        public delegate void ConnectionAccepted(SocketAsyncEventArgs saea);
        private ConnectionAccepted connectionAccepted;

        private EventHandler<SocketAsyncEventArgs> blockAcceptEventHandler;
        private EventHandler<SocketAsyncEventArgs> receiveEventHandler;
        private EventHandler<SocketAsyncEventArgs> blockSendEventHandler;

        private Socket listenSocket;
        private string listenHost;
        private int listenPort;

        private volatile bool shutdown = false;

        private ReceivePrefixHandler receivePrefixHandler;
        private ReceiveMessageHandler receiveMessageHandler;
        private ReceiveTypeSelector receiveTypeSelector;
        private ReceiveResponseHandler receiveResponseHandler;

        private ILog log;

        public BlocksAcceptor(string listenHost, int listenPort, MessageReceived messageReceived, MessageSended messageSended,
            ConnectionAccepted connectionAccepted, ReceiveFailed receiveFailed, SendFailed sendFailed)
        {
            log = LogManager.GetLogger(typeof(BlocksAcceptor));

            this.listenHost = listenHost;
            this.listenPort = listenPort;

            this.messageReceived = messageReceived;
            this.messageSended = messageSended;
            this.connectionAccepted = connectionAccepted;
            this.receiveFailed = receiveFailed;
            this.sendFailed = sendFailed;

            receivePrefixHandler = new ReceivePrefixHandler();
            receiveMessageHandler = new ReceiveMessageHandler();
            receiveTypeSelector = new ReceiveTypeSelector();
            receiveResponseHandler = new ReceiveResponseHandler();

            blockAcceptEventHandler = new EventHandler<SocketAsyncEventArgs>(acceptEvent);
            receiveEventHandler = new EventHandler<SocketAsyncEventArgs>(receiveEvent);
            blockSendEventHandler = new EventHandler<SocketAsyncEventArgs>(sendEvent);
        }

        public void start()
        {
            var listenIPAddress = IPAddress.Parse(listenHost);
            var localEndPoint = new IPEndPoint(listenIPAddress, listenPort);

            listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(localEndPoint);
            listenSocket.Listen(BACKLOG);

            startAccept();
        }

        public void stop()
        {
            shutdown = true;

            if (listenSocket.Connected)
            {
                listenSocket.Shutdown(SocketShutdown.Both);
            }
            listenSocket.Close();
        }

        private void acceptEvent(object sender, SocketAsyncEventArgs saea)
        {
            processAccept(saea);
        }

        private void receiveEvent(object sender, SocketAsyncEventArgs saea)
        {
            processReceive(saea);
        }

        private void sendEvent(object sender, SocketAsyncEventArgs saea)
        {
            processSend(saea);
        }

        private void startAccept()
        {
            SocketAsyncEventArgs saea = new SocketAsyncEventArgs();
            saea.Completed += blockAcceptEventHandler;

            if (listenSocket != null && listenSocket.IsBound)
            {
                if (!listenSocket.AcceptAsync(saea))
                {
                    processAccept(saea);
                }
            }
        }

        private void processAccept(SocketAsyncEventArgs saea)
        {
            if (saea.SocketError == SocketError.OperationAborted && shutdown)
            {
                return;
            }
            else
            {
                startAccept();

                if (saea.SocketError == SocketError.Success)
                {
                    SocketAsyncEventArgs saeaForReceive = createSaea(receiveEventHandler, BUFFER_SIZE);
                    saeaForReceive.AcceptSocket = saea.AcceptSocket;
                    connectionAccepted(saeaForReceive);

                    saea.AcceptSocket = null;
                }
                else
                {
                    saea.AcceptSocket.Close();
                }
            }
        }

        public void startReceive(SocketAsyncEventArgs saea)
        {
            if (!saea.AcceptSocket.Connected)
            {
                receiveFailed(saea);
                closeSocket(saea);
                return;
            }
            
            var userToken = (DataHoldingUserToken)saea.UserToken;

            if (userToken.bytesDoneCountThisOp == saea.BytesTransferred)
            {
                userToken.resetVariableForNewRequest();
                try
                {
                    if (!saea.AcceptSocket.ReceiveAsync(saea))
                    {
                        processReceive(saea);
                    }
                }
                catch (Exception e)
                {
                    log.Debug("Start Receive from block failed.", e);
                    receiveFailed(saea);
                    closeSocket(saea);
                }
            }
            else
            {
                processReceive(saea);
            }
        }

        private void processReceive(SocketAsyncEventArgs saea)
        {
            var userToken = (DataHoldingUserToken)saea.UserToken;

            if (saea.SocketError != SocketError.Success || saea.BytesTransferred == 0)
            {
                log.DebugFormat("Process receive from block failed. SocketError={0} or BytesTransferred={1}.", saea.SocketError, saea.BytesTransferred);
                userToken.resetAll();
                receiveFailed(saea);
                closeSocket(saea);
                return;
            }

            receiveTypeSelector.clearFromCRLF(saea, userToken);

            int bytesToProcess = saea.BytesTransferred - userToken.bytesDoneCountThisOp;
            bytesToProcess = receivePrefixHandler.handlePrefix(saea, userToken, bytesToProcess);
            if (bytesToProcess == 0)
            {
                startReceive(saea);
                return;
            }
            else
            {
                if (userToken.dataTypeId == 0)
                {
                    receiveTypeSelector.defineTypeData(saea, userToken);
                    if (userToken.dataTypeId == 0)
                    {
                        userToken.resetAll();
                        receiveFailed(saea);
                        closeSocket(saea);
                        return;
                    }
                }

                byte[] message = null;
                int code = 0;
                if (userToken.dataTypeId == 1)
                {
                    code = receiveMessageHandler.handleMessage(saea, userToken, bytesToProcess, out message);
                }
                else if (userToken.dataTypeId == 2)
                {
                    code = receiveResponseHandler.handleResponse(saea, userToken, out message);
                }

                if (code < 0)
                {
                    userToken.resetAll();
                    receiveFailed(saea);
                    closeSocket(saea);
                    return;
                }
                else
                {
                    if (message != null)
                    {
                        userToken.resetReadyMessage();
                        messageReceived(message, saea);
                        return;
                    }
                    else
                    {
                        startReceive(saea);
                        return;
                    }
                }
            }
        }

        public void startSend(SocketAsyncEventArgs saea, byte[] bytes)
        {
            var userToken = (DataHoldingUserToken)saea.UserToken;
            userToken.resetAll();
            userToken.messageBytes = bytes;
            startSend(saea);
        }

        private void startSend(SocketAsyncEventArgs saea)
        {
            var userToken = (DataHoldingUserToken)saea.UserToken;

            Buffer.BlockCopy(userToken.messageBytes, userToken.messageBytesDoneCount, saea.Buffer,
                    saea.Offset, userToken.messageBytes.Length - userToken.messageBytesDoneCount);
            saea.SetBuffer(saea.Offset, userToken.messageBytes.Length - userToken.messageBytesDoneCount);

            if (!saea.AcceptSocket.Connected)
            {
                sendFailed(saea);
                closeSocket(saea);
                return;
            }

            try
            {
                if (!saea.AcceptSocket.SendAsync(saea))
                {
                    processSend(saea);
                }
            }
            catch (Exception e)
            {
                log.Debug("Start send to block failed.", e);
                sendFailed(saea);
                closeSocket(saea);
            }
        }

        private void processSend(SocketAsyncEventArgs saea)
        {
            var userToken = (DataHoldingUserToken)saea.UserToken;

            if (saea.SocketError == SocketError.Success)
            {
                userToken.messageBytesDoneCount += saea.BytesTransferred;

                if (userToken.messageBytesDoneCount == userToken.messageBytes.Length)
                {
                    // отправка завершена
                    userToken.resetAll();
                    messageSended(saea);
                }
                else
                {
                    startSend(saea);
                }
            }
            else
            {
                log.DebugFormat("Process send to block failed. SocketError={0}.", saea.SocketError);
                userToken.resetAll();
                sendFailed(saea);
                closeSocket(saea);
            }
        }

        public void closeSocket(SocketAsyncEventArgs saea)
        {
            log.Debug("Close socket to block.");
            try
            {
                if (saea.AcceptSocket.Connected)
                {
                    saea.AcceptSocket.Shutdown(SocketShutdown.Both);
                }
                saea.AcceptSocket.Close();
            }
            catch
            {
            }
        }

        public SocketAsyncEventArgs createSaeaForSend(Socket acceptSocket)
        {
            SocketAsyncEventArgs saea = createSaea(blockSendEventHandler, BUFFER_SIZE);
            saea.AcceptSocket = acceptSocket;
            return saea;
        }
    }
}
