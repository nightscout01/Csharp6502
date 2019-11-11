using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace EMU6502
{
    class ConsoleMain
    {
        private const string FILE_LOCATION = @"";
        private const ushort START_LOCATION = 0x0;
        public static void Main(string[] args)
        {
            byte[] programROM = File.ReadAllBytes(FILE_LOCATION);
            CPU EMU = new CPU(programROM, START_LOCATION);
            EMU.InitializeCPU();
            while (true)
            {
                Thread.Sleep(100);
                EMU.EmulateCycle();
            }
        }
    }
}
