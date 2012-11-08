using System;
using System.Collections.Generic;
using System.Text;

using NUnit.Framework;
using System.ComponentModel;
using System.Threading;
using System.Reflection;

namespace DeftTech.DuckTyping.Tests
{
    [TestFixture]
    public class DuckTypingTestFixture
    {
        public delegate object GeneralDelegate(object arg);
        public delegate bool SpecializedDelegate(string s);

        private object m_Sender;
        private EventArgs m_EventArgs;

        public DuckTypingTestFixture()
        { }

        [Test]
        public void TestCanCast()
        {
            Duck duck = new Duck();
            IInterface proxy = DuckTyping.Cast<IInterface>(duck);

            Assert.IsTrue(DuckTyping.CanCast<IInterface, Duck>(), "CanCast should have returned true.");
            Assert.IsTrue(DuckTyping.CanCast<IInterface>(duck), "CanCast should have returned true.");
            Assert.IsTrue(DuckTyping.CanCast<IInterface>(typeof(Duck)), "CanCast should have returned true.");
            Assert.IsTrue(DuckTyping.CanCast(typeof(IInterface), duck), "CanCast should have returned true.");
            Assert.IsTrue(DuckTyping.CanCast(typeof(IInterface), typeof(Duck)), "CanCast should have returned true.");

            Assert.IsTrue(DuckTyping.CanCast<Duck, IInterface>(), "CanCast should have returned true.");

            Assert.IsFalse(DuckTyping.CanCast<IFormattable>(duck), "CanCast should have returned false.");


            Assert.IsTrue(DuckTyping.CanCast<GeneralDelegate, SpecializedDelegate>(), "CanCast should have returned true.");
            Assert.IsTrue(DuckTyping.CanCast<SpecializedDelegate, GeneralDelegate>(), "CanCast should have returned true.");
            Assert.IsFalse(DuckTyping.CanCast<GeneralDelegate, EventHandler>(), "CanCast should have returned false.");


            Assert.IsTrue(DuckTyping.CanCast<AttributeTargets, string>(), "CanCast should have returned true.");
            Assert.IsTrue(DuckTyping.CanCast<string, AttributeTargets>(), "CanCast should have returned true.");
            Assert.IsFalse(DuckTyping.CanCast<AttributeTargets, DateTime>(), "CanCast should have returned false.");
        }

        [Test]
        public void TestPrepareCast()
        {
            string proxyAssemblyName = "DuckInterfaceProxy_" + typeof(IPrepareCastInterface).Name.Replace(".", "_").Replace("+", "-") + "_" + typeof(PrepareCastDuck).Name.Replace(".", "_").Replace("+", "-") + ".dll";

            Assert.IsNull(FindAssembly(proxyAssemblyName), "Proxy assembly has already been generated, so PrepareCast test cannot be done.");

            DuckTyping.PrepareCast<IPrepareCastInterface, PrepareCastDuck>();

            Assert.IsNotNull(FindAssembly(proxyAssemblyName), "PrepareCast did not result in a proxy assembly being generated.");  
        }

        [Test]
        public void TestInterfaceCast()
        {
            Duck duck = new Duck();

            IInterface proxy = DuckTyping.Cast<IInterface>(duck);

            Assert.IsNotNull(proxy, "DuckTyping.Cast<IInterface>(duck) return null.");
            Assert.IsTrue(proxy is IDuckProxy, "Cast did not return a proxy.");
            Assert.AreEqual(duck, ((IDuckProxy)proxy).UnwrapDuck(), "Cast returned a proxy that refers to the wrong duck object.");
            Assert.AreEqual(duck, DuckTyping.Cast<Duck>(proxy), "Reverse cast returned the wrong value.");
        }

        [Test]
        public void TestGenericVarianceCast()
        {
            List<string> list = new List<string>();
            IEnumerable<object> enumerable = DuckTyping.Cast<IEnumerable<object>>(list);
        }

        [Test]
        public void TestDelegateCast()
        {
            SpecializedDelegate specializedDelegate = new SpecializedDelegate(this.SpecializedDelegateMethod);
            GeneralDelegate generalDelegate = DuckTyping.Cast<GeneralDelegate>(specializedDelegate);
            Assert.IsTrue(generalDelegate.Target is IDuckProxy, "Cast did not return a delegate to a proxy.");
            Assert.AreEqual(specializedDelegate, ((IDuckProxy)(generalDelegate.Target)).UnwrapDuck(), "Cast returned a delegate to a proxy that does not refer to the original delegate.");
            Assert.AreEqual(specializedDelegate, DuckTyping.Cast<SpecializedDelegate>(generalDelegate), "Reverse cast returned the wrong value.");
            bool b = (bool)(generalDelegate("true"));

            generalDelegate = new GeneralDelegate(this.GeneralDelegateMethod);
            specializedDelegate = DuckTyping.Cast<SpecializedDelegate>(generalDelegate);
            Assert.IsTrue(specializedDelegate.Target is IDuckProxy, "Cast did not return a delegate to a proxy.");
            Assert.AreEqual(generalDelegate, ((IDuckProxy)(specializedDelegate.Target)).UnwrapDuck(), "Cast returned a delegate to a proxy that does not refer to the original delegate.");
            Assert.AreEqual(generalDelegate, DuckTyping.Cast<GeneralDelegate>(specializedDelegate), "Reverse cast returned the wrong value.");
            specializedDelegate("true");
        }

        [Test]
        public void TestEnumCast()
        {
            Assert.AreEqual(AttributeTargets.All, DuckTyping.Cast<AttributeTargets>("All"), "Cast from a string to an enumeration type returned the wrong value.");
            Assert.AreEqual("All", DuckTyping.Cast<string>(AttributeTargets.All), "Cast from an enumeration type to a string returned the wrong value.");
        }

        [Test]
        public void TestStaticInterfaceCast()
        {
            IAdder adder = DuckTyping.StaticCast<IAdder>(typeof(StaticAdder));

            Assert.AreEqual(4, adder.Add(2, 2));
            Assert.AreEqual(4, adder.LastTotal);

            adder.LastTotal = 15;
            Assert.AreEqual(15, adder.LastTotal);
        }

        [Test]
        public void TestInterfaceProxyEquals()
        {
            Duck duck1 = new Duck();
            Duck duck2 = new Duck();
            IInterface proxy1a = DuckTyping.Cast<IInterface>(duck1);
            IInterface proxy1b = DuckTyping.Cast<IInterface>(duck1);
            IInterface proxy2 = DuckTyping.Cast<IInterface>(duck2);

            Assert.IsTrue(proxy1a.Equals(proxy1b), "Interface proxy does not properly forward calls to the Equals method.");
            Assert.IsTrue(!proxy1a.Equals(proxy2), "Interface proxy overrides the Equals method improperly.");

            Assert.AreEqual(proxy1a.GetHashCode(), duck1.GetHashCode(), "Interface proxy does not properly forward calls to the GetHashCode method.");
        }

        [Test]
        public void TestInterfaceProxyToString()
        {
            Duck duck = new Duck();
            IInterface proxy = DuckTyping.Cast<IInterface>(duck);

            Assert.AreEqual(duck.ToString(), proxy.ToString(), "Interface proxy does not properly forward calls to ToString method.");
        }

        [Test]
        public void TestInterfaceProxyMethod()
        {
            IInterface duck = DuckTyping.Cast<IInterface>(new Duck());

            duck.Method();
            
            duck.Method(Duck.A, Duck.B, Duck.C, Duck.D);

            string stringValue = "String value.";
            Assert.AreEqual(stringValue, duck.Method(stringValue), "Method returned wrong string value.");
            Assert.IsNull(duck.Method(null), "Passing and returning a null value failed.");

            int intValue = 5;
            Assert.AreEqual(intValue, duck.Method(intValue), "Method returned wrong int value.");

            Assert.IsTrue(duck.BestMatchMethod(stringValue), "When generating the proxy class, the best matching method overload on the duck class was not chosen to forward calls to.");
        }

        [Test]
        public void TestInterfaceProxyGenericMethod()
        {
            IInterface duck = DuckTyping.Cast<IInterface>(new Duck());

            Assert.AreEqual("A", duck.GenericMethod<string>("A"), "Generic method returned wrong value.");
        }

        [Test]
        public void TestInterfaceProxyMethodVariance()
        {
            Duck duck = new Duck();
            IInterface proxy = DuckTyping.Cast<IInterface>(duck);

            string stringValue = "String value.";
            int intValue = 5;

            Assert.AreEqual(stringValue, proxy.ContravariantMethod(stringValue), "Contravariant method returned wrong value.");
            Assert.IsNull(proxy.ContravariantMethod(null), "Passing and returning a null value to and from a contravariant method failed.");

            try
            {
                proxy.ContravariantMethod(new object[0]);

                Assert.Fail("Passing a non-string object through a contravariant method that takes a string parameter should have thrown an InvalidCastException.");
            }
            catch (InvalidCastException)
            { }

            try
            {
                proxy.ContravariantValueMethod(7);

                Assert.Fail("Passing a non-DateTime object through a contravariant method that takes a DateTime parameter should have thrown an InvalidCastException.");
            }
            catch (InvalidCastException)
            { }

            try
            {
                proxy.ContravariantValueMethod(null);

                Assert.Fail("Passing null through a contravariant method that takes a value type parameter should have thrown a NullReferenceException.");
            }
            catch (NullReferenceException)
            { }

            Assert.AreEqual("Method", proxy.ContravariantEnumMethod("Method"), "Contravariant method returned wrong enum value.");

            try
            {
                proxy.ContravariantEnumMethod(null);

                Assert.Fail("Passing null through a contravariant method that taks an enum parameter should have thrown a NullReferenceException.");
            }
            catch (NullReferenceException)
            { }

            Assert.AreEqual(stringValue, proxy.CovariantMethod(stringValue), "Covariant method returned wrong value.");
            Assert.IsNull(null, proxy.CovariantMethod(null), "Passing and returning a null value to and from a covariant method failed.");
            Assert.AreEqual(intValue, proxy.CovariantValueMethod(intValue), "Covariant method returned wrong int value.");
            Assert.AreEqual(AttributeTargets.Class, proxy.CovariantEnumMethod(AttributeTargets.Class), "Covariant method returned wrong enum value.");

            // Note: This next line is primarily to check whether the system handles a recursive cast properly.  If it doesn't,
            // a StackOverflowException will be thrown.
            Assert.IsTrue(proxy.Equals(proxy.VariantByRecursiveCastMethod(duck)), "Variant by recursive cast method returned wrong value.");

            Assert.AreEqual(duck, proxy.VariantByUncastMethod(proxy), "Variant by uncast method returned wrong value.");
        }

        [Test]
        public void TestInterfaceProxyEvent()
        {
            Duck duck = new Duck();
            IInterface proxy = DuckTyping.Cast<IInterface>(duck);

            object sender = this;
            AddingNewEventArgs e = new AddingNewEventArgs();

            EventHandler eventHandler = new EventHandler(this.EventHandlerMethod);
            AddingNewEventHandler addingNewEventHandler = new AddingNewEventHandler(this.AddingNewEventHandlerMethod);

            m_Sender = null;
            m_EventArgs = null;
            proxy.Event += eventHandler;
            duck.RaiseEvent(sender, e);
            Assert.AreEqual(sender, m_Sender, "Proxy class did not properly forward adding of an event handler.");

            m_Sender = null;
            m_EventArgs = null;
            proxy.Event -= eventHandler;
            duck.RaiseEvent(sender, e);
            Assert.IsNull(m_Sender, "Proxy class did not properly forward removing of an event handler.");

            m_Sender = null;
            m_EventArgs = null;
            proxy.CovariantEvent += addingNewEventHandler;
            duck.RaiseCovariantEvent(sender, e);
            Assert.AreEqual(sender, m_Sender, "Proxy class did not properly forward adding of an event handler to a covariant event.");

            m_Sender = null;
            m_EventArgs = null;
            proxy.CovariantEvent -= addingNewEventHandler;
            duck.RaiseCovariantEvent(sender, e);
            Assert.IsNull(m_Sender, "Proxy class did not properly forward removing of an event handler from a covariant event.");

            m_Sender = null;
            m_EventArgs = null;
            proxy.ContravariantEvent += eventHandler;
            duck.RaiseContravariantEvent(sender, e);
            Assert.AreEqual(sender, m_Sender, "Proxy class did not properly forward adding of an event handler to a contravariant event.");

            m_Sender = null;
            m_EventArgs = null;
            proxy.ContravariantEvent -= eventHandler;
            duck.RaiseContravariantEvent(sender, e);
            Assert.IsNull(m_Sender, "Proxy class did not properly forward removing of an event handler from a contravariant event.");
        }

        [Test]
        public void TestInterfaceProxyProperty()
        {
            Duck duck = new Duck();
            IInterface proxy = DuckTyping.Cast<IInterface>(duck);

            string stringValue = "String value.";
            int intValue = 5;

            proxy.Property = stringValue;
            Assert.AreEqual(stringValue, proxy.Property, "Property not implemented correctly.");

            proxy.ValueProperty = intValue;
            Assert.AreEqual(intValue, proxy.ValueProperty, "Value type property not implemented correctly.");

            proxy[4] = stringValue;
            Assert.AreEqual(stringValue, proxy[4], "Indexed property not implemented correctly.");
        }

        private Assembly FindAssembly(string assemblyName)
        {
            Assembly assembly = null;
            
            Assembly[] assemblies = Thread.GetDomain().GetAssemblies();
            foreach (Assembly a in assemblies)
            {
                if (a.FullName.StartsWith(assemblyName))
                {
                    assembly = a;
                    break;
                }
            }

            return assembly;
        }

        private bool SpecializedDelegateMethod(string s)
        {
            return s.ToLower() == "true";
        }

        private object GeneralDelegateMethod(object arg)
        {
            return (object)(((string)arg).ToLower() == "true");
        }

        private void EventHandlerMethod(object sender, EventArgs e)
        {
            m_Sender = sender;
            m_EventArgs = e;
        }

        private void AddingNewEventHandlerMethod(object sender, AddingNewEventArgs e)
        {
            m_Sender = sender;
            m_EventArgs = e;
        }
    }
}
