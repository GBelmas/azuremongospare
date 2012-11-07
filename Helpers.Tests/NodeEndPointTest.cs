using Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Helpers.Tests
{
    
    
    /// <summary>
    ///This is a test class for NodeEndPointTest and is intended
    ///to contain all NodeEndPointTest Unit Tests
    ///</summary>
    [TestClass()]
    public class NodeEndPointTest
    {


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion


        /// <summary>
        ///A test for Equals
        ///</summary>
        [TestMethod()]
        public void EqualsTest()
        {
            NodeEndPoint n1 = new NodeEndPoint();
            NodeEndPoint n2 = new NodeEndPoint();

            Assert.AreEqual(new NodeEndPoint("www.google.com",80), new NodeEndPoint("www.google.com",80) );
            Assert.AreEqual(new NodeEndPoint("www.google.com ", 80), new NodeEndPoint("www.google.com", 80));
            Assert.AreEqual(new NodeEndPoint(" www.google.com ", 80), new NodeEndPoint("www.google.com", 80));
            Assert.AreEqual(new NodeEndPoint(" www.google.com ", 80), new NodeEndPoint(" www.google.com", 80));
            Assert.AreEqual(new NodeEndPoint(" www.google.com", 80), new NodeEndPoint(" www.google.com", 80));
            Assert.AreNotEqual(new NodeEndPoint("www.google.com", 81), new NodeEndPoint("www.google.com", 80));
            Assert.AreNotEqual(new NodeEndPoint("www.goXogle.com", 80), new NodeEndPoint("www.google.com", 80));
            Assert.AreEqual(new NodeEndPoint("www.GOOGLE.com", 80), new NodeEndPoint("www.google.com", 80));
        }
    }
}
