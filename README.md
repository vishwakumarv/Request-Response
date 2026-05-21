# Modbus RTU Operations

For CRC, Modbus sends the lower byte first and then the upper byte.

---

# Procedure

1. Create request bytes  
2. Calculate CRC  
3. Append CRC  
4. Simulate response  
5. Convert to hex/string  
6. Display/log output  

---

# Operations

## Read Operations

- FC01 – Read Coils  
- FC02 – Read Discrete Inputs  
- FC03 – Read Holding Registers  
- FC04 – Read Input Registers  

## Write Operations

- FC05 – Write Single Coil  
- FC06 – Write Single Register  
- FC15 – Write Multiple Coils  
- FC16 – Write Multiple Registers  

---

# Function Code Details

FC01–FC06 are the most common and were defined early in the Modbus standard.  
FC07–FC14 exist but are rarely used (examples include FC07 = Read Exception Status and FC08 = Diagnostics), since they are mostly device-specific.

FC15 and FC16 (`0x0F` and `0x10` in hexadecimal) are the *Write Multiple* versions.  
They were introduced later to support writing multiple coils/registers in a single request instead of repeatedly sending FC05/FC06 requests.

So the overall pattern becomes:

| FC  | Operation      | Data Type                              |
|-----|----------------|----------------------------------------|
| 01  | Read           | Coils (single bits, read/write)        |
| 02  | Read           | Discrete Inputs (single bits, read only) |
| 03  | Read           | Holding Registers (16-bit, read/write) |
| 04  | Read           | Input Registers (16-bit, read only)    |
| 05  | Write Single   | Coil                                   |
| 06  | Write Single   | Register                               |
| 15  | Write Multiple | Coils                                  |
| 16  | Write Multiple | Registers                              |

FC07–FC14 are skipped in most industrial devices because they are either diagnostic/device-specific or were never widely adopted.

The EDJ20 device almost certainly only requires:
- FC01
- FC02
- FC03
- FC04
- FC05
- FC06
- FC15
- FC16

which matches the implemented operations.
