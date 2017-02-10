﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SSHServer
{
    public class Client
    {
        private Socket m_Socket;
        private ILogger m_Logger;

        private bool m_HasCompletedProtocolVersionExchange = false;
        private string m_ProtocolVersionExchange;

        // We are considered connected if we have a valid socket object
        public bool IsConnected { get { return m_Socket != null; } }

        public Client(Socket socket, ILogger logger)
        {
            m_Socket = socket;
            m_Logger = logger;

            m_Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            m_Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);

            // 4.2.Protocol Version Exchange - https://tools.ietf.org/html/rfc4253#section-4.2
            Send($"{Server.ProtocolVersionExchange}\r\n");
        }

        public void Poll()
        {
            if (!IsConnected)
                return;

            bool dataAvailable = m_Socket.Poll(0, SelectMode.SelectRead);
            if (dataAvailable)
            {
                int read = m_Socket.Available;
                if (read < 1)
                {
                    Disconnect();
                    return;
                }

                if (!m_HasCompletedProtocolVersionExchange)
                {
                    // Wait for CRLF
                    try
                    {
                        ReadProtocolVersionExchange();
                        if (m_HasCompletedProtocolVersionExchange)
                        {
                            // TODO: Consider processing Protocol Version Exchange for validity
                            m_Logger.LogDebug($"Received ProtocolVersionExchange: {m_ProtocolVersionExchange}");
                        }
                    }
                    catch (Exception)
                    {
                        Disconnect();
                        return;
                    }
                }

                if (m_HasCompletedProtocolVersionExchange)
                {
                    // TODO: Read and process packets
                }
            }
        }

        public void Disconnect()
        {
            m_Logger.LogDebug($"Disconnected");
            if (m_Socket != null)
            {
                try
                {
                    m_Socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception) { }

                m_Socket = null;
            }
        }

        private void Send(string message)
        {
            m_Logger.LogDebug($"Sending raw string: {message.Trim()}");
            Send(Encoding.UTF8.GetBytes(message));
        }

        private void Send(byte[] data)
        {
            if (!IsConnected)
                return;

            m_Socket.Send(data);
        }

        // Read 1 byte from the socket until we find "\r\n"
        private void ReadProtocolVersionExchange()
        {
            NetworkStream stream = new NetworkStream(m_Socket, false);
            string result = null;

            List<byte> data = new List<byte>();

            bool foundCR = false;
            int value = stream.ReadByte();
            while (value != -1)
            {
                if (foundCR && (value == '\n'))
                {
                    // DONE
                    result = Encoding.UTF8.GetString(data.ToArray());
                    m_HasCompletedProtocolVersionExchange = true;
                    break;
                }

                if (value == '\r')
                    foundCR = true;
                else
                {
                    foundCR = false;
                    data.Add((byte)value);
                }

                value = stream.ReadByte();
            }

            m_ProtocolVersionExchange += result;
        }
    }
}