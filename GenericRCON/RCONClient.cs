using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GenericRCON {
    /// <summary>
    /// Class used to connect to RCON Server, Authenticate, and send RCON commands using the Source RCON Protocol
    /// </summary>
    public class RCONClient
    {
        private Socket _RCONSocket;
        private string _RCONPassword = "";
        private string _RCONHost = "";
        private int    _RCONPort = 27015;
        private int    _RCONID = 0;
        private bool   _authenticated = false;

        /// <summary>
        /// Getter for a Boolean value indicating if the RCON connection id currently connected and authenticated
        /// </summary>
        public bool Authenticated
        {
            get { return (_RCONSocket.Connected && _authenticated); }
        }

        /// <summary>
        /// Basic Packet Structure, consists out of Size, ID, Type and Body
        /// See https://developer.valvesoftware.com/wiki/Source_RCON_Protocol
        /// </summary>
        public struct PacketStruct
        {
            /// <summary>
            /// The packet size field is a 32-bit little endian integer, representing the length of the request in bytes. Note that the packet size field itself is not included when determining the size of the packet, so the value of this field is always 4 less than the packet's actual length. The minimum possible value for packet size is 10
            /// </summary>
            public byte[] Size; //32-bit little-endian Signed Integer
            /// <summary>
            /// The packet id field is a 32-bit little endian integer chosen by the client for each request. It may be set to any positive integer. When the server responds to the request, the response packet will have the same packet id as the original request (unless it is a failed SERVERDATA_AUTH_RESPONSE packet) It need not be unique, but if a unique packet id is assigned, it can be used to match incoming responses to their corresponding requests.
            /// </summary>
            public byte[] ID;   //32-bit little-endian Signed Integer
            /// <summary>
            /// The packet type field is a 32-bit little endian integer, which indicates the purpose of the packet. Its value will always be either 0, 2, or 3, depending on which of the following request/response types the packet represents
            /// </summary>
            public byte[] Type; //32-bit little-endian Signed Integer
            /// <summary>
            /// The packet body field is a string encoded in ASCII (i.e. ASCIIZ). Depending on the packet type, it may contain either the RCON password for the server, the command to be executed, or the server's response to a request.
            /// </summary>
            public byte[] Body; //String
        }

        /// <summary>
        /// Packet Type can be one of the following values: SERVERDATA_AUTH, SERVERDATA_AUTH_RESPONSE, SERVERDATA_EXECCOMMAND, SERVERDATA_RESPONSE_VALUE
        /// See https://developer.valvesoftware.com/wiki/Source_RCON_Protocol
        /// </summary>
        public enum PACKET_TYPE : int
        {
            /// <summary>
            /// Body --> The RCON password of the server (if this matches the server's rcon_password cvar, the auth will succeed)
            /// </summary>
            SERVERDATA_AUTH = 3,
            /// <summary>
            /// Body --> Empty string (0x00), ID --> If authentication was successful, the ID assigned by the request. If auth failed, -1 (0xFF FF FF FF)
            /// </summary>
            SERVERDATA_AUTH_RESPONSE = 2,
            /// <summary>
            /// Body --> The command to be executed on the server
            /// </summary>
            SERVERDATA_EXECCOMMAND = 2,
            /// <summary>
            /// Body --> The server's response to the original command. May be empty string (0x00) in some cases.
            /// </summary>
            SERVERDATA_RESPONSE_VALUE = 0
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="Password">RCON Password</param>
        /// <param name="Host">RCON Hostname or IP Address</param>
        /// <param name="Port">RCON Port Number</param>
        public RCONClient(string Password, string Host, int Port = 27015)
        {
            this._RCONPassword = Password;
            this._RCONHost = Host;
            this._RCONPort = Port;
            _RCONSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }  

        /// <summary>
        /// Sends a command to the RCON Server. If not connected and/or authenticated, it will try to connect and authenticate first
        /// </summary>
        /// <param name="Command">RCON Command to send to the RCON Server</param>
        /// <returns>Returns the BODY of the packet envelope</returns>
        public string SendCommand(string Command)
        {
            PacketStruct bytes;
            if (!_authenticated || !_RCONSocket.Connected)
            {
                Authenticate();
            }
            bytes = SendRaw(_RCONID++, PACKET_TYPE.SERVERDATA_EXECCOMMAND, Command);
            return System.Text.Encoding.UTF8.GetString(bytes.Body);
        }

        /// <summary>
        /// System will try to authenticate with the RCON server using the last password provided password
        /// </summary>
        /// <returns></returns>
        public bool Authenticate()
        {
            PacketStruct bytes;
            int ID = 0;
            if (_RCONSocket.Connected)  //Disconnect for each authentication (Start afresh)
            {
                _authenticated = false;
                _RCONSocket.Disconnect(true);
            }
            try
            {
                _RCONSocket.Connect(this._RCONHost, this._RCONPort);
                bytes = SendRaw(ID, PACKET_TYPE.SERVERDATA_AUTH, this._RCONPassword);
                _authenticated = (BitConverter.ToInt32(bytes.ID, 0) == ID);
            }
            catch (SocketException se)
            {
                Console.WriteLine("Failed: " + se.StackTrace.ToString());
            }
            
            return _authenticated;
        }

        /// <summary>
        /// Authenticate with the RCON server using the provided Password
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public bool Authenticate(string password)
        {
            this._RCONPassword = password;
            return Authenticate();
        }

        /// <summary>
        /// Send a request to the RCON Server by providing raw information
        /// </summary>
        /// <param name="ID">The packet id field is a 32-bit little endian integer chosen by the client for each request. It may be set to any positive integer. When the server responds to the request, the response packet will have the same packet id as the original request (unless it is a failed SERVERDATA_AUTH_RESPONSE packet) It need not be unique, but if a unique packet id is assigned, it can be used to match incoming responses to their corresponding requests.</param>
        /// <param name="Type">The packet type field is a PACKET_TYPE struct</param>
        /// <param name="Body">String containing the command, password or server response</param>
        /// <returns></returns>
        public PacketStruct SendRaw(int ID, PACKET_TYPE Type, string Body)
        {
            RCONPacket Packet_send = new RCONPacket(ID, Type, Body);
            RCONPacket Packet_receive = new RCONPacket();
            Packet_send.Send(_RCONSocket);
            Packet_receive.Receive(_RCONSocket);
            return Packet_receive.bytes;
        }
    }

    /// <summary>
    /// Class use to encapsulate the Basic Packet Structure and convertion into byte arrays. It also contains Send and Receive methods to send directly form the packet using the specified Socket
    /// </summary>
    class RCONPacket
    {           
        /// <summary>
        /// Private byte array used to store the complete packet
        /// </summary>
        byte[] Packet;
        /// <summary>
        /// Instance of the Basic Packet Structure
        /// </summary>
        public RCONClient.PacketStruct bytes;

        /// <summary>
        /// Constructor used to create a new packet and initializing values
        /// </summary>
        public RCONPacket()
        {
            this.bytes.Size = new byte[4];
            this.bytes.ID   = new byte[4];
            this.bytes.Type = new byte[4];
        }

        /// <summary>
        /// Constructor used to create a new packet and initializing values
        /// </summary>
        /// <param name="Type">The packet type field is a PACKET_TYPE struct</param>
        /// <param name="Body">String containing the command, password or server response</param>
        public RCONPacket(RCONClient.PACKET_TYPE Type, string Body)
        {

            UTF8Encoding utf8 = new UTF8Encoding();
            this.bytes.Size   = BitConverter.GetBytes(10 + Body.Length);    //4
            this.bytes.ID     = BitConverter.GetBytes(0);                   //4
            this.bytes.Type   = BitConverter.GetBytes((int)Type);           //4
            this.bytes.Body   = utf8.GetBytes(Body);                        //Body.Lengh

            this.createPacket();
        }

        /// <summary>
        /// Constructor used to create a new packet and initializing values
        /// </summary>
        /// <param name="ID">The packet id field is a 32-bit little endian integer chosen by the client for each request. It may be set to any positive integer. When the server responds to the request, the response packet will have the same packet id as the original request (unless it is a failed SERVERDATA_AUTH_RESPONSE packet) It need not be unique, but if a unique packet id is assigned, it can be used to match incoming responses to their corresponding requests.</param>
        /// <param name="Type">The packet type field is a PACKET_TYPE struct</param>
        /// <param name="Body">String containing the command, password or server response</param>
        public RCONPacket(int ID, RCONClient.PACKET_TYPE Type, string Body)
        {
            UTF8Encoding utf8 = new UTF8Encoding();
            this.bytes.Size   = BitConverter.GetBytes(10 + Body.Length);    //4
            this.bytes.ID     = BitConverter.GetBytes(ID);                  //4
            this.bytes.Type   = BitConverter.GetBytes((int)Type);           //4
            this.bytes.Body   = utf8.GetBytes(Body);                        //Body.Lengh

            this.createPacket();
        }

        /// <summary>
        /// Convert the Basic Packet Structure into a single byte array and store the result in Packet
        /// </summary>
        void createPacket()
        {
           
            byte[] packetEmpty = {(byte)0,(byte)0};                         //2

            Packet = new byte[14 + this.bytes.Body.Length];

            this.bytes.Size.CopyTo(Packet, 0);
            this.bytes.ID.CopyTo(Packet, 4);
            this.bytes.Type.CopyTo(Packet, 8);
            this.bytes.Body.CopyTo(Packet, 12);
            packetEmpty.CopyTo(Packet, 12 + this.bytes.Body.Length);
        }

        /// <summary>
        /// Overide the ToString method to convert the Basic Packet Structure in a single line String
        /// e.g. Size: Value, ID: Value, Type: Value, Body: Value
        /// </summary>
        /// <returns>Returns a string in a human readable format</returns>
        public override string ToString()
        {
            return  "Size: "   +                         BitConverter.ToInt32(this.bytes.Size, 0) +
                    ", ID: "   +                         BitConverter.ToInt32(this.bytes.ID,   0) +
                    ", Type: " + (RCONClient.PACKET_TYPE)BitConverter.ToInt32(this.bytes.Type, 0) +
                    ", Body: " +       System.Text.Encoding.Default.GetString(this.bytes.Body);
        }

        /// <summary>
        /// Send this RCONPacket via the specified Socket
        /// </summary>
        /// <param name="RCONSocket">System.Net.Sockets.Socket used to send this RCONPacket</param>
        /// <returns></returns>
        public int Send(Socket RCONSocket)
        {
            //Get rid of buffered up data (like Keep Alive) before sending a new request
            if (RCONSocket.Connected && RCONSocket.Available > 0)
            {
                RCONSocket.Receive(new byte[RCONSocket.Available]);
            }
            return RCONSocket.Send(Packet,0, Packet.Length, SocketFlags.None);
        }

        /// <summary>
        /// Recieve data anbd store it in this RCONPacket via the specified Socket
        /// </summary>
        /// <param name="RCONSocket">System.Net.Sockets.Socket used to recieve date and store in this RCONPacket</param>
        public void Receive(Socket RCONSocket)
        {
            byte[] packetEmpty = { (byte)1, (byte)1 };
            int size = 0;


            while (RCONSocket.Connected && RCONSocket.Available < 4) ;  //Waiting to get while it is still connected (better way of doing this?)
            RCONSocket.Receive(this.bytes.Size, 4, SocketFlags.None);
            size = BitConverter.ToInt32(this.bytes.Size, 0);

            while (RCONSocket.Connected && RCONSocket.Available < 4) ;
            RCONSocket.Receive(this.bytes.ID, SocketFlags.None);

            while (RCONSocket.Connected && RCONSocket.Available < 4) ;
            RCONSocket.Receive(this.bytes.Type, SocketFlags.None);

            size = size - 8;                        //Subtract the 4 'ID' bytes and the 4 'Type' Bytes
            this.bytes.Body = new byte[size - 2];   //Subtract the 2 (0x00) characters at the end of the packet

            //Might be to big for buffer. No errors recieved yet, but this might have issues
            byte[] buffer = new byte[size];
            while (RCONSocket.Connected && RCONSocket.Available < size);  
            RCONSocket.Receive(buffer, size, SocketFlags.None);

            Array.Copy(buffer, 0, this.bytes.Body, 0, size - 2);
        }

    }
}
