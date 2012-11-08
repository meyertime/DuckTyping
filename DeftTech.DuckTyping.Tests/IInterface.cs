using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;

namespace DeftTech.DuckTyping.Tests
{
    public interface IInterface
    {
        void Method();
        void Method(object a, string b, int c, DateTime d);
        string Method(string s);
        int Method(int i);

        T GenericMethod<T>(T a);

        bool BestMatchMethod(string s);

        string CovariantMethod(string s);
        int CovariantValueMethod(int i);
        AttributeTargets CovariantEnumMethod(AttributeTargets target);

        object ContravariantMethod(object a);
        object ContravariantValueMethod(object a);        
        string ContravariantEnumMethod(string target);

        object VariantByRecursiveCastMethod(Duck a);
        object VariantByUncastMethod(IInterface a);

        IInterface StackOverflowCheckMethod();

        string Property { get; set; }
        int ValueProperty { get; set; }

        object this[int index] { get; set; }

        event EventHandler Event;
        event AddingNewEventHandler CovariantEvent;
        event EventHandler ContravariantEvent;
    }
}
