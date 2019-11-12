// Copyright Maurice Montag 2019
// All Rights Reserved.


using System;

namespace EMU6502
{

    enum MemoryAddressingMode  // this is just the worst (at least most instructions don't have all of them)
    {
        Accumulator = 0,  // this will (likely) always need to be a special case because commands with this addressing mode work on the A register
        Immediate = 1,  // instead of a value in memory.
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

    enum CPUFlag  // this could come in handy
    {
        C = 0,
        Z = 1,
        I = 2,
        D = 3,
        B = 4,
        UNUSED_RESERVED = 5,
        V = 6,
        S = 7,
    }


    class CPU
    {
        /* BIG TODOS:
         *  V/overflow flag is not set in the ADC instruction, and probably not set anywhere
         *  Not fully cycle accurate as when we go over a page boundary we're supposed to add an extra cycle, I haven't done that yet.
         *  Find a consistent and good way to handle getting and setting flags. Having both byte (0 or 1) and boolean versions for set is odd.
         */
        private bool initialized;   // a cheap hack, but I believe it's needed for sanity checking.
        private const bool DEBUG = true;
        private readonly byte[] memory;
        private ushort PC;  // program counter
        private byte status;  // status reg
        /*
         *  Bit No. 7   6   5   4   3   2   1   0
                    S   V       B   D   I   Z   C
         */
        private byte S;  // stack pointer.  The 6502 stack pointer holds the last byte of the memory address 0x01XX. 
                         // The 6502 stack grows down from 0x01FF to 0x0100. 
        private byte X;  // register X
        private byte Y;  // register Y
        private byte A;  // accumulator register
        private int cycleDelayCounter;
        // 6502 instruction operation codes (opcodes) 
        // are eight-bits long and have the general form aaabbbcc, where aaa and cc 
        // define the opcode, and bbb defines the addressing mode.

        // THIS CPU IS NOT MULTITHREADED... duh
        public CPU(byte[] b)  // probably shouldn't use this constructor, I'll most likely remove it.
        {
            memory = new byte[65536];  // allocate 64K of "RAM" for the CPU
            Array.Copy(b, 0, memory, 0, b.Length);  // copy the passed in program into RAM if applicable.
            PC = 0;  // set the program counter to 0
            status = (byte)(status | 0x20);  // 0010 0000  we set status bit 5 to 1, as it is not used and should always contain logical 1.

            S = 0;  // apparently on the 6502 the stack pointer is NOT self initializing, most ROMS contain code to set it to the proper value,
                    // 0xFF, on startup

            // there's actually some very specific stuff that goes down here. Apparently it looks for a memory address to jump to at a specific mem address, I should
            // probably implement that.
        }

        public CPU(byte[] b, ushort startLocation)  // unlike CHIP-8, there's no defined program data start location
        {
            memory = new byte[65536];  // allocate 64K of "RAM" for the CPU
            Array.Copy(b, 0, memory, startLocation, b.Length);  // copy the passed in program into RAM at the specified index.
            PC = 0;  // set the program counter to 0
            S = 0; // apparently on the 6502 the stack pointer is NOT self initializing, most ROMS contain code to set it to the proper value,
                   // 0xFF, on startup
            status = (byte)(status | 0x20);
            // 0010 0000  we set status bit 5 to 1, as it is not used and should always contain logical 1.
            // on reset, the 6502 looks for the program address to jump to at addresses 0xFFFC and 0xFFFD (low byte and high byte respectively)
            // we should store the start location in those addresses.
            byte MSB = (byte)(startLocation >> 8);
            byte LSB = (byte)(startLocation & 0x00FF);
            memory[0xFFFC] = LSB;
            memory[0xFFFD] = MSB;
        }

        public void InitializeCPU()  // essentially the same as a power on or reset (I should probably make the RESET interrupt just call this if I add it)
        {
            // we read the program start address from the memory addresses 0xFFFC and 0xFFFD
            byte MSB = memory[0xFFFD];
            byte LSB = memory[0xFFFC];  // we could not even use the temp variables, but I'm trying to emphasize readability (or something). 
            PC = (ushort)((MSB << 8) | LSB);
            initialized = true;  // set the init flag.
        }


        public void EmulateCycle()
        {
            if (!initialized)  // TODO: might slow stuff down a lil bit, I'll have to see if this is really needed later.
            {
                throw new InvalidOperationException("The CPU is not initialized");
            }
            //if (cycleDelayCounter > 0)  // perhaps we'll get this thing to be cycle accurate :D
            //{
            //    cycleDelayCounter--;
            //    return;
            //}
            // opcodes are 1 byte, but the number of additional bytes is defined by the opcode itself, 
            // so we'll have to increment the program counter by a variable number
            byte opcode = memory[PC];  // get the opcode (opcodes are only a byte, how much data is actually used per 
            // instruction depends on the instruction)
            if (DEBUG)
            {
                Console.WriteLine("Current Opcode: {0:X}", opcode);  // FOR DEBUGGING
                Console.WriteLine("A: 0x{0:X}", A);
                Console.WriteLine("X: 0x{0:X}", X);
                Console.WriteLine("Y: 0x{0:X}", Y);
                Console.WriteLine("PC: 0x{0:X}", PC);
            }
            switch (opcode)  // there's got to be a better way to organize this. 
            {
                // BRK
                case 0x0:
                    BRK();
                    break;

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

                // LDA
                case 0xA9:
                    LDA(MemoryAddressingMode.Immediate);
                    break;
                case 0xA5:
                    LDA(MemoryAddressingMode.Zero_Page);
                    break;
                case 0xB5:
                    LDA(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0xAD:
                    LDA(MemoryAddressingMode.Absolute);
                    break;
                case 0xBD:
                    LDA(MemoryAddressingMode.Absolute_Indexed_X);
                    break;
                case 0xB9:
                    LDA(MemoryAddressingMode.Absolute_Indexed_Y);
                    break;
                case 0xA1:
                    LDA(MemoryAddressingMode.Indirect_Indexed);
                    break;
                case 0xB1:
                    LDA(MemoryAddressingMode.Indexed_Indirect);
                    break;

                // STX
                case 0x86:
                    STX(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x96:
                    STX(MemoryAddressingMode.Zero_Page_Indexed_Y);
                    break;
                case 0x8E:
                    STX(MemoryAddressingMode.Absolute);
                    break;


                // STY
                case 0x84:
                    STY(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x94:
                    STY(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0x8C:
                    STY(MemoryAddressingMode.Absolute);
                    break;

                // STA
                case 0x85:
                    STA(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x95:
                    STA(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0x80:
                    STA(MemoryAddressingMode.Absolute);
                    break;
                case 0x9D:
                    STA(MemoryAddressingMode.Absolute_Indexed_X);
                    break;
                case 0x99:
                    STA(MemoryAddressingMode.Absolute_Indexed_Y);
                    break;
                case 0x81:
                    STA(MemoryAddressingMode.Indirect_Indexed);
                    break;
                case 0x91:
                    STA(MemoryAddressingMode.Indexed_Indirect);
                    break;

                // BIT
                case 0x24:
                    BIT(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x2C:
                    BIT(MemoryAddressingMode.Absolute);
                    break;

                // SEC
                case 0x38:
                    SEC();
                    break;

                // CLC
                case 0x18:
                    CLC();
                    break;

                // CLD
                case 0xD8:
                    CLD();
                    break;

                // CLI
                case 0x58:
                    CLI();
                    break;

                // CLV
                case 0xB8:
                    CLV();
                    break;

                // TXA
                case 0x8A:
                    TXA();
                    break;

                // TAX
                case 0xAA:
                    TAX();
                    break;

                // TYA
                case 0x98:
                    TYA();
                    break;

                // TAY
                case 0xA8:
                    TAY();
                    break;

                // TSX
                case 0xBA:
                    TSX();
                    break;

                // TXS
                case 0x9A:
                    TXS();
                    break;

                // INX
                case 0xE8:
                    INX();
                    break;

                // INY
                case 0xC8:
                    INY();
                    break;

                // DEY
                case 0x88:
                    DEY();
                    break;

                // DEX
                case 0xCA:
                    DEX();
                    break;

                // NOP
                case 0xEA:
                    NOP();
                    break;

                // ADC
                case 0x69:
                    ADC(MemoryAddressingMode.Immediate);
                    break;
                case 0x65:
                    ADC(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x75:
                    ADC(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0x60:
                    ADC(MemoryAddressingMode.Absolute);
                    break;
                case 0x7D:
                    ADC(MemoryAddressingMode.Absolute_Indexed_X);
                    break;
                case 0x79:
                    ADC(MemoryAddressingMode.Absolute_Indexed_Y);
                    break;
                case 0x61:
                    ADC(MemoryAddressingMode.Indexed_Indirect);
                    break;
                case 0x71:
                    ADC(MemoryAddressingMode.Indirect_Indexed);
                    break;

                // SBC
                case 0xE9:
                    SBC(MemoryAddressingMode.Immediate);
                    break;
                case 0xE5:
                    SBC(MemoryAddressingMode.Zero_Page);
                    break;
                case 0xF5:
                    SBC(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0xED:
                    SBC(MemoryAddressingMode.Absolute);
                    break;
                case 0xFD:
                    SBC(MemoryAddressingMode.Absolute_Indexed_X);
                    break;
                case 0xF9:
                    SBC(MemoryAddressingMode.Absolute_Indexed_Y);
                    break;
                case 0xE1:
                    SBC(MemoryAddressingMode.Indexed_Indirect);
                    break;
                case 0xF1:
                    SBC(MemoryAddressingMode.Indirect_Indexed);
                    break;

                // AND 
                case 0x29:
                    AND(MemoryAddressingMode.Immediate);
                    break;
                case 0x25:
                    AND(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x35:
                    AND(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0x2D:
                    AND(MemoryAddressingMode.Absolute);
                    break;
                case 0x3D:
                    AND(MemoryAddressingMode.Absolute_Indexed_X);
                    break;
                case 0x39:
                    AND(MemoryAddressingMode.Absolute_Indexed_Y);
                    break;
                case 0x21:
                    AND(MemoryAddressingMode.Indexed_Indirect);
                    break;
                case 0x31:
                    AND(MemoryAddressingMode.Indirect_Indexed);
                    break;

                // ORA
                case 0x09:
                    ORA(MemoryAddressingMode.Immediate);
                    break;
                case 0x05:
                    ORA(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x15:
                    ORA(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0x0D:
                    ORA(MemoryAddressingMode.Absolute);
                    break;
                case 0x1D:
                    ORA(MemoryAddressingMode.Absolute_Indexed_X);
                    break;
                case 0x19:
                    ORA(MemoryAddressingMode.Absolute_Indexed_Y);
                    break;
                case 0x01:
                    ORA(MemoryAddressingMode.Indexed_Indirect);
                    break;
                case 0x11:
                    ORA(MemoryAddressingMode.Indirect_Indexed);
                    break;

                // EOR
                case 0x49:
                    EOR(MemoryAddressingMode.Immediate);
                    break;
                case 0x45:
                    EOR(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x55:
                    EOR(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0x40:
                    EOR(MemoryAddressingMode.Absolute);
                    break;
                case 0x5D:
                    EOR(MemoryAddressingMode.Absolute_Indexed_X);
                    break;
                case 0x59:
                    EOR(MemoryAddressingMode.Absolute_Indexed_Y);
                    break;
                case 0x41:
                    EOR(MemoryAddressingMode.Indexed_Indirect);
                    break;
                case 0x51:
                    EOR(MemoryAddressingMode.Indirect_Indexed);
                    break;

                // ASL
                case 0x0A:
                    ASL(MemoryAddressingMode.Accumulator);
                    break;
                case 0x06:
                    ASL(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x16:
                    ASL(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0x0E:
                    ASL(MemoryAddressingMode.Absolute);
                    break;
                case 0x1E:
                    ASL(MemoryAddressingMode.Absolute_Indexed_X);
                    break;

                // LSR
                case 0x4A:
                    LSR(MemoryAddressingMode.Accumulator);
                    break;
                case 0x46:
                    LSR(MemoryAddressingMode.Zero_Page);
                    break;
                case 0x56:
                    LSR(MemoryAddressingMode.Zero_Page_Indexed_X);
                    break;
                case 0x4E:
                    LSR(MemoryAddressingMode.Absolute);
                    break;
                case 0x5E:
                    LSR(MemoryAddressingMode.Absolute_Indexed_X);
                    break;


                // Conditional Branches
                case 0xD0:
                    BNE();
                    break;
                case 0x90:
                    BCC();
                    break;
                case 0xB0:
                    BCS();
                    break;
                case 0xF0:
                    BEQ();
                    break;
                case 0x30:
                    BMI();
                    break;
                case 0x10:
                    BPL();
                    break;
                case 0x50:
                    BVC();
                    break;
                case 0x70:
                    BVS();
                    break;


                // Stack Instructions

                case 0x48:
                    PHA();
                    break;
                case 0x08:
                    PHP();
                    break;
                case 0x68:
                    PLA();
                    break;
                case 0x28:
                    PLP();
                    break;











                default:
                    throw new ArgumentException("ERROR: unknown opcode found: " + opcode);  // it's not going to be hex formatted but it's better
                                                                                            // than nothing
            }
        }

        private void LDX(MemoryAddressingMode addressingMode)
        {
            if (DEBUG)
            {
                Console.WriteLine("LDX");
            }
            ushort memLocation;
            memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
            X = memory[memLocation];  // load the data at that zero page memory location into X
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:  // cool switching on enum
                    cycleDelayCounter = 2;  // this command takes 2 cycles
                    break;
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;  // this command takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_Y:
                    // The value in Y is added to the specified zero page address for a sum address. The value at 
                    // the sum address is used to perform the computation.
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:  // a full 16 bit address is specified
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.  
                    // (apparently its 5 if a page boundary is crossed but idk what that means so...)
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to LDX instruction.");
            }
            GeneralFlagHelper(X);
        }

        private void LDY(MemoryAddressingMode addressingMode)
        {
            if (DEBUG)
            {
                Console.WriteLine("LDY");
            }
            ushort memLocation;
            memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
            Y = memory[memLocation];  // load the data at that zero page memory location into X
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:  // cool switching on enum
                    cycleDelayCounter = 2;  // this command takes 2 cycles
                    break;
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;  // this command takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:  // a full 16 bit address is specified
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.  (apparently its 5 if a page boundary 
                    // is crossed but idk what that means so...)
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to LDY instruction.");
            }
            GeneralFlagHelper(Y);  // let's see if we can just put this at the end of the switch statements
        }

        private void LDA(MemoryAddressingMode addressingMode)  // I can definitely shrink this guy down too, there's a lot of repeated code. 
        {
            if (DEBUG)
            {
                Console.WriteLine("LDA");
            }
            ushort memLocation;
            memLocation = GetMemoryAddress(addressingMode);
            A = memory[memLocation];  // load the data at that memory location into A
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:  // cool switching on enum
                    cycleDelayCounter = 2;  // this command takes 2 cycles
                    break;
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;  // this command takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:  // a full 16 bit address is specified
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:  // add 1 if page boundary crossed (eeeee)
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.  (apparently its 5 if a page boundary 
                    // is crossed but idk what that means so...)
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.  (apparently its 5 if a page boundary 
                    // is crossed but idk what that means so...)
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    cycleDelayCounter = 6;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:
                    cycleDelayCounter = 5;  // 6 if page boundary crossed but idk
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to LDA instruction.");
            }
            GeneralFlagHelper(A);  // let's see if we can just put this at the end of the switch statements
        }

        private void STX(MemoryAddressingMode addressingMode)  // Store index X in memory  -> Store value in register X into the given memory location 
        {
            ushort memLocation;
            memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // let's hope this zero extends like we expect.
            memory[memLocation] = X;  // we store X in that memory location.
            if (DEBUG)
            {
                Console.WriteLine("STX {0:X}", memory[memLocation]);  // this isn't a dissasembler, as we don't know which memory accessing mode
                                                                      // was used, but it's better than nothing
            }
            switch (addressingMode)
            {
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;  // takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_Y:
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                case MemoryAddressingMode.Absolute:
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to STX instruction.");
            }
        }

        private void STY(MemoryAddressingMode addressingMode)  // Store index Y in memory  -> Store value in register Y into the given memory location 
        {
            ushort memLocation;
            memLocation = GetMemoryAddress(addressingMode);
            memory[memLocation] = Y;  // we store X in that memory location.
            if (DEBUG)
            {
                Console.WriteLine("STY {0:X}", memory[memLocation]);  // this isn't a dissasembler, as we don't know which memory accessing mode
                                                                      // was used, but it's better than nothing.
            }
            switch (addressingMode)
            {
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;  // takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                case MemoryAddressingMode.Absolute:
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to STY instruction.");
            }
        }

        private void STA(MemoryAddressingMode addressingMode)  // I'm going to try implementing this one in it's shorter form
        {
            ushort memLocation;
            memLocation = GetMemoryAddress(addressingMode);
            memory[memLocation] = A;
            if (DEBUG)
            {
                Console.WriteLine("STA {0:X}", memory[memLocation]);
            }
            switch (addressingMode)
            {
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:
                    cycleDelayCounter = 5;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    cycleDelayCounter = 5;
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    cycleDelayCounter = 6;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:
                    cycleDelayCounter = 6;
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to STA instruction: " + addressingMode);
            }
        }

        private void ADC(MemoryAddressingMode addressingMode)
        {
            if (DEBUG)
            {
                Console.WriteLine("ADC");
            }
            // TODO: this method needs to set V (overflow) flag when required, but ehh I'll get to that later
            ushort memLocation = GetMemoryAddress(addressingMode);
            // A += GetCarryFlag();  // we add the carry flag to the accumulator in this operation.
            int val = memory[memLocation] + A + GetCarryFlag();
            if (val > 255)
            {
                SetCarryFlag(true);
                val -= 256;
            }
            else
            {
                SetCarryFlag(false);
            }
            A = (byte)val;
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:
                    cycleDelayCounter = 2;
                    break;
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:  // 5 if it crosses a page boundary but at the moment I don't know what that means
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:  // 5 if it crosses a page boundary but at the moment I don't know what that means
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    cycleDelayCounter = 6;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:  // 6 if it crosses a page boundary but at the moment I don't know what that means
                    cycleDelayCounter = 5;
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to ADC instruction: " + addressingMode);
            }
            GeneralFlagHelper(A);
        }

        private void AND(MemoryAddressingMode addressingMode)
        {
            if (DEBUG)
            {
                Console.WriteLine("AND");
            }
            ushort memLocation = GetMemoryAddress(addressingMode);
            A = (byte)(memory[memLocation] & A);
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:
                    cycleDelayCounter = 2;
                    break;
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:
                    cycleDelayCounter = 4;   // 5 when going over a page break
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    cycleDelayCounter = 4;   // 5 when going over a page break
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    cycleDelayCounter = 6;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:
                    cycleDelayCounter = 5;
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to AND instruction: " + addressingMode);
            }
            GeneralFlagHelper(A);
        }


        private void ORA(MemoryAddressingMode addressingMode)  // OR memory with accumulator
        {
            if (DEBUG)
            {
                Console.WriteLine("ORA");
            }
            ushort memLocation = GetMemoryAddress(addressingMode);
            A = (byte)(memory[memLocation] | A);
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:
                    cycleDelayCounter = 2;
                    break;
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:
                    cycleDelayCounter = 4;   // 5 when going over a page break
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    cycleDelayCounter = 4;   // 5 when going over a page break
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    cycleDelayCounter = 6;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:
                    cycleDelayCounter = 5;
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to ORA instruction: " + addressingMode);
            }
            GeneralFlagHelper(A);
        }

        private void EOR(MemoryAddressingMode addressingMode)  // XOR memory with accumulator
        {
            if (DEBUG)
            {
                Console.WriteLine("EOR");
            }
            ushort memLocation = GetMemoryAddress(addressingMode);
            A = (byte)(memory[memLocation] ^ A);
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:
                    cycleDelayCounter = 2;
                    break;
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:
                    cycleDelayCounter = 4;   // 5 when going over a page break
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    cycleDelayCounter = 4;   // 5 when going over a page break
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    cycleDelayCounter = 6;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:
                    cycleDelayCounter = 5;  // apparently 6 when going over a page break, only in EOR for some reason. (Look into that).
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to EOR instruction: " + addressingMode);
            }
            GeneralFlagHelper(A);
        }

        private void ASL(MemoryAddressingMode addressingMode)  // shift left 1 bit
        {
            if (DEBUG)
            {
                Console.WriteLine("ASL");
            }
            if (addressingMode == MemoryAddressingMode.Accumulator)  // special case
            {
                SetCarryFlag((byte)(A >> 7));  // using new overloaded SetCarryFlag. I'll have to look into how I actually want the flags to be 
                                               // set and retrieved and choose 1 good option.
                A = (byte)(A << 1);  // shift A by 1 to the left
                PC += 1;
                cycleDelayCounter = 2;  // this takes two cycles.
                GeneralFlagHelper(A);
            }
            else
            {
                ushort memLocation = GetMemoryAddress(addressingMode);
                SetCarryFlag((byte)(memory[memLocation] >> 7));  // set the carry flag to the MSB of whatever byte is in memory.
                memory[memLocation] = (byte)(memory[memLocation] << 1);  // actually perform the left shift
                switch (addressingMode)
                {
                    case MemoryAddressingMode.Zero_Page:
                        cycleDelayCounter = 5;
                        break;
                    case MemoryAddressingMode.Zero_Page_Indexed_X:
                        cycleDelayCounter = 6;
                        break;
                    case MemoryAddressingMode.Absolute:
                        cycleDelayCounter = 6;
                        break;
                    case MemoryAddressingMode.Absolute_Indexed_X:
                        cycleDelayCounter = 7;
                        break;
                    default:
                        throw new ArgumentException("Invalid Addressing Mode passed to ASL instruction: " + addressingMode);
                }
                GeneralFlagHelper(memory[memLocation]);
            }
        }

        private void LSR(MemoryAddressingMode addressingMode)  // logical right shift by 1 bit
        {
            if (DEBUG)
            {
                Console.WriteLine("LSR");
            }
            if (addressingMode == MemoryAddressingMode.Accumulator)  // special case
            {
                A = (byte)(A >> 1);  // shift A by 1 to the right  (since results are cast to an int afterwards, let's just mask off the most signifcant
                                     // bit with a zero after we're done with the shifting.
                A = (byte)(A & 0x7f);  // mask A with bitmask 0b 0111 1111
                PC += 1;
                cycleDelayCounter = 2;  // this takes two cycles.
                SetSignFlag(false);  // sign flag is always false after this instruction executes
                SetZeroFlag(A == 0);  // pros: only 1 line, very clean looking.  cons: pretty weird looking to anyone who doesn't realize you can do this.
            }
            else
            {
                ushort memLocation = GetMemoryAddress(addressingMode);
                memory[memLocation] = (byte)(memory[memLocation] >> 1);  // actually perform the right shift
                memory[memLocation] = (byte)(memory[memLocation] & 0x7f);  // mask A with bitmask 0b 0111 1111
                // seems like a nice place to try to use ref byte or something. (TODO: look into that)
                switch (addressingMode)
                {
                    case MemoryAddressingMode.Zero_Page:
                        cycleDelayCounter = 5;
                        break;
                    case MemoryAddressingMode.Zero_Page_Indexed_X:
                        cycleDelayCounter = 6;
                        break;
                    case MemoryAddressingMode.Absolute:
                        cycleDelayCounter = 6;
                        break;
                    case MemoryAddressingMode.Absolute_Indexed_X:
                        cycleDelayCounter = 7;
                        break;
                    default:
                        throw new ArgumentException("Invalid Addressing Mode passed to LSR instruction: " + addressingMode);
                }
                SetSignFlag(false);  // set the sign/negative flag to false/0 (specified for this instruction)
                SetZeroFlag(memory[memLocation] == 0);  // very short and snazzy, but perhaps not the most readable.
            }
        }

        private void SBC(MemoryAddressingMode addressingMode)  // subtract with carry (we subtract the number from this instruction from the value in A)
        {
            if (DEBUG)
            {
                Console.WriteLine("SBC");
            }
            ushort memLocation;
            memLocation = GetMemoryAddress(addressingMode);
            SBCFlagHelper(memory[memLocation]);  // this HAS to go before the subtraction operation
            A = (byte)(A - memory[memLocation]);
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:
                    cycleDelayCounter = 2;
                    break;
                case MemoryAddressingMode.Absolute:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    cycleDelayCounter = 6;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:
                    cycleDelayCounter = 5;
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to STY instruction: " + addressingMode);
            }
            GeneralFlagHelper(A);  // set the proper processor flags to the proper values
        }

        private void SEC()  // set carry flag to 1.
        {
            if (DEBUG)
            {
                Console.WriteLine("SEC");
            }
            SetCarryFlag(true);
            cycleDelayCounter = 2;  // somehow this takes two cycles
            PC += 1;
        }

        private void CLC()  // set carry flag to 0
        {
            if (DEBUG)
            {
                Console.WriteLine("CLC");
            }
            SetCarryFlag(false);
            cycleDelayCounter = 2;
            PC += 1;
        }

        private void CLD()  // set decimal flag to 0 (clear decimal mode)
        {
            if (DEBUG)
            {
                Console.WriteLine("CLD");
            }
            SetBCDFlag(false);
            cycleDelayCounter = 2;
            PC += 1;
        }

        private void CLI()  // set interrupt disable flag to 0 (clear interrupt flag)
        {

            if (DEBUG)
            {
                Console.WriteLine("CLI");
            }
            SetInterruptFlag(false);
            cycleDelayCounter = 2;
            PC += 1;
        }

        private void CLV()  // set overflow flag to 0 (clear overflow flag)
        {

            if (DEBUG)
            {
                Console.WriteLine("CLV");
            }
            SetOverflowFlag(false);
            cycleDelayCounter = 2;
            PC += 1;
        }

        private void TXA()  // transfer X to accumulator
        {
            if (DEBUG)
            {
                Console.WriteLine("TXA");
            }
            A = X;
            GeneralFlagHelper(A);
            cycleDelayCounter = 2;
            PC += 1;
        }

        private void TAX()  // transfer accumulator to X
        {
            if (DEBUG)
            {
                Console.WriteLine("TAX");
            }
            X = A;
            GeneralFlagHelper(X);
            cycleDelayCounter = 2;
            PC += 1;
        }

        private void TYA()  // transfer Y to accumulator
        {
            if (DEBUG)
            {
                Console.WriteLine("TYA");
            }
            A = Y;
            GeneralFlagHelper(A);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
            PC += 1;
        }

        private void TAY()  // transfer accumulator to Y
        {
            if (DEBUG)
            {
                Console.WriteLine("TAY");
            }
            Y = A;
            GeneralFlagHelper(Y);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
            PC += 1;
        }

        private void TSX()  // transfer stack pointer to X
        {
            if (DEBUG)
            {
                Console.WriteLine("TSX");
            }
            X = S;
            GeneralFlagHelper(X);
            cycleDelayCounter = 2;
            PC += 1;
        }

        private void TXS()  // transfer X to stack pointer
        {
            if (DEBUG)
            {
                Console.WriteLine("TXS");
            }
            S = X;
            cycleDelayCounter = 2;
            PC += 1;
        }

        private void INX()  // increment X by 1
        {
            if (DEBUG)
            {
                Console.WriteLine("INX");
            }
            X++;
            GeneralFlagHelper(X);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
            PC += 1;
        }

        private void INY()  // increment Y by 1
        {
            if (DEBUG)
            {
                Console.WriteLine("INY");
            }
            Y++;
            GeneralFlagHelper(Y);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
            PC += 1;
        }

        private void DEX()  // decrement X by 1
        {
            if (DEBUG)
            {
                Console.WriteLine("DEX");
            }
            X--;
            GeneralFlagHelper(X);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
            PC += 1;
        }

        private void DEY()  // decrement Y by 1
        {
            if (DEBUG)
            {
                Console.WriteLine("DEY");
            }
            Y--;
            GeneralFlagHelper(Y);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
            PC += 1;
        }


        /*
         * N receives the initial, un-ANDed value of memory bit 7.
         * V receives the initial, un-ANDed value of memory bit 6.
         * Z is set if the result of the AND is zero, otherwise reset.
         */
        private void BIT(MemoryAddressingMode addressingMode)
        {
            if (DEBUG)
            {
                Console.WriteLine("BIT");
            }
            ushort memLocation = GetMemoryAddress(addressingMode);
            SetCarryFlag((byte)(memory[memLocation] >> 7));  // set the carry flag to the 7th memory bit.
            SetOverflowFlag((byte)(memory[memLocation] >> 6));  // set the overflow flag to the 6th memory bit.
            if ((A & memory[memLocation]) == 0)
            {
                SetZeroFlag(true);  // for this one having the booleans is nice, but I suppose I could just make the byte one say anything non-zero is
                                    // true just like C... that would probably require an if statement though. 
            }
            else
            {
                SetZeroFlag(false);
            }
            switch (addressingMode)
            {
                case MemoryAddressingMode.Zero_Page:
                    cycleDelayCounter = 3;
                    break;
                case MemoryAddressingMode.Absolute:
                    cycleDelayCounter = 4;
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to BIT instruction: " + addressingMode);
            }

        }

        private void BRK()  // generates a non-maskable interrupt
        {
            if (DEBUG)
            {
                Console.WriteLine("BRK");
            }
            PC += 1;
            Console.WriteLine(A);  // for DEBUG
            // GENERATE NON MASKABLE INTERRUPT OR SOMETHING 
        }

        // BRANCH INSTRUCTIONS

        private void BNE()  // branch on result not 0  (equivelent to jnz in x86-64 I think... 351 gang rise up)
        {
            if (DEBUG)
            {
                Console.WriteLine("BNE");
            }
            PC += 2;
            cycleDelayCounter = 2;  // add 1 if branch occurs on same page, add 2 if it branches to another page.
            if (GetZeroFlag() == 0)  // we need to branch if the zero flag is not set (i.e. the result is not 0)
            {
                BranchHelper();  // perform the branch
            }
            else
            {
                cycleDelayCounter = 2;
            }
        }

        private void BEQ()
        {
            if (DEBUG)
            {
                Console.WriteLine("BEQ");
            }
            PC += 2;
            cycleDelayCounter = 2;  // add 1 if branch occurs on same page, add 2 if it branches to another page.
            if (GetZeroFlag() == 1)  // we need to branch if the zero flag is set (i.e. the result is 0)
            {
                BranchHelper();  // perform the branch
            }
            else
            {
                cycleDelayCounter = 2;
            }
        }

        private void BCC()  // branch on carry flag 0
        {
            if (DEBUG)
            {
                Console.WriteLine("BCC");
            }
            PC += 2;
            //Console.WriteLine(GetZeroFlag());
            cycleDelayCounter = 2;  // add 1 if branch occurs on same page, add 2 if it branches to another page.
            if (GetCarryFlag() == 0)  // we need to branch if the carry flag is not set
            {
                BranchHelper();  // perform the branch
            }
            else
            {
                cycleDelayCounter = 2;
            }
        }

        private void BCS()  // branch on carry flag 1
        {
            if (DEBUG)
            {
                Console.WriteLine("BCS");
            }
            PC += 2;
            //Console.WriteLine(GetZeroFlag());
            cycleDelayCounter = 2;  // add 1 if branch occurs on same page, add 2 if it branches to another page.
            if (GetCarryFlag() == 1)  // we need to branch if the carry flag is set
            {
                BranchHelper();  // perform the actual branch
            }
            else
            {
                cycleDelayCounter = 2;
            }
        }

        private void BMI()  // branch if negative flag is 1.
        {
            if (DEBUG)
            {
                Console.WriteLine("BMI");
            }
            PC += 2;
            cycleDelayCounter = 2;  // add 1 if branch occurs on same page, add 2 if it branches to another page.
            if (GetNegativeFlag() == 1)  // we need to branch if the zero flag is set (i.e. the result is 0)
            {
                BranchHelper();  // perform the branch
            }
            else
            {
                cycleDelayCounter = 2;
            }
        }

        private void BPL()  // branch if negative flag is 0.
        {
            if (DEBUG)
            {
                Console.WriteLine("BPL");
            }
            PC += 2;
            cycleDelayCounter = 2;  // add 1 if branch occurs on same page, add 2 if it branches to another page.
            if (GetNegativeFlag() == 0)  // we need to branch if the zero flag is set (i.e. the result is 0)
            {
                BranchHelper();  // perform the branch
            }
            else
            {
                cycleDelayCounter = 2;
            }
        }

        private void BVC()  // branch if overflow flag is 0 (Branch oVerflow Clear)
        {
            if (DEBUG)
            {
                Console.WriteLine("BVC");
            }
            PC += 2;
            cycleDelayCounter = 2;  // add 1 if branch occurs on same page, add 2 if it branches to another page.
            if (GetOverflowFlag() == 0)  // we need to branch if the zero flag is set (i.e. the result is 0)
            {
                BranchHelper();  // perform the branch
            }
            else
            {
                cycleDelayCounter = 2;
            }
        }

        private void BVS()  // branch if overflow flag is 1 (Branch oVerflow Set)
        {
            if (DEBUG)
            {
                Console.WriteLine("BVS");
            }
            PC += 2;
            cycleDelayCounter = 2;  // add 1 if branch occurs on same page, add 2 if it branches to another page.
            if (GetOverflowFlag() == 0)  // we need to branch if the zero flag is set (i.e. the result is 0)
            {
                BranchHelper();  // perform the branch
            }
            else
            {
                cycleDelayCounter = 2;
            }
        }

        private void BranchHelper()  // this method actually executes a branch. All the branch actions are the same, only the conditionals are different.
        {
            PC--;
            if (memory[PC] > 0x7f)
            {
                PC -= (ushort)(~memory[PC] & 0x00ff);
            }
            else
            {
                PC += (ushort)(memory[PC] & 0x00ff);
            }
            cycleDelayCounter = 3;  // it's 3 cycles if there is a jump
            Console.WriteLine("current PC is: {0:X}", PC);
        }


        // STACK INSTRUCTIONS   (stack related instructions)

        private void PHA()  // push accumulator (A) on stack
        {

            PushToStack(A);  // push our accumulator on the stack
            cycleDelayCounter = 3;  // this operation takes 3 cycles
            PC += 1;  // increment the program counter by 1.
        }

        private void PHP()  // push status register on stack
        {
            PushToStack(status);
            cycleDelayCounter = 3;
            PC += 1; 
        }

        private void PLA()  // pull accumulator from stack  (pull byte at S [topmost byte] into accumulator register A)
        {
            A = PullFromStack();
            cycleDelayCounter = 4;  // this operation takes 4 cycles
            PC += 1;
        }

        private void PLP()  // pull processor status from stack (pull byte at S [topmost byte] into status register)
        {
            status = PullFromStack();  // set the status register to the byte we just pulled off of the stack.
            cycleDelayCounter = 4;  // this operation takes 4 cycles
            PC += 1;
        }


        private void PushToStack(byte b)
        {
            ushort stackMem = (ushort)(0x0100 | S);  // generate our 16 bit actual memory address from our 8 bit stack pointer "address"
            memory[stackMem] = b;  // set the data at this memory location to be the data we want to push to the stack
            S -= 0x1;  // we only store 1 byte at a time on the stack, so after we're done storing our data we decrement the stack pointer by 1.
        }

        private byte PullFromStack()
        {
            ushort stackMem = (ushort)(0x0100 | S);  // generate our 16 bit actual memory address from our 8 bit stack pointer "address"
            byte toReturn = memory[stackMem];  // get the data from that memory address and store it in a byte that we will return
            S += 0x1;  // increment the stack pointer up by 1.
            return toReturn;  // return the byte we retrieved.
        }

        private void NOP()
        {
            PC += 1;  // no operation, just increment program counter
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
                    PC += 3; // increment program counter by 3
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    memLocation += X;
                    PC += 3;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    memLocation += Y;
                    PC += 3;
                    break;
                case MemoryAddressingMode.Immediate:
                    memLocation = (ushort)(PC + 1);
                    PC += 2;
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    memLocation = (ushort)(memory[PC + 1] + X);  // the memory address of the LSB of the memory address we want.
                    LSB = memory[memLocation];
                    MSB = memory[memLocation + 1];
                    memLocation = (ushort)(MSB << 8 | LSB);  // it's little endian? so we have to do this, maybe C#'s casting isn't so bad after all.
                    PC += 2;
                    // hope that works
                    break;
                case MemoryAddressingMode.Indirect:  // the instruction contains a 16 bit address which identifies the location of the 
                    // LSB of another 16 bit address which is the real target of the instruction (why is this even a thing?)
                    memLocation = (ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    LSB = memory[memLocation];
                    MSB = memory[memLocation + 1];
                    memLocation = (ushort)(MSB << 8 | LSB);  // it's little endian? so we have to do this, maybe C#'s casting isn't so bad after all.
                    PC += 3;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:  // different than Indexed Indirect (eeeeee)
                    //In instruction contains the zero page location of the least significant byte of 16 bit address. 
                    //The Y register is dynamically added to this value to generated the actual target address for operation.
                    memLocation = memory[PC + 1];  // zero page location
                    LSB = memory[memLocation];
                    MSB = memory[memLocation + 1];
                    memLocation = (ushort)(MSB << 8 | LSB);  // it's little endian? so we have to do this, maybe C#'s casting isn't so bad after all.
                    PC += 2;
                    break;
                case MemoryAddressingMode.Relative:  // instruction contains a signed 8 bit relative offset. Actually maybe let's not use this
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
                    PC += 2;
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    memLocation = (ushort)(memory[PC + 1] + X);
                    PC += 2;
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_Y:
                    memLocation = (ushort)(memory[PC + 1] + Y);
                    PC += 2;
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing mode");
            }
            return memLocation;
        }


        // HELPER METHODS FOR SETTING THE VARIOUS PROCESSOR FLAGS


        // overload of SetCarryFlag where the method takes a byte (0 or 1) instead of a boolean. In C#, booleans are not usable in mathematics.
        private void SetCarryFlag(byte b)
        {
            if (b > 1)
            {
                throw new ArgumentOutOfRangeException("Carry flag can only be set to 0 or 1");
            }
            status = (byte)(status & (0xFE + b));  // I think this will work.
        }

        private void SetCarryFlag(bool b)
        // this holds the carry out of the most significant bit in any arithmetic operation. 
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


        private byte GetCarryFlag()  // why are we not returning a boolean, well because we need to directly add the result of this to another value.
        {
            return (byte)(status & 0x01);
        }

        private byte GetZeroFlag()  // should this one return a boolean though because it makes more sense???? 
        {
            return (byte)((status >> 1) & 0x01);
        }

        private byte GetNegativeFlag()
        {
            return (byte)(status >> 7);
        }

        private byte GetOverflowFlag()
        {
            return (byte)((status >> 6) & 0x01);
        }

        private void SetZeroFlag(byte b)
        {
            if (b > 1)
            {
                throw new ArgumentOutOfRangeException("Zero flag can only be set to 0 or 1");
            }
            // TODO: FIGURE OUT THE BITSHIFTING VERSION OF THIS LATER
            //status = (byte)(status & ((b << 1) + 1));  <-- currently not correct

            if (b == 1)  // yeah yeah I know, it's not even a ternary operator version of this. I might come through and compress the if/else
                         // statements that I can into ternary statements, but that does affect readability so ehhh maybe not.
            {
                status = (byte)(status | 0x02);  // 0000 0010  we set the zero bit to true 
            }
            else
            {
                status = (byte)(status & 0xFD);  // 1111 1101  we set the zero bit to false
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

        private void SetSoftwareInterruptFlag(bool b)  // set when a software interrupt is executed
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

        // overload that takes an actual byte value
        private void SetOverflowFlag(byte b)
        {
            if (b > 1)
            {
                throw new ArgumentOutOfRangeException("Overflow flag can only be set to 0 or 1");
            }

            if (b == 1)  // TODO: figure out the pure bitwise version of this too instead of an if statement.
            {
                status = (byte)(status | 0x40);  // 0100 0000  we set the overflow (V) bit to true
            }
            else
            {
                status = (byte)(status & 0xBF);  // 1011 1111  we set that bit to false
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
