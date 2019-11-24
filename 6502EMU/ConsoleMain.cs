using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace EMU6502
{
    class ConsoleMain
    {
        private const string FILE_LOCATION = @"C:\Users\night\Downloads\6502_functional_test.bin";
        private const ushort START_LOCATION = 0x400;
        public static void Main(string[] args)
        {
            byte[] programROM = File.ReadAllBytes(FILE_LOCATION);
            CPU EMU = new CPU(programROM, START_LOCATION);
            EMU.InitializeCPU();
            while (true)
            {
                EMU.EmulateCycle();
               // Thread.Sleep(10);
                Console.ReadLine(); // wait until enter key pressed before doing the next cycle
            }
        }
    }
}
