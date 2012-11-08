using System;
using System.Collections.Generic;
using System.Text;

namespace DeftTech.DuckTyping.Tests
{
    public static class StaticAdder
    {
        private static int _LastTotal = -1;

        public static int Add(int arg1, int arg2)
        {
            int result = arg1 + arg2;

            _LastTotal = result;
            return result;
        }

        public static int LastTotal
        {
            get { return _LastTotal; }
            set { _LastTotal = value; }
        }
    }
}
