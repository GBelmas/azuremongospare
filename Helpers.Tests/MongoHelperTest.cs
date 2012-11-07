using Helpers.Mongo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Collections.Generic;
using System.Net;

namespace Helpers.Tests
{
    
    
    /// <summary>
    ///This is a test class for MongoHelperTest and is intended
    ///to contain all MongoHelperTest Unit Tests
    ///</summary>
    [TestClass()]
    public class MongoHelperTest
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
        ///A test for GetShardConfigurationFromMongoC
        ///</summary>
        [TestMethod()]
        public void GetShardConfigurationFromMongoCTest()
        {
            List<ShardNode> actual = MongoHelper.GetShardConfigurationFromMongoC(new  NodeEndPoint("127,0,0,30",30020));
        }

        /// <summary>
        ///A test for CompareShardConfig
        ///</summary>
        [TestMethod()]
        [DeploymentItem("Helpers.dll")]
        public void CompareShardConfigTest()
        {
            List<ShardNode> InternalConfig = new List<ShardNode>();
            List<ShardNode> ExpectedConfig = new List<ShardNode>();

            //Fresh starting mongoc => no need
            InternalConfig = new List<ShardNode>();
            ExpectedConfig = new List<ShardNode>();
            Assert.AreEqual(false, MongoHelper_Accessor.CompareShardConfig(InternalConfig, ExpectedConfig));

            //Fresh starting mongoc again => no need
            InternalConfig = new List<ShardNode>();
            ExpectedConfig = new List<ShardNode>();
            ExpectedConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.12:30000,192.168.1.4:30000" });
            ExpectedConfig.Add(new ShardNode() { ID = "goodone2", Host = "goodone2/10.2.3.4:30000,10.2.3.5:30000,10.2.3.6:30000" });
            ExpectedConfig.Add(new ShardNode() { ID = "goodone3", Host = "goodone3/172.16.10.10:30000,172.16.10.11:30000,172.16.10.12:30000" });
            Assert.AreEqual(false, MongoHelper_Accessor.CompareShardConfig(InternalConfig, ExpectedConfig));


            //1 unreferenced in mongoc => no need
            InternalConfig = new List<ShardNode>();
            InternalConfig.Add(new ShardNode(){ ID="whoareyoureplicaset", Host="whoareyoureplicaset/120.0.0.1:20000,120.0.0.1:20001,120.0.0.1:20002"});
            InternalConfig.Add(new ShardNode(){ ID="goodone", Host="goodone/192.168.1.2:30000,192.168.1.3:30000,192.168.1.4:30000"});
            ExpectedConfig = new List<ShardNode>();
            ExpectedConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.3:30000,192.168.1.4:30000" });
            Assert.AreEqual(false, MongoHelper_Accessor.CompareShardConfig(InternalConfig, ExpectedConfig));

            //same numbers 1 different IP => need
            InternalConfig = new List<ShardNode>();
            InternalConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.3:30000,192.168.1.4:30000" });
            ExpectedConfig = new List<ShardNode>();
            ExpectedConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.12:30000,192.168.1.4:30000" });
            Assert.AreEqual(true, MongoHelper_Accessor.CompareShardConfig(InternalConfig, ExpectedConfig));


            //same numbers 3 different IPs => need
            InternalConfig = new List<ShardNode>();
            InternalConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.3:30000,192.168.1.4:30000" });
            InternalConfig.Add(new ShardNode() { ID = "goodone2", Host = "goodone2/192.168.2.2:30000,192.168.2.3:30000,192.168.2.4:30000" });
            ExpectedConfig = new List<ShardNode>();
            ExpectedConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.12:30000,192.168.1.4:30000" });
            ExpectedConfig.Add(new ShardNode() { ID = "goodone2", Host = "goodone2/10.2.3.4:30000,10.2.3.5:30000,10.2.3.6:30000" });
            Assert.AreEqual(true, MongoHelper_Accessor.CompareShardConfig(InternalConfig, ExpectedConfig));

            //mongoc not fully aware of future shards => no need
            InternalConfig = new List<ShardNode>();
            InternalConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.3:30000,192.168.1.4:30000" });
            InternalConfig.Add(new ShardNode() { ID = "goodone2", Host = "goodone2/192.168.2.2:30000,192.168.2.3:30000,192.168.2.4:30000" });
            ExpectedConfig = new List<ShardNode>();
            ExpectedConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.3:30000,192.168.1.4:30000" });
            ExpectedConfig.Add(new ShardNode() { ID = "goodone2", Host = "goodone2/192.168.2.2:30000,192.168.2.3:30000,192.168.2.4:30000" });
            ExpectedConfig.Add(new ShardNode() { ID = "goodone3", Host = "goodone3/172.16.10.10:30000,172.16.10.11:30000,172.16.10.12:30000" });
            Assert.AreEqual(false, MongoHelper_Accessor.CompareShardConfig(InternalConfig, ExpectedConfig));

            //same numbers 1 rotating IP => no need
            InternalConfig = new List<ShardNode>();
            InternalConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.3:30000,192.168.1.4:30000" });
            ExpectedConfig = new List<ShardNode>();
            ExpectedConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.4:30000,192.168.1.3:30000" });
            Assert.AreEqual(false, MongoHelper_Accessor.CompareShardConfig(InternalConfig, ExpectedConfig));

            //mongoc not fully aware of future shards and IPs all mixed up => no need
            InternalConfig = new List<ShardNode>();
            InternalConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.2:30000,192.168.1.3:30000,192.168.1.4:30000" });
            InternalConfig.Add(new ShardNode() { ID = "goodone2", Host = "goodone2/192.168.2.2:30000,192.168.2.3:30000,192.168.2.4:30000" });
            ExpectedConfig = new List<ShardNode>();
            ExpectedConfig.Add(new ShardNode() { ID = "goodone", Host = "goodone/192.168.1.3:30000,192.168.1.2:30000,192.168.1.4:30000" });
            ExpectedConfig.Add(new ShardNode() { ID = "goodone2", Host = "goodone2/192.168.2.3:30000,192.168.2.4:30000,192.168.2.2:30000" });
            ExpectedConfig.Add(new ShardNode() { ID = "goodone3", Host = "goodone3/172.16.10.10:30000,172.16.10.11:30000,172.16.10.12:30000" });
            Assert.AreEqual(false, MongoHelper_Accessor.CompareShardConfig(InternalConfig, ExpectedConfig));

        }
    }
}
