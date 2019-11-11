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
        private bool initialized;   // a cheap hack, but I believe it's needed for sanity checking.
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
        public CPU(byte[] b)  // probably shouldn't use this constructor, I'll most likely remove it.
        {
            memory = new byte[65536];  // allocate 64K of "RAM" for the CPU
            Array.Copy(b, 0, memory, 0, b.Length);  // copy the passed in program into RAM if applicable.
            PC = 0;  // set the program counter to 0
            status = (byte)(status | 0x20);  // 0010 0000  we set status bit 5 to 1, as it is not used and should always contain logical 1.

            // there's actually some very specific stuff that goes down here. Apparently it looks for a memory address to jump to at a specific mem address, I should
            // probably implement that.
        }

        public CPU(byte[] b, ushort startLocation)  // unlike CHIP-8, there's no defined program data start location
        {
            memory = new byte[65536];  // allocate 64K of "RAM" for the CPU
            Array.Copy(b, 0, memory, startLocation, b.Length);  // copy the passed in program into RAM at the specified index.
            PC = 0;  // set the program counter to 0
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
            if (cycleDelayCounter > 0)  // perhaps we'll get this thing to be cycle accurate :D
            {
                cycleDelayCounter--;
                return;
            }
            // opcodes are 1 byte, but the number of additional bytes is defined by the opcode itself, so we'll have to increment the program counter by a variable number
            byte opcode = memory[PC];  // get the opcode (opcodes are only a byte, how much data is actually used per instruction depends on the instruction)
            Console.WriteLine("{0:X}",opcode);  // FOR DEBUGGING
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
                case 0x90:
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


                // SEC
                case 0x38:
                    SEC();
                    break;

                // CLC
                case 0x18:
                    CLC();
                    break;

                // TYA
                case 0x98:
                    TYA();
                    break;

                // TAY
                case 0xA8:
                    TAY();
                    break;

                // DEY
                case 0x88:
                    DEY();
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
                case 0x70:
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

                // BNE   (maybe all the conditional branches should go here)
                case 0xD0:
                    BNE();
                    break;











                default:
                    Console.WriteLine("ERROR: unknown opcode found: {0:X}", opcode);
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
                    cycleDelayCounter = 2;  // this command takes 2 cycles
                    PC += 2;
                    break;
                case MemoryAddressingMode.Zero_Page:
                    memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    X = memory[memLocation];  // load the data at that zero page memory location into X
                    cycleDelayCounter = 3;  // this command takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_Y:
                    // The value in Y is added to the specified zero page address for a sum address. The value at the sum address is used to perform the computation.
                    memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    X = memory[memLocation];
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:  // a full 16 bit address is specified
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    X = memory[memLocation];
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    X = memory[memLocation];  // same as above but we add y to the memory address
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
                    Y = memory[GetMemoryAddress(addressingMode)];  // load the next byte into the X register
                    cycleDelayCounter = 2;  // this command takes 2 cycles
                    break;
                case MemoryAddressingMode.Zero_Page:
                    memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    Y = memory[memLocation];  // load the data at that zero page memory location into X
                    cycleDelayCounter = 3;  // this command takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    // The value in Y is added to the specified zero page address for a sum address. The value at the sum address is used to perform the computation.
                    memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    Y = memory[memLocation];
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:  // a full 16 bit address is specified
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    Y = memory[memLocation];
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    Y = memory[memLocation];  // same as above but we add y to the memory address
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.  (apparently its 5 if a page boundary is crossed but idk what that means so...)
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to LDY instruction.");
            }
            GeneralFlagHelper(Y);  // let's see if we can just put this at the end of the switch statements
        }

        private void LDA(MemoryAddressingMode addressingMode)  // I can definitely shrink this guy down too, there's a lot of repeated code. 
        {
            ushort memLocation;
            switch (addressingMode)
            {
                case MemoryAddressingMode.Immediate:  // cool switching on enum
                    // okay for this instruction we load the next byte into the X register
                    A = memory[GetMemoryAddress(addressingMode)];  // load the next byte into the A register
                    cycleDelayCounter = 2;  // this command takes 2 cycles
                    break;
                case MemoryAddressingMode.Zero_Page:
                    memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    A = memory[memLocation];  // load the data at that zero page memory location into A
                    cycleDelayCounter = 3;  // this command takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    // The value in X is added to the specified zero page address for a sum address. The value at the sum address is used to perform the computation.
                    memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // hopefully we zero extend out to 16 bits like we should
                    A = memory[memLocation];
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute:  // a full 16 bit address is specified
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    A = memory[memLocation];
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:  // add 1 if page boundary crossed (eeeee)
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    A = memory[memLocation];  // same as above but we add y to the memory address
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.  (apparently its 5 if a page boundary is crossed but idk what that means so...)
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    A = memory[memLocation];  // same as above but we add y to the memory address
                    cycleDelayCounter = 4;  // this one took 4 cycles to operate on the 6502.  (apparently its 5 if a page boundary is crossed but idk what that means so...)
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    A = memory[memLocation];  // same as above but we add y to the memory address
                    cycleDelayCounter = 6;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // C# casting weirdness, it seems to rear its head a lot when coding emulators.
                    A = memory[memLocation];  // same as above but we add y to the memory address
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
            switch (addressingMode)
            {
                case MemoryAddressingMode.Zero_Page:
                    memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // let's hope this zero extends like we expect.
                    memory[memLocation] = X;  // we store X in that memory location.
                    cycleDelayCounter = 3;  // takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_Y:
                    memLocation = GetMemoryAddress(addressingMode);//memory[PC + 1];  // let's hope this zero extends like we expect.
                    memory[memLocation + Y] = X;  // we store X in that memory location + y.
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                case MemoryAddressingMode.Absolute:
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
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
                    memLocation = GetMemoryAddress(addressingMode);
                    memory[memLocation] = Y;  // we store X in that memory location.
                    cycleDelayCounter = 3;  // takes 3 cycles
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    memLocation = GetMemoryAddress(addressingMode); //memory[PC + 1];  // let's hope this zero extends like we expect.
                    memory[memLocation + X] = Y;  // we store X in that memory location + y.
                    cycleDelayCounter = 4;  // takes 4 cycles
                    break;
                case MemoryAddressingMode.Absolute:
                    memLocation = GetMemoryAddress(addressingMode);//(ushort)(memory[PC + 1] << 8 | memory[PC + 2]);  // get the 16 bit absolute mem address
                    memory[memLocation] = Y;  // we store X in that memory location + y.
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
                    throw new ArgumentException("Invalid Addressing Mode passed to STY instruction: " + addressingMode);
            }
        }

        private void ADC(MemoryAddressingMode addressingMode)
        {
            // TODO: this method needs to set V (overflow) flag when required, but ehh I'll get to that later
            ushort memLocation = GetMemoryAddress(addressingMode);
            A += GetCarryFlag();  // we add the carry flag to the accumulator in this operation.
            if (A + memory[memLocation] > 255)
            {
                SetCarryFlag(true);
                A = (byte)(A + memory[memLocation] - 255);
            }
            else
            {
                SetCarryFlag(false);
            }
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

        private void SBC(MemoryAddressingMode addressingMode)  // subtract with carry (we subtract the number from this instruction from the value in A)
        {
            ushort memLocation;
            switch (addressingMode)  // I'm noticing that I can shrink this down. I think I'll shrink the instructions down after I know they work.
            {
                case MemoryAddressingMode.Immediate:
                    memLocation = GetMemoryAddress(addressingMode);
                    SBCFlagHelper(memory[memLocation]);  // MUST REMEMBER TO PUT THIS BEFORE THE ACTUAL SUBTRACTION @-@
                    A = (byte)(A - memory[memLocation]);
                    cycleDelayCounter = 2;
                    break;
                case MemoryAddressingMode.Absolute:
                    memLocation = GetMemoryAddress(addressingMode);
                    SBCFlagHelper(memory[memLocation]);
                    A = (byte)(A - memory[memLocation]);
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Zero_Page:
                    memLocation = GetMemoryAddress(addressingMode);
                    SBCFlagHelper(memory[memLocation]);
                    A = (byte)(A - memory[memLocation]);
                    cycleDelayCounter = 3;
                    break;
                case MemoryAddressingMode.Zero_Page_Indexed_X:
                    memLocation = GetMemoryAddress(addressingMode);
                    SBCFlagHelper(memory[memLocation]);
                    A = (byte)(A - memory[memLocation]);
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_X:
                    memLocation = GetMemoryAddress(addressingMode);
                    SBCFlagHelper(memory[memLocation]);
                    A = (byte)(A - memory[memLocation]);
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Absolute_Indexed_Y:
                    memLocation = GetMemoryAddress(addressingMode);
                    SBCFlagHelper(memory[memLocation]);
                    A = (byte)(A - memory[memLocation]);
                    cycleDelayCounter = 4;
                    break;
                case MemoryAddressingMode.Indexed_Indirect:
                    memLocation = GetMemoryAddress(addressingMode);
                    SBCFlagHelper(memory[memLocation]);
                    A = (byte)(A - memory[memLocation]);
                    cycleDelayCounter = 6;
                    break;
                case MemoryAddressingMode.Indirect_Indexed:
                    memLocation = GetMemoryAddress(addressingMode);
                    SBCFlagHelper(memory[memLocation]);
                    A = (byte)(A - memory[memLocation]);
                    cycleDelayCounter = 5;
                    break;
                default:
                    throw new ArgumentException("Invalid Addressing Mode passed to STY instruction: " + addressingMode);
            }
            GeneralFlagHelper(A);  // set the proper processor flags to the proper values
        }

        private void SEC()  // set carry flag to 1.
        {
            SetCarryFlag(true);
            cycleDelayCounter = 2;  // somehow this takes two cycles
            PC += 1;
        }

        private void CLC()  // set carry flag to 0
        {
            SetCarryFlag(false);
            cycleDelayCounter = 2;
            PC += 1;
        }

        private void TYA()  // transfer Y to accumulator
        {
            A = Y;
            GeneralFlagHelper(A);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
            PC += 1;
        }

        private void TAY()  // transfer accumulator to Y
        {
            Y = A;
            GeneralFlagHelper(Y);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
            PC += 1;
        }

        private void DEY()  // decrement Y by 1
        {
            Y--;
            GeneralFlagHelper(Y);  // apparently we should do this
            cycleDelayCounter = 2;  // somehow this takes two cycles as well
            PC += 1;
        }

        private void BNE()  // branch on result not 0  (equivelent to jnz in x86-64 I think... 351 gang rise up)
        {
            cycleDelayCounter = 2;  // add 1 if branch occurs on same page, add 2 if it branches to another page.
            if (GetZeroFlag() == 1)  // we need to branch
            {
                GetMemoryAddress(MemoryAddressingMode.Relative);  // currently just using MemoryAddressingMode.Relative on GetMemoryAddress performs
                                                                  // a jump.
            }
            PC += 1;
        }

        private void pushStack()
        {

        }

        private void ADCFlagHelper(byte val)
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
                case MemoryAddressingMode.Indirect:  // the instruction contains a 16 bit address which identifies the location of the LSB of another 16 bit 
                                                     // address which is the real target of the instruction (why is this even a thing?)
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


        private byte GetCarryFlag()  // why are we not returning a boolean, well because we need to directly add the result of this to another value.
        {
            return (byte)(status & 0x01);
        }

        private byte GetZeroFlag()  // should this one return a boolean though because it makes more sense???? 
        {
            return (byte)((status >> 1) & 0x01);
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
