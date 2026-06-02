using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Edj20Tester.Models;

namespace Edj20Tester
{
    public class ModbusPacket
    {
        public byte[] RawBytes { get; set; }
        public ushort TransactionId { get; set; }
        public ushort ProtocolId { get; set; }
        public ushort Length { get; set; }
        public byte UnitId { get; set; }
        public byte FunctionCode { get; set; }
        public ushort StartAddress { get; set; }
        public ushort Quantity { get; set; }
        public byte[] DataBytes { get; set; }
        public bool IsResponse { get; set; }
        public byte ByteCount { get; set; }
        public ModbusFunction Function { get; set; }
    }

    public class DeviceResponse
    {
        public string Raw { get; }
        public ModbusPacket Request { get; set; }
        public ModbusPacket Response { get; set; }
        public bool IsError => Raw.StartsWith("ERROR");
        public DeviceResponse(string raw) => Raw = raw;
    }

    public class DeviceClient
    {
        private const byte UnitId = 0x01;
        private int _transactionId = 0;

        // TCP connection supplied by MainWindow after connect
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 502;

        private byte[] BuildTcpFrame(byte[] pdu, ushort tid)
        {
            ushort length = (ushort)(1 + pdu.Length);
            byte[] mbap = new byte[]
            {
                (byte)(tid    >> 8), (byte)(tid    & 0xFF),
                0x00,                 0x00,
                (byte)(length >> 8), (byte)(length & 0xFF),
                UnitId
            };
            return mbap.Concat(pdu).ToArray();
        }

        private ushort NextTransactionId()
        {
            return (ushort)(Interlocked.Increment(ref _transactionId) & 0xFFFF);
        }

        private ModbusPacket MakePacket(byte[] fullFrame, byte fc, ModbusFunction fn,
                                        ushort startAddr, ushort qty,
                                        byte byteCount, byte[] dataBytes, bool isResponse)
        {
            return new ModbusPacket
            {
                RawBytes = fullFrame,
                TransactionId = (ushort)((fullFrame[0] << 8) | fullFrame[1]),
                ProtocolId = 0,
                Length = (ushort)((fullFrame[4] << 8) | fullFrame[5]),
                UnitId = UnitId,
                FunctionCode = fc,
                Function = fn,
                StartAddress = startAddr,
                Quantity = qty,
                ByteCount = byteCount,
                DataBytes = dataBytes,
                IsResponse = isResponse
            };
        }

        // ── Send raw frame over TCP and read response ─────────────────────────
        private async Task<byte[]> SendAndReceiveAsync(byte[] requestFrame)
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IpAddress, Port);
            tcp.ReceiveTimeout = 3000;
            tcp.SendTimeout = 3000;

            var stream = tcp.GetStream();

            // Send request
            await stream.WriteAsync(requestFrame, 0, requestFrame.Length);

            // Read MBAP header first (7 bytes) to know total length
            byte[] header = new byte[7];
            int read = 0;
            while (read < 7)
            {
                int n = await stream.ReadAsync(header, read, 7 - read);
                if (n == 0) throw new Exception("Connection closed by device.");
                read += n;
            }

            // Bytes 4-5 of MBAP = length field (covers unit id + PDU)
            int pduLen = (header[4] << 8) | header[5];

            // Read remaining PDU bytes (pduLen - 1 because unit id already in header)
            byte[] pdu = new byte[pduLen - 1];
            read = 0;
            while (read < pdu.Length)
            {
                int n = await stream.ReadAsync(pdu, read, pdu.Length - read);
                if (n == 0) throw new Exception("Connection closed by device.");
                read += n;
            }

            return header.Concat(pdu).ToArray();
        }

        // ── SendAsync ─────────────────────────────────────────────────────────
        public async Task<DeviceResponse> SendAsync(ModbusFunction function,
                                                     ushort startAddress = 0,
                                                     ushort quantity = 1)
        {
            try
            {
                return function switch
                {
                    ModbusFunction.FC03_ReadHoldingRegisters or
                    ModbusFunction.FC04_ReadInputRegisters
                        => await BuildReadRegisterResponse(function, startAddress, quantity),

                    ModbusFunction.FC06_WriteSingleRegister
                        => await BuildWriteSingleRegisterResponse(),

                    ModbusFunction.FC16_WriteMultipleRegisters
                        => await BuildWriteMultipleRegistersResponse(),

                    _ => new DeviceResponse($"ERROR: Unsupported function code 0x{(byte)function:X2}")
                };
            }
            catch (Exception ex)
            {
                return new DeviceResponse($"ERROR: {ex.Message}");
            }
        }

        // ── FC03 / FC04 – Read Holding / Input Registers ──────────────────────
        private async Task<DeviceResponse> BuildReadRegisterResponse(ModbusFunction function,
                                                                      ushort startAddress,
                                                                      ushort quantity)
        {
            byte fc = (byte)function;
            byte addrHi = (byte)(startAddress >> 8);
            byte addrLo = (byte)(startAddress & 0xFF);
            byte qtyHi = (byte)(quantity >> 8);
            byte qtyLo = (byte)(quantity & 0xFF);
            ushort reqTid = NextTransactionId();

            byte[] reqPdu = { fc, addrHi, addrLo, qtyHi, qtyLo };
            byte[] reqFull = BuildTcpFrame(reqPdu, reqTid);
            var req = MakePacket(reqFull, fc, function,
                                 startAddress, quantity,
                                 byteCount: 0, dataBytes: null,
                                 isResponse: false);

            // Send to real device and receive response
            byte[] resFull = await SendAndReceiveAsync(reqFull);

            // Parse response
            // resFull: [TID Hi][TID Lo][Proto Hi][Proto Lo][Len Hi][Len Lo][UnitId][FC][ByteCount][Data...]
            if (resFull.Length < 9)
                return new DeviceResponse("ERROR: Response too short") { Request = req };

            byte resFc = resFull[7];

            // Check for Modbus exception (FC | 0x80)
            if ((resFc & 0x80) != 0)
            {
                byte exCode = resFull[8];
                return new DeviceResponse($"ERROR: Modbus Exception 0x{exCode:X2}")
                {
                    Request = req,
                    Response = MakePacket(resFull, resFc, function,
                                          startAddress, quantity,
                                          byteCount: 0,
                                          dataBytes: new byte[] { exCode },
                                          isResponse: true)
                };
            }

            byte byteCount = resFull[8];
            byte[] regData = new byte[byteCount];
            Array.Copy(resFull, 9, regData, 0, Math.Min(byteCount, resFull.Length - 9));

            var res = MakePacket(resFull, resFc, function,
                                 startAddress, quantity,
                                 byteCount, dataBytes: regData,
                                 isResponse: true);

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        // ── FC06 – Write Single Register ──────────────────────────────────────
        private async Task<DeviceResponse> BuildWriteSingleRegisterResponse()
        {
            const byte fc = (byte)ModbusFunction.FC06_WriteSingleRegister;
            byte addrHi = 0x00; byte addrLo = 0x01;
            byte valHi = 0x00; byte valLo = 0x03;
            ushort tid = NextTransactionId();

            byte[] reqPdu = { fc, addrHi, addrLo, valHi, valLo };
            byte[] reqFull = BuildTcpFrame(reqPdu, tid);
            ushort startAddr = (ushort)((addrHi << 8) | addrLo);
            var req = MakePacket(reqFull, fc, ModbusFunction.FC06_WriteSingleRegister,
                                 startAddr, qty: 0,
                                 byteCount: 0, dataBytes: new byte[] { valHi, valLo },
                                 isResponse: false);

            byte[] resFull = await SendAndReceiveAsync(reqFull);

            if (resFull.Length < 9)
                return new DeviceResponse("ERROR: Response too short") { Request = req };

            byte resFc = resFull[7];
            if ((resFc & 0x80) != 0)
            {
                byte exCode = resFull[8];
                return new DeviceResponse($"ERROR: Modbus Exception 0x{exCode:X2}")
                {
                    Request = req,
                    Response = MakePacket(resFull, resFc, ModbusFunction.FC06_WriteSingleRegister,
                                          startAddr, qty: 0,
                                          byteCount: 0,
                                          dataBytes: new byte[] { exCode },
                                          isResponse: true)
                };
            }

            byte resAddrHi = resFull[8];
            byte resAddrLo = resFull[9];
            byte resValHi = resFull[10];
            byte resValLo = resFull[11];

            var res = MakePacket(resFull, resFc, ModbusFunction.FC06_WriteSingleRegister,
                                 startAddr, qty: 0,
                                 byteCount: 0,
                                 dataBytes: new byte[] { resValHi, resValLo },
                                 isResponse: true);

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        // ── FC16 – Write Multiple Registers ───────────────────────────────────
        private async Task<DeviceResponse> BuildWriteMultipleRegistersResponse()
        {
            const byte fc = (byte)ModbusFunction.FC16_WriteMultipleRegisters;
            byte addrHi = 0x00; byte addrLo = 0x00;
            byte qtyHi = 0x00; byte qtyLo = 0x02;
            byte byteCount = 0x04;
            byte r1Hi = 0x00; byte r1Lo = 0x0A;
            byte r2Hi = 0x01; byte r2Lo = 0x02;
            ushort reqTid = NextTransactionId();

            byte[] reqPdu = { fc, addrHi, addrLo, qtyHi, qtyLo, byteCount, r1Hi, r1Lo, r2Hi, r2Lo };
            byte[] reqFull = BuildTcpFrame(reqPdu, reqTid);
            ushort startAddr = (ushort)((addrHi << 8) | addrLo);
            ushort qty = (ushort)((qtyHi << 8) | qtyLo);
            var req = MakePacket(reqFull, fc, ModbusFunction.FC16_WriteMultipleRegisters,
                                 startAddr, qty,
                                 byteCount, dataBytes: new byte[] { r1Hi, r1Lo, r2Hi, r2Lo },
                                 isResponse: false);

            byte[] resFull = await SendAndReceiveAsync(reqFull);

            if (resFull.Length < 9)
                return new DeviceResponse("ERROR: Response too short") { Request = req };

            byte resFc = resFull[7];
            if ((resFc & 0x80) != 0)
            {
                byte exCode = resFull[8];
                return new DeviceResponse($"ERROR: Modbus Exception 0x{exCode:X2}")
                {
                    Request = req,
                    Response = MakePacket(resFull, resFc, ModbusFunction.FC16_WriteMultipleRegisters,
                                          startAddr, qty,
                                          byteCount: 0,
                                          dataBytes: new byte[] { exCode },
                                          isResponse: true)
                };
            }

            var res = MakePacket(resFull, resFc, ModbusFunction.FC16_WriteMultipleRegisters,
                                 startAddr, qty,
                                 byteCount: 0, dataBytes: null,
                                 isResponse: true);

            return new DeviceResponse("OK") { Request = req, Response = res };
        }
    }
}
