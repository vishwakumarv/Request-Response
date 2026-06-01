using System;

namespace Edj20Tester.Models
{
    public enum ModbusFunction
    {
        FC03_ReadHoldingRegisters = 3,
        FC04_ReadInputRegisters = 4,
        FC06_WriteSingleRegister = 6,
        FC16_WriteMultipleRegisters = 16,
    }

    public enum TestStatus
    {
        Pass,
        Fail,
        Error,
        Skipped
    }

    public class TestResult
    {
        public string TestPoint { get; set; } = string.Empty;
        public string TestType { get; set; } = string.Empty;
        public ModbusFunction Function { get; set; }
        public double MeasuredValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string ExpectedRange { get; set; } = string.Empty;
        public TestStatus Status { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool IsPass => Status == TestStatus.Pass;
    }
}
