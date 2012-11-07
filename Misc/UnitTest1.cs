using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using System.Net;

namespace Misc
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            BsonArray membersDoc = new BsonArray();

            List<IPEndPoint> nodes = new List<IPEndPoint>();
            nodes.Add(new IPEndPoint(IPAddress.Parse("10.0.0.1"), 10000));
            nodes.Add(new IPEndPoint(IPAddress.Parse("10.0.0.2"), 10000));
            nodes.Add(new IPEndPoint(IPAddress.Parse("10.0.0.3"), 10000));

            // Get all the instances of the replica
            int serverId = 0;
            foreach (IPEndPoint node in nodes)
            {

                string host = string.Format("{0}:{1}", node.Address, node.Port);
                BsonDocument nodeDoc;
                
                if (serverId != 2)
                {
                    //Normal member
                    nodeDoc = new BsonDocument { { "_id", serverId }, { "host", host } };
                }
                else
                {
                    //Hidden member
                    nodeDoc = new BsonDocument { { "_id", serverId }, { "host", host }, { "buildIndexes", "false" }, { "hidden", "true" }, { "priority", "0" } };
                }


                membersDoc.Add(nodeDoc);

                serverId++;
            }

            var configDoc = new BsonDocument { { "_id", "replica1" }, { "members", membersDoc } };

            //rs.initiate ({_id : "replica1",members : [{ _id : 0, host : "10.61.92.137:20003" },{ _id : 1, host : "10.61.82.87:20003" },{ _id : 2, host : "10.61.80.121:20003", buildIndexes : false, hidden : true, priority: 0  } ] })
            string s = configDoc.ToString();
        }
    }
}
