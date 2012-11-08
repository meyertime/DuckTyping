using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;
using System.ComponentModel;

namespace DeftTech.DuckTyping.Tests
{
    public class Duck
    {
        public static readonly object A = "A";
        public static readonly string B = "B";
        public static readonly int C = 3;
        public static readonly DateTime D = new DateTime(2007, 6, 29);

        private string m_Property;
        private int m_ValueProperty;

        private object[] m_Array;

        public Duck()
        {
            m_Array = new object[8];
        }

        public void Method()
        { }

        public void Method(object a, string b, int c, DateTime d)
        {
            Assert.AreEqual(A, a, "Passed wrong value of object parameter.");
            Assert.AreEqual(B, b, "Passed wrong value of string parameter.");
            Assert.AreEqual(C, c, "Passed wrong value of int parameter.");
            Assert.AreEqual(D, d, "Passed wrong value of DateTime parameter.");
        }

        public string Method(string s)
        {
            return s;
        }

        public int Method(int i)
        {
            return i;
        }

        public T GenericMethod<T>(T a)
        {
            return a;
        }

        public bool BestMatchMethod(object a)
        {
            return false;
        }

        public bool BestMatchMethod(string s)
        {
            return true;
        }

        public object CovariantMethod(object s)
        {
            return s;
        }

        public object CovariantValueMethod(object i)
        {
            return i;
        }

        public string CovariantEnumMethod(string target)
        {
            return target;
        }

        public string ContravariantMethod(string a)
        {
            return a;
        }

        public DateTime ContravariantValueMethod(DateTime a)
        {
            return a;
        }

        public AttributeTargets ContravariantEnumMethod(AttributeTargets target)
        {
            return target;
        }

        public object VariantByRecursiveCastMethod(IInterface a)
        {
            return a;
        }

        public object VariantByUncastMethod(Duck a)
        {
            return a;
        }

        public Duck StackOverflowCheckMethod()
        {
            return null;
        }

        public string Property 
        {
            get { return m_Property; }
            set { m_Property = value; }
        }

        public int ValueProperty
        {
            get { return m_ValueProperty; }
            set { m_ValueProperty = value; }
        }

        public object this[int index]
        {
            get { return m_Array[index]; }
            set { m_Array[index] = value; }
        }

        public event EventHandler Event;
        public event EventHandler CovariantEvent;
        public event AddingNewEventHandler ContravariantEvent;

        public void RaiseEvent(object sender, EventArgs e)
        {
            if (Event != null) Event(sender, e);
        }

        public void RaiseCovariantEvent(object sender, EventArgs e)
        {
            if (CovariantEvent != null) CovariantEvent(sender, e);
        }

        public void RaiseContravariantEvent(object sender, AddingNewEventArgs e)
        {
            if (ContravariantEvent != null) ContravariantEvent(sender, e);
        }
    }
}
