using System;
using System.Collections.Generic;

namespace EMU6502
{

    enum MemoryAddressingMode  // this is just the worst (at least most instructions don't have all of them)
    {
        Accumulator = 0,
        Immediate = 1,
        Implied = 2,
        Relative = 3,
        Absolute = 4,
        Zero_Page = 5,
        Indirect = 6,
        Absolute_Indexed = 7,
        Zero_Page_Indexed_X = 8,
        Zero_Page_Indexed_Y = 9,
        Indexed_Indirect = 10,
        Indirect_Indexed = 11
    }
    class CPU
    {
        private byte[] memory;
        private ushort PC;  // program counter
        private byte status;  // status reg
        /*
         *  Bit No. 7   6   5   4   3   2   1   0
                    S   V       B   D   I   Z   C
         */
        private byte SP;  // stack pointer?
        private byte X;  // register X
        private byte Y;  // register Y
        private byte A;  // accumulator register
        private int cycleDelayCounter;
        readonly Stack<ushort> stack = new Stack<ushort>();  // we'll just use a stack for our stack why not?
            // On the 6502, the stack starts at 0x1FF and grows towards 0x100.
        // 6502 instruction operation codes (opcodes) 
        // are eight-bits long and have the general form aaabbbcc, where aaa and cc 
        // define the opcode, and bbb defines the addressing mode.

        // THIS CPU IS NOT MULTITHREADED... duh
        public CPU(byte[] b)
        {
            memory = new byte[65536];  // allocate 64K of "RAM" for the CPU
            Array.Copy(b, 0, memory, 0, b.Length);  // copy the passed in program into RAM if applicable.
            PC = 0;  // set the program counter to 0
            status = (byte)(status | 0x20);  // 0010 0000  we set status bit 5 to 1, as it is not used and should always contain logical 1.
        }
        public void emulateCycle()
        {
            if(cycleDelayCounter > 0)  // perhaps we'll get this thing to be cycle accurate :D
            {
                cycleDelayCounter--;
                return;
            }
            // opcodes are 1 byte, but the number of additional bytes is defined by the opcode itself, so we'll have to increment the program counter by a variable number
            byte opcode = memory[PC];  // get the opcode

            switch (opcode)  // there's got to be a better way to organize this. 
            {
               case 0xA2:  // LDX (Load index X with memory)  immediate
                    LDX(MemoryAddressingMode.Immediate);
                    break;
                    
                        
            }
        }

        private void LDX(MemoryAddressingMode addressingMode)
        {
            ushort memLocation;
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:  // cool switching on enum
                    // okay for this instruction we load the next byte into the X register
                    X = memory[PC + 1];  // load the next byte into the X register
                    PC += 2;  // increment program counter by 2 as specified.
                    cycleDelayCounter = 2;  // this command takes 2 cycles
                    break;
                case MemoryAddressingMode.Zero_Page:
                    memLocation = memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    X = memory[memLocation];  // load the data at that zero page memory location into X
                    PC += 2;  // increment program counter as specified
                    cycleDelayCounter = 3;  // this command takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_Y:
                    // The value in Y is added to the specified zero page address for a sum address. The value at the sum address is used to perform the computation.
                    memLocation = memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    X = memory[memLocation+Y];
                    PC += 2;  // increment program counter as specified
                    break;
            }
        }

        private void pushStack()
        {

        }










        // HELPER METHODS FOR SETTING THE VARIOUS PROCESSOR FLAGS

        private void SetCarryFlag(bool b) 
                // his holds the carry out of the most significant bit in any arithmetic operation. 
            // In subtraction operations however, this flag is cleared - set to 0 - if a borrow is required, set to 1 - if no borrow is required. 
            // The carry flag is also used in shift and rotate logical operations.
        {
            if (b)  // yeah I know I could use ternary operators or something but like ehhhh
            {
                status = (byte) (status | 0x01);  // 0000 0001  we set the carry bit to true 
            } else
            {
                status = (byte)(status & 0xFE);  // 1111 1110  we set the carry bit to false
            }
        }

        private void SetZeroFlag(bool b)  // this is set to 1 when any arithmetic or 
            // logical operation produces a zero result, and is set to 0 if the result is non-zero.
        {
            if (b)  // yeah I know I could use ternary operators or something but like ehhhh
            {
                status = (byte)(status | 0x02);  // 0000 0010  we set the zero bit to true 
            }
            else
            {
                status = (byte)(status & 0xFD);  // 1111 1101  we set the zero bit to false
            }
        }

        private void SetInterruptFlag(bool b)  // if this is set, interrupts are disabled, if it is not set, interrupts are enabled
        {
            if (b)  // yeah I know I could use ternary operators or something but like ehhhh
            {
                status = (byte)(status | 0x04);  // 0000 0100  we set the interrupt bit to true 
            }
            else
            {
                status = (byte)(status & 0xFB);  // 1111 1011  we set the interrupt bit to false
            }
        }

        private void SetBCDFlag(bool b)  // if this is set, if using add or subtract with carry, results are treated as BCD (NES does not use this)
        {
            if (b)  // yeah I know I could use ternary operators or something but like ehhhh
            {
                status = (byte)(status | 0x08);  // 0000 1000  we set the BCD bit to true 
            }
            else
            {
                status = (byte)(status & 0xF7);  // 1111 0111  we set the BCD bit to false
            }
        }

        private void SetSoftWareInterruptFlag(bool b)  // set when a software interrupt is executed
        {
            if (b)  // yeah I know I could use ternary operators or something but like ehhhh
            {
                status = (byte)(status | 0x10);  // 0001 0000  we set the software interrupt bit to true
            }
            else
            {
                status = (byte)(status & 0xEF);  // 1110 1111  we set that bit to false
            }
        }

        private void SetOverflowFlag(bool b)  // set when an operation produces a result too big to be held in a byte
        {
            if (b)  // yeah I know I could use ternary operators or something but like ehhhh
            {
                status = (byte)(status | 0x40);  // 0100 0000  we set the overflow (V) bit to true
            }
            else
            {
                status = (byte)(status & 0xBF);  // 1011 1111  we set that bit to false
            }
        }

        private void SetSignFlag(bool b)  // set when the result of an operation is negative, cleared if its positive
        {
            if (b)  // yeah I know I could use ternary operators or something but like ehhhh
            {
                status = (byte)(status | 0x80);  // 1000 0000  we set the overflow (V) bit to true
            }
            else
            {
                status = (byte)(status & 0x7F);  // 0111 1111  we set that bit to false
            }
        }
    }
}
