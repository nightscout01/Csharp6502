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
        Absolute_Indexed_X = 7,
        Absolute_Indexed_Y = 8,
        Zero_Page_Indexed_X = 9,
        Zero_Page_Indexed_Y = 10,
        Indexed_Indirect = 11,
        Indirect_Indexed = 12
    }
    class CPU
    {
        private readonly byte[] memory;
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

            // there's actually some very specific stuff that goes down here. Apparently it looks for a memory address to jump to at a specific mem address, I should
            // probably implement that.
        }
        public void EmulateCycle()
        {
            if (cycleDelayCounter > 0)  // perhaps we'll get this thing to be cycle accurate :D
            {
                cycleDelayCounter--;
                return;
            }
            // opcodes are 1 byte, but the number of additional bytes is defined by the opcode itself, so we'll have to increment the program counter by a variable number
            byte opcode = memory[PC];  // get the opcode (opcodes are only a byte, how much data is actually used per instruction depends on the instruction)

            switch (opcode)  // there's got to be a better way to organize this. 
            {
                // LDX
                case 0xA2:
                    LDX(MemoryAddressingMode.Immediate);
                    break;
                case 0xA6:
                    LDX(MemoryAddressingMode.Zero_Page);
                    break;
                case 0xB6:
                    LDX(MemoryAddressingMode.Zero_Page_Indexed_Y);
                    break;
                case 0xAE:
                    LDX(MemoryAddressingMode.Absolute);
                    break;
                case 0xBE:
                    LDX(MemoryAddressingMode.Absolute_Indexed_Y);
                    break;

                // LDY
                case 0xA0:
                    LDY(MemoryAddressingMode.Immediate);
                    break;
                case 0xA4:
                    LDY(MemoryAddressingMode.Zero_Page);
                    break;
                case 0xB4:
                    LDY(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0xAC:
                    LDY(MemoryAddressingMode.Absolute);
                    break;
                case 0xBC:
                    LDY(MemoryAddressingMode.Absolute_Indexed_X);
                    break;


                //STX
                case 0x86:
                    STX(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x96:
                    STX(MemoryAddressingMode.Zero_Page_Indexed_Y);
                    break;
                case 0x8E:
                    STX(MemoryAddressingMode.Absolute);
                    break;


                //STY
                case 0x84:
                    STY(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x94:
                    STY(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0x8C:
                    STY(MemoryAddressingMode.Absolute);
                    break;

                // SEC
                case 0x38:
                    SEC();
                    break;

                // TYA
                case 0x98:
                    TYA();
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
                                         //  GeneralFlagHelper(X);
                    PC += 2;  // increment program counter by 2 as specified.
                    cycleDelayCounter = 2;  // this command takes 2 cycles
                    break;
                case MemoryAddressingMode.Zero_Page:
                    memLocation = memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    X = memory[memLocation];  // load the data at that zero page memory location into X
                                              //     GeneralFlagHelper(X);
                    PC += 2;  // increment program counter as specified
                    cycleDelayCounter = 3;  // this command takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_Y:
                    // The value in Y is added to the specified zero page address for a sum address. The value at the sum address is used to perform the computation.
                    memLocation = memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    X = memory[memLocation + Y];
                    //     GeneralFlagHelper(X);
                    PC += 2;  // increment program counter as specified
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:  // a full 16 bit address is specified
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    X = memory[memLocation];
                    //    GeneralFlagHelper(X);
                    PC += 3;  // increment program counter as specified (this time by 3 bytes instead of 2)
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    X = memory[memLocation + Y];  // same as above but we add y to the memory address
                                                  //    GeneralFlagHelper(X);
                    PC += 3;  // increment program counter as specified (this time by 3 bytes instead of 2)
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.  (apparently its 5 if a page boundary is crossed but idk what that means so...)
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to LDX instruction.");
            }
            GeneralFlagHelper(X);
        }

        private void LDY(MemoryAddressingMode addressingMode)
        {
            ushort memLocation;
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:  // cool switching on enum
                    // okay for this instruction we load the next byte into the X register
                    Y = memory[PC + 1];  // load the next byte into the X register
                                         //    GeneralFlagHelper(Y);
                    PC += 2;  // increment program counter by 2 as specified.
                    cycleDelayCounter = 2;  // this command takes 2 cycles
                    break;
                case MemoryAddressingMode.Zero_Page:
                    memLocation = memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    Y = memory[memLocation];  // load the data at that zero page memory location into X
                                              //  GeneralFlagHelper(Y);
                    PC += 2;  // increment program counter as specified
                    cycleDelayCounter = 3;  // this command takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    // The value in Y is added to the specified zero page address for a sum address. The value at the sum address is used to perform the computation.
                    memLocation = memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    Y = memory[memLocation + X];
                    //   GeneralFlagHelper(Y);
                    PC += 2;  // increment program counter as specified
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:  // a full 16 bit address is specified
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    Y = memory[memLocation];
                    //  GeneralFlagHelper(Y);
                    PC += 3;  // increment program counter as specified (this time by 3 bytes instead of 2)
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    Y = memory[memLocation + X];  // same as above but we add y to the memory address
                                                  //   GeneralFlagHelper(Y);
                    PC += 3;  // increment program counter as specified (this time by 3 bytes instead of 2)
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.  (apparently its 5 if a page boundary is crossed but idk what that means so...)
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to LDY instruction.");
            }
            GeneralFlagHelper(Y);  // let's see if we can just put this at the end of the switch statements
        }

        private void STX(MemoryAddressingMode addressingMode)  // Store index X in memory  -> Store value in register X into the given memory location 
        {
            ushort memLocation;
            switch (addressingMode)
            {
                case MemoryAddressingMode.Zero_Page:
                    memLocation = memory[PC + 1];  // let's hope this zero extends like we expect.
                    memory[memLocation] = X;  // we store X in that memory location.
                    cycleDelayCounter = 3;  // takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_Y:
                    memLocation = memory[PC + 1];  // let's hope this zero extends like we expect.
                    memory[memLocation + Y] = X;  // we store X in that memory location + y.
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                case MemoryAddressingMode.Absolute:
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    memory[memLocation] = X;  // we store X in that memory location + y.
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to STX instruction.");
            }
        }

        private void STY(MemoryAddressingMode addressingMode)  // Store index Y in memory  -> Store value in register Y into the given memory location 
        {
            ushort memLocation;
            switch (addressingMode)
            {
                case MemoryAddressingMode.Zero_Page:
                    memLocation = memory[PC + 1];  // let's hope this zero extends like we expect.
                    memory[memLocation] = Y;  // we store X in that memory location.
                    cycleDelayCounter = 3;  // takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    memLocation = memory[PC + 1];  // let's hope this zero extends like we expect.
                    memory[memLocation + X] = Y;  // we store X in that memory location + y.
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                case MemoryAddressingMode.Absolute:
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    memory[memLocation] = Y;  // we store X in that memory location + y.
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to STY instruction.");
            }
        }

        private void SBC(MemoryAddressingMode addressingMode)  // subtract with carry (we subtract the number from this instruction from the value in A)
        {
            ushort memLocation;
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:
                    SBCFlagHelper(memory[PC + 1]);  // MUST REMEMBER TO PUT THIS BEFORE THE ACTUAL SUBTRACTION @-@
                    A = (byte)(A - memory[PC + 1]);
                    //    GeneralFlagHelper(A);  // must remember to set the rest of the flags
                    cycleDelayCounter = 2;
                    break;
                case MemoryAddressingMode.Zero_Page:
                    memLocation = memory[PC + 1];
                    SBCFlagHelper(memory[memLocation]);
                    A = (byte)(A - memory[memLocation]);
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    memLocation = memory[PC + 1];
                    SBCFlagHelper(memory[memLocation + X]);
                    A = (byte)(A - memory[memLocation + X]);
                    break;
                    //  case MemoryAddressingMode.




            }
            GeneralFlagHelper(A);  // set the proper processor flags to the proper values
        }

        private void SEC()  // set carry flag to 1.
        {
            SetCarryFlag(true);
            cycleDelayCounter = 2;  // somehow this takes two cycles
        }

        private void TYA()  // transfer Y to accumulator
        {
            A = Y;
            GeneralFlagHelper(A);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
        }

        private void pushStack()
        {

        }


        private void SBCFlagHelper(byte val)  // a helper method to help set the various flags after an SBC command
        {
            if (val > A)
            {
                SetCarryFlag(false);
            }
        }

        private void GeneralFlagHelper(byte b)  // this might prove useful, but at least a minor refactor will be required at the end of all this
                                                // if we want to maintain nice looking code I think.
        {
            ZeroFlagHelper(b);
            NegativeFlagHelper(b);
        }


        private void ZeroFlagHelper(byte b)
        {
            if (b == 0)  // gross.
            {
                SetZeroFlag(true);
            }
            else
            {
                SetZeroFlag(false);
            }
        }

        private void NegativeFlagHelper(byte b)  // also gross
        {
            int res = b >> 7;
            if (res == 0)
            {
                SetSignFlag(false);
            }
            else
            {
                SetSignFlag(true);
            }
        }

        private ushort GetMemoryAddress(MemoryAddressingMode addressingMode)  // should we have this function also increment the program counter?
        {
            ushort memLocation;
            byte MSB;
            byte LSB;
            switch (addressingMode)
            {
                case MemoryAddressingMode.Absolute:
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    memLocation += X;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    memLocation += Y;
                    break;
                case MemoryAddressingMode.Immediate:
                    memLocation = (ushort)(PC + 1);
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    memLocation = (ushort)(memory[PC + 1] + X);  // the memory address of the LSB of the memory address we want.
                    LSB = memory[memLocation];
                    MSB = memory[memLocation + 1];
                    memLocation = (ushort)(MSB << 8 | LSB);  // it's little endian? so we have to do this, maybe C#'s casting isn't so bad after all.
                    // hope that works
                    break;
                case MemoryAddressingMode.Indirect:  // the instruction contains a 16 bit address which identifies the location of the LSB of another 16 bit 
                                                     // address which is the real target of the instruction (why is this even a thing?)
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    LSB = memory[memLocation];
                    MSB = memory[memLocation + 1];
                    memLocation = (ushort)(MSB << 8 | LSB);  // it's little endian? so we have to do this, maybe C#'s casting isn't so bad after all.
                    break;
                case MemoryAddressingMode.Indirect_Indexed:  // different than Indexed Indirect (eeeeee)
                    //In instruction contains the zero page location of the least significant byte of 16 bit address. 
                    //The Y register is dynamically added to this value to generated the actual target address for operation.
                    memLocation = memory[PC + 1];  // zero page location
                    LSB = memory[memLocation];
                    MSB = memory[memLocation + 1];
                    memLocation = (ushort)(MSB << 8 | LSB);  // it's little endian? so we have to do this, maybe C#'s casting isn't so bad after all.
                    break;
                case MemoryAddressingMode.Relative:  // instruction contains a signed 8 bit relative offset.
                    PC += 2;  // increment program counter
                    byte offset = memory[PC + 1];
                    if (offset > 127)  // gross
                    {
                        PC += offset;
                        PC -= 255;
                    }
                    else
                    {
                        PC += offset;
                    }
                    memLocation = memory[PC];  // idek if this one actually returns a memory address, we'll have to see if I can remove this case or not
                    break;
                case MemoryAddressingMode.Zero_Page:
                    memLocation = memory[PC + 1];
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    memLocation = (ushort)(memory[PC + 1] + X);
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_Y:
                    memLocation = (ushort)(memory[PC + 1] + Y);
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing mode");
            }
            return memLocation;
        }


        // HELPER METHODS FOR SETTING THE VARIOUS PROCESSOR FLAGS

        private void SetCarryFlag(bool b)
        // his holds the carry out of the most significant bit in any arithmetic operation. 
        // In subtraction operations however, this flag is cleared - set to 0 - if a borrow is required, set to 1 - if no borrow is required. 
        // The carry flag is also used in shift and rotate logical operations.
        {
            if (b)  // yeah I know I could use ternary operators or something but like ehhhh
            {
                status = (byte)(status | 0x01);  // 0000 0001  we set the carry bit to true 
            }
            else
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

        // ALSO CALLED THE NEGATIVE FLAG? Apparently it gets the 7th (MSB) bit of most operations that return a value
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
