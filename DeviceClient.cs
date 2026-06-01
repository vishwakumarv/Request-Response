using System;
using System.Linq;
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

        private DeviceResponse BuildExceptionResponse(ModbusFunction function,
                                                       byte exceptionCode,
                                                       ModbusPacket request)
        {
            byte fc = (byte)function;
            byte errorFc = (byte)(fc | 0x80);
            ushort tid = NextTransactionId();

            byte[] resPdu = { errorFc, exceptionCode };
            byte[] resFull = BuildTcpFrame(resPdu, tid);
            var res = MakePacket(resFull, errorFc, function,
                                 startAddr: 0, qty: 0,
                                 byteCount: 0,
                                 dataBytes: new byte[] { exceptionCode },
                                 isResponse: true);

            return new DeviceResponse($"ERROR: Exception 0x{exceptionCode:X2}")
            {
                Request = request,
                Response = res
            };
        }

        // ── SendAsync ─────────────────────────────────────────────────────────
        // startAddress and quantity are only used by FC03/FC04
        public async Task<DeviceResponse> SendAsync(ModbusFunction function,
                                                     ushort startAddress = 0,
                                                     ushort quantity = 2)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return function switch
                    {
                        ModbusFunction.FC03_ReadHoldingRegisters or
                        ModbusFunction.FC04_ReadInputRegisters
                            => BuildReadRegisterResponse(function, startAddress, quantity),

                        ModbusFunction.FC06_WriteSingleRegister
                            => BuildWriteSingleRegisterResponse(),

                        ModbusFunction.FC16_WriteMultipleRegisters
                            => BuildWriteMultipleRegistersResponse(),

                        _ => new DeviceResponse($"ERROR: Unsupported function code 0x{(byte)function:X2}")
                    };
                }
                catch (Exception ex)
                {
                    return new DeviceResponse($"ERROR: {ex.Message}");
                }
            });
        }

        // ── FC03 / FC04 – Read Holding / Input Registers ─────────────────────
        private DeviceResponse BuildReadRegisterResponse(ModbusFunction function,
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

            // Build simulated response: each register = 0x0000 by default
            byte byteCount = (byte)(quantity * 2);
            byte[] regData = new byte[byteCount]; // all zeros simulation
            ushort resTid = NextTransactionId();

            byte[] resPdu = new byte[2 + byteCount];
            resPdu[0] = fc;
            resPdu[1] = byteCount;
            Array.Copy(regData, 0, resPdu, 2, byteCount);

            byte[] resFull = BuildTcpFrame(resPdu, resTid);
            var res = MakePacket(resFull, fc, function,
                                 startAddress, quantity,
                                 byteCount, dataBytes: regData,
                                 isResponse: true);

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        // ── FC06 – Write Single Register ─────────────────────────────────────
        private DeviceResponse BuildWriteSingleRegisterResponse()
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

            byte[] resFull = BuildTcpFrame(reqPdu, tid);
            var res = MakePacket(resFull, fc, ModbusFunction.FC06_WriteSingleRegister,
                                 startAddr, qty: 0,
                                 byteCount: 0, dataBytes: new byte[] { valHi, valLo },
                                 isResponse: true);

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        // ── FC16 – Write Multiple Registers ──────────────────────────────────
        private DeviceResponse BuildWriteMultipleRegistersResponse()
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

            ushort resTid = NextTransactionId();
            byte[] resPdu = { fc, addrHi, addrLo, qtyHi, qtyLo };
            byte[] resFull = BuildTcpFrame(resPdu, resTid);
            var res = MakePacket(resFull, fc, ModbusFunction.FC16_WriteMultipleRegisters,
                                 startAddr, qty,
                                 byteCount: 0, dataBytes: null,
                                 isResponse: true);

            return new DeviceResponse("OK") { Request = req, Response = res };
        }
    }
}
