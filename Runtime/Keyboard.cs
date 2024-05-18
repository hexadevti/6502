﻿using System.Text;
using Runtime.Abstractions;

namespace Runtime;

public class Keyboard : IOverLay
{
     
    public Keyboard()
    {
        Start = 0xc000;
        End = 0xc000;
    }

    public int Start { get; }
    public int End { get; }
    public void Write(ushort address, byte b)
    {
    }

    public byte Read(ushort address, Memory memory)
    {
        return memory.KeyPressed;
    }
}