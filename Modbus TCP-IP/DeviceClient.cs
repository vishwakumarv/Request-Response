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

        // ── MBAP Header builder ───────────────────────────────────────────────
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

        // ── Thread-safe transaction ID ────────────────────────────────────────
        private ushort NextTransactionId()
        {
            return (ushort)(Interlocked.Increment(ref _transactionId) & 0xFFFF);
        }

        // ── Packet builder helper ─────────────────────────────────────────────
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

        // ── Modbus exception response builder ─────────────────────────────────
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
        public async Task<DeviceResponse> SendAsync(ModbusFunction function)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return function switch
                    {
                        ModbusFunction.FC03_ReadHoldingRegisters or
                        ModbusFunction.FC04_ReadInputRegisters => BuildReadRegisterResponse(function),

                        ModbusFunction.FC06_WriteSingleRegister => BuildWriteSingleRegisterResponse(),
                        ModbusFunction.FC16_WriteMultipleRegisters => BuildWriteMultipleRegistersResponse(),

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
        // Request PDU : [FC][AddrHi][AddrLo][QtyHi][QtyLo]
        // Response PDU: [FC][ByteCount][RegData...]
        private DeviceResponse BuildReadRegisterResponse(ModbusFunction function)
        {
            byte fc = (byte)function;
            byte addrHi = 0x00; byte addrLo = 0x00;
            byte qtyHi = 0x00; byte qtyLo = 0x02;
            ushort reqTid = NextTransactionId();

            byte[] reqPdu = { fc, addrHi, addrLo, qtyHi, qtyLo };
            byte[] reqFull = BuildTcpFrame(reqPdu, reqTid);
            ushort startAddr = (ushort)((addrHi << 8) | addrLo);
            ushort qty = (ushort)((qtyHi << 8) | qtyLo);
            var req = MakePacket(reqFull, fc, function,
                                 startAddr, qty,
                                 byteCount: 0, dataBytes: null,
                                 isResponse: false);

            // Two 16-bit registers: Reg1 = 6, Reg2 = 5
            byte byteCount = 0x04;
            byte d1Hi = 0x00; byte d1Lo = 0x06;
            byte d2Hi = 0x00; byte d2Lo = 0x05;
            ushort resTid = NextTransactionId();

            byte[] resPdu = { fc, byteCount, d1Hi, d1Lo, d2Hi, d2Lo };
            byte[] resFull = BuildTcpFrame(resPdu, resTid);
            var res = MakePacket(resFull, fc, function,
                                 startAddr, qty,
                                 byteCount, dataBytes: new byte[] { d1Hi, d1Lo, d2Hi, d2Lo },
                                 isResponse: true);

            return new DeviceResponse("OK") { Request = req, Response = res };
        }

        // ── FC06 – Write Single Register ─────────────────────────────────────
        // Request PDU : [FC][AddrHi][AddrLo][ValHi][ValLo]
        // Response PDU: echo of request PDU (same TID)
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
        // Request PDU : [FC][AddrHi][AddrLo][QtyHi][QtyLo][ByteCount][RegData...]
        // Response PDU: [FC][AddrHi][AddrLo][QtyHi][QtyLo]
        private DeviceResponse BuildWriteMultipleRegistersResponse()
        {
            const byte fc = (byte)ModbusFunction.FC16_WriteMultipleRegisters;
            byte addrHi = 0x00; byte addrLo = 0x00;
            byte qtyHi = 0x00; byte qtyLo = 0x02;
            byte byteCount = 0x04;
            byte r1Hi = 0x00; byte r1Lo = 0x0A;  // Reg1 = 10
            byte r2Hi = 0x01; byte r2Lo = 0x02;  // Reg2 = 258
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
