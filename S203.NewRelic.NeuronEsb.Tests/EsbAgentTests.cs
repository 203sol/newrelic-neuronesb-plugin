using System;
using NUnit.Framework;

namespace S203.NewRelic.NeuronEsb.Tests
{
    [TestFixture]
    class EsbAgentTests
    {
        [Test]
        public void Ctor_Null_Argument_Name()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var agent = new EsbAgent(null, "", 0, "");
            });
        }

        [Test]
        public void Ctor_Null_Argument_Host()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var agent = new EsbAgent("", null, 0, "");
            });
        }

        [Test]
        public void Ctor_Null_Argument_Instance()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var agent = new EsbAgent("", "", 0, null);
            });
        }
    }
}
