using System;
using System.Collections.Generic;

namespace EMU6502
{
    class CPU
    {
        byte[] memory;
        ushort PC;  // program counter
        byte status;  // status reg
        /*
         *  Bit No. 7   6   5   4   3   2   1   0
                    S   V       B   D   I   Z   C
         */
        byte SP;  // stack pointer?
        byte x;  // register X
        byte y;  // register Y
        byte accum;  // accumulator register
        readonly Stack<ushort> stack = new Stack<ushort>();  // we'll just use a stack for our stack why not?
            // On the 6502, the stack starts at 0x1FF and grows towards 0x100.

        // 6502 instruction operation codes (opcodes) 
        // are eight-bits long and have the general form aaabbbcc, where aaa and cc 
        // define the opcode, and bbb defines the addressing mode.

        public CPU(byte[] b)
        {
            memory = new byte[65536];  // allocate 64K of "RAM" for the CPU
            Array.Copy(b, 0, memory, 0, b.Length);  // copy the passed in program into RAM if applicable.
            PC = 0;  // set the program counter to 0
            status = (byte)(status | 0x20);  // 0010 0000  we set status bit 5 to 1, as it is not used and should always contain logical 1.
        }
        public void emulateCycle()
        {
            
        }

        private void pushStack()
        {

        }

        private void SetCarryFlag(bool b)
        {
            if (b)  // yeah I know I could use ternary operators or something but like ehhhh
            {
                status = (byte) (status | 0x01);  // 0000 0001  we set the carry bit to true 
            } else
            {
                status = (byte)(status & 0xFE);  // 1111 1110  we set the carry bit to false
            }
        }

        private void SetZeroFlag(bool b)
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
