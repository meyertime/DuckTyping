using System;
using System.Collections.Generic;
using System.Text;

namespace DeftTech.DuckTyping.Tests
{
    public interface IAdder
    {
        int Add(int arg1, int arg2);
        int LastTotal { get; set; }
    }
}
