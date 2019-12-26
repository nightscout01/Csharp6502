// Copyright Maurice Montag 2019
// All Rights Reserved
// See LICENSE file for more information

using System;
using System.IO;

namespace EMU6502
{
    class ConsoleMain
    {
        private const string FILE_LOCATION = @"C:\Users\night\Downloads\6502_functional_test.bin";  // replace with path to your ROM

        private const ushort START_LOCATION = 0x400;  // this isn't as rigid as the usual CHIP8 programs, we should really be using the 6502
            // reset vector to determine program start location, using the CPU constructor without the start location argument will use this vector
            // as contained in the ROM.
        public static void Main(string[] args)
        {
            byte[] programROM = File.ReadAllBytes(FILE_LOCATION);
            CPU EMU = new CPU(programROM, START_LOCATION);
            EMU.InitializeCPU();
            while (true)
            {
                EMU.EmulateCycle();
                //EMU.DEBUG = true;
                if (EMU.PC == 0x5E4)
                {
                    EMU.DEBUG = true;
                    Console.ReadLine();
                }
                //Thread.Sleep(10);
                // Console.ReadLine(); // wait until enter key pressed before doing the next cycle
            }
        }
    }
}
