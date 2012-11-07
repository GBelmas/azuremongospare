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
using MongoDB.Driver;
using Microsoft.WindowsAzure.ServiceRuntime;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using System.Net;
using System.IO;
using System.Diagnostics;

namespace Helpers.Mongo
{
    public class MongoDriverHelper
    {
        #region Fields

        private MongoServer server = null;
        private MongoDatabase database = null;
        private MongoCollection collection = null; 

        #endregion

        #region Constructors

        public MongoDriverHelper(NodeEndPoint endpoint)
        {
            this.server = MongoHelper.CreateMongoServer(endpoint);
        }

        public MongoDriverHelper(NodeEndPoint endpoint, string databaseName) 
            : this(endpoint)
        {
            this.SetDatabase(databaseName);
        }

        public MongoDriverHelper(NodeEndPoint endpoint, string databaseName, string collectionName) 
            : this(endpoint, databaseName)
        {
            this.SetCollection(collectionName);
        } 

	    #endregion

        #region Configuration
        public static void RunDiskPart(char destinationDriveLetter, char mountedDriveLetter)
        {
            string diskpartFile = "diskpart.txt";

            if (File.Exists(diskpartFile))
            {
                File.Delete(diskpartFile);
            }

            string cmd = "select volume = " + mountedDriveLetter + "\r\n" + "assign letter = " + destinationDriveLetter;
            File.WriteAllText(diskpartFile, cmd);

            //start the process 
            Trace.TraceInformation("running diskpart");
            Trace.TraceInformation("using " + cmd);
            using (Process changeletter = new Process())
            {
                changeletter.StartInfo.Arguments = "/s" + " " + diskpartFile;
                changeletter.StartInfo.FileName = System.Environment.GetEnvironmentVariable("WINDIR") + "\\System32\\diskpart.exe";
                //#if !DEBUG 
                changeletter.Start();
                changeletter.WaitForExit();
                //#endif 
            }

            File.Delete(diskpartFile);
        } 
 
        /// <summary>
        /// Set the database
        /// </summary>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        public bool SetDatabase(string databaseName)
        {
            try
            {
                this.database = server.GetDatabase(databaseName);
                if (this.database == null) Console.WriteLine("database is null");
                return this.database != null ? true : false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Set the collection
        /// </summary>
        /// <param name="collectionName"></param>
        /// <returns></returns>
        public bool SetCollection(string collectionName)
        {
            try
            {
                this.collection = database.GetCollection(collectionName);
                if (this.collection == null) Console.WriteLine("collection is null");
                return this.collection != null ? true : false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        #endregion

        #region Insert

        /// <summary>
        /// Insert a document in the current collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool InsertDocument<T>(T t)
        {
            try
            {
                var result = this.collection.Insert<T>(t);

                Console.WriteLine(result.ErrorMessage);

                if (result != null && result.Ok)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Insert a batch of documents in the current collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool InsertDocumments<T>(params T[] t)
        {
            try
            {
                var result = this.collection.InsertBatch<T>(t);

                if (result != null && result.All(r => r.Ok))
                    return true;
                else
                {
                    Console.WriteLine(result.SingleOrDefault().ErrorMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        } 
        #endregion

        #region Find

        /// <summary>
        /// Find the elements match with the query in the current collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public List<T> Find<T>(string name, BsonValue value)
        {
            try
            {
                var query = Query.EQ(name, value);
                return this.collection.FindAs<T>(query).ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Return all the elements in the current collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<T> FindAll<T>()
        {
            try
            {
                return this.collection.FindAllAs<T>().ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }  

        #endregion
    }
}
