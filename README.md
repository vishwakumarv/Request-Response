# Modbus RTU vs Modbus TCP/IP

## What Changes?

The main difference between Modbus RTU and Modbus TCP/IP is the **frame structure**.

### Modbus RTU Frame

```text
[SlaveID][FunctionCode][Data][CRC-Lo][CRC-Hi]
```

- Uses serial communication (RS485/UART)
- Includes CRC for error checking
- Compact binary frame

---

### Modbus TCP/IP Frame

```text
[TransID-Hi][TransID-Lo]
[Proto-Hi][Proto-Lo]
[Len-Hi][Len-Lo]
[UnitID]
[FunctionCode]
[Data...]
```

- Uses Ethernet/TCP communication
- Replaces CRC with an MBAP Header
- Runs on TCP Port `502`

---

## Key Difference

| Feature | Modbus RTU | Modbus TCP/IP |
|---|---|---|
| Communication | Serial | Ethernet/TCP |
| Error Check | CRC | TCP checksum |
| Header | None | MBAP Header |
| Port | COM Port | Port 502 |
| Speed | Slower | Faster |

---

## MBAP Header Structure

| Bytes | Field |
|---|---|
| 2 | Transaction ID |
| 2 | Protocol ID |
| 2 | Length |
| 1 | Unit ID |

---

## Example Read Holding Register Request (TCP)

```text
00 01 00 00 00 06 01 03 00 00 00 01
```

### Breakdown

| Bytes | Meaning |
|---|---|
| 00 01 | Transaction ID |
| 00 00 | Protocol ID |
| 00 06 | Length |
| 01 | Unit ID |
| 03 | Function Code |
| 00 00 | Start Address |
| 00 01 | Quantity |

---

## Project Goal

This project demonstrates:
- Modbus TCP/IP connection using C#
- Reading Holding Registers
- Writing Holding Registers
- Understanding MBAP framing
- Comparing RTU and TCP packet structures



Simulating our PC as a Modbus TCP/IP Slave using diagslave

CMD - diagslave.exe -m tcp -p <portno.>
