//Copyright (c) <2012>, Kobojo©, Vnext
//All rights reserved.

//Redistribution and use in source and binary forms, with or without
//modification, are permitted provided that the following conditions are met:
//1. Redistributions of source code must retain the above copyright
//   notice, this list of conditions and the following disclaimer.
//2. Redistributions in binary form must reproduce the above copyright
//   notice, this list of conditions and the following disclaimer in the
//   documentation and/or other materials provided with the distribution.
//3. All advertising materials mentioning features or use of this software
//   must display the following acknowledgement:
//   This product includes software developed by the Kobojo©, VNext.
//4. Neither the name of the Kobojo©, VNext nor the
//   names of its contributors may be used to endorse or promote products
//   derived from this software without specific prior written permission.

//THIS SOFTWARE IS PROVIDED BY Kobojo©, VNext ''AS IS'' AND ANY
//EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
//WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
//DISCLAIMED. IN NO EVENT SHALL Kobojo©, VNext BE LIABLE FOR ANY
//DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
//(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
//LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
//ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
//(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Timers;
using System.IO;
using MongoDB.Driver.Builders;

namespace MongoAttacker
{
    class Program
    {
        private static string filename = Path.Combine(Environment.CurrentDirectory, DateTime.Now.Ticks.ToString() + ".txt");
        private static int insertCount = 0;

        static void Main(string[] args)
        {
            try
            {
                string url = "65.52.224.21:10000";

                var fs = File.Create(filename);
                fs.Close();

                Timer timer = new Timer(5000);
                timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
                timer.Start();

                for (int i = new Random().Next(0, 1000000); i >= 0; i++)
                {
                    try
                    {
                        var server = MongoServer.Create(string.Format("mongodb://{0}/?slaveOk=true", url));
                        server.Connect();

                        var db = server.GetDatabase("test");
                        var collection = db.GetCollection<BsonDocument>("numbers");
                        collection.Insert(new BsonDocument()
                        {
                            { i.ToString(), @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur venenatis, lectus viverra blandit faucibus, eros risus adipiscing ipsum, at iaculis magna neque in nibh. Nulla venenatis, purus in imperdiet aliquet, ligula ligula gravida lorem, et egestas justo tellus cursus elit. Nam ut metus leo. In et odio et ligula auctor vulputate. Nunc posuere erat at tortor molestie gravida. Vestibulum sodales nunc pharetra tellus aliquam et blandit risus suscipit. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos himenaeos. Proin erat leo, eleifend vitae hendrerit sollicitudin, eleifend vel neque. Sed et sapien nunc, at adipiscing dolor. Sed luctus mollis arcu, eu adipiscing risus suscipit nec. Aenean eu elit lacus, in dapibus sem. Aliquam est dui, porta vitae fringilla eget, tristique in neque. Aliquam eget nisl ac tellus sodales viverra. Nam mauris mauris, ornare placerat aliquet id, eleifend et mauris. Fusce iaculis condimentum aliquet. Nunc dictum massa nunc." }
                        });

                        insertCount++;

                        var query = Query.EQ(i.ToString(), @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur venenatis, lectus viverra blandit faucibus, eros risus adipiscing ipsum, at iaculis magna neque in nibh. Nulla venenatis, purus in imperdiet aliquet, ligula ligula gravida lorem, et egestas justo tellus cursus elit. Nam ut metus leo. In et odio et ligula auctor vulputate. Nunc posuere erat at tortor molestie gravida. Vestibulum sodales nunc pharetra tellus aliquam et blandit risus suscipit. Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos himenaeos. Proin erat leo, eleifend vitae hendrerit sollicitudin, eleifend vel neque. Sed et sapien nunc, at adipiscing dolor. Sed luctus mollis arcu, eu adipiscing risus suscipit nec. Aenean eu elit lacus, in dapibus sem. Aliquam est dui, porta vitae fringilla eget, tristique in neque. Aliquam eget nisl ac tellus sodales viverra. Nam mauris mauris, ornare placerat aliquet id, eleifend et mauris. Fusce iaculis condimentum aliquet. Nunc dictum massa nunc.");
                        foreach (BsonDocument number in collection.Find(query))
                        {
                            var element = number.Elements.ToList()[1];
                            Console.WriteLine(string.Format("Name: {0} - Value : {1}[...]", element.Name, element.Value.AsString.Substring(0, 40)));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("erreur");
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            catch (Exception exx)
            {
                Console.WriteLine(exx.Message);
            }
            finally
            {
                Console.WriteLine("Appuyez sur une touche pour quitter");
                Console.ReadLine();
            }
        }

        static void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var fs = File.OpenWrite(filename);
                var data = Encoding.UTF8.GetBytes(insertCount.ToString());
                fs.Write(data, 0, data.Length);
                fs.Flush();
                fs.Close();
        }
    }
}
