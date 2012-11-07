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
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Configuration;

namespace Helpers.Azure
{
    /// <summary>
    /// Helper for Azure table storage
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TableHelper<T> where T : TableServiceEntity
    {
        #region Fields
        protected TableServiceContext context;
        protected string tableName;
        #endregion Fields

        #region Public Methods
        public TableServiceContext GetContext(string configurationSettingName)
        {
            CloudTableClient svcClient = null;
            CloudStorageAccount acc = null;

            try
            {
                acc = CloudStorageAccount.FromConfigurationSetting(configurationSettingName);
            }
            catch (Exception ex)
            {
                try
                {
                    string connectionstring = ConfigurationManager.AppSettings[configurationSettingName];
                    acc = CloudStorageAccount.Parse(connectionstring);
                }
                catch (Exception e)
                {
                }
            }
            
            svcClient = acc.CreateCloudTableClient();

            svcClient.CreateTableIfNotExist(tableName);

            return svcClient.GetDataServiceContext();
        }
        #endregion Public Methods

        #region Constructors
        /// <summary>
        /// Initialize a new instance of <see cref="TableHelper"/> with the given table name and configuration name.
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="configurationSettingName"></param>
        public TableHelper(string tableName, string configurationSettingName)
        {
            this.tableName = tableName;
            context = GetContext(configurationSettingName);
        }
        #endregion Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemToAdd"></param>
        /// <returns></returns>
        public bool AddItem(T itemToAdd)
        {
            try
            {
                context.AddObject(tableName, itemToAdd);
                context.SaveChangesWithRetries();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemToUpdate"></param>
        /// <returns></returns>
        public bool UpdateItem(T itemToUpdate)
        {
            try
            {
                context.UpdateObject(itemToUpdate);
                context.SaveChangesWithRetries();
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<T> GetAllItems()
        {
            var itemsQuery = context.CreateQuery<T>(tableName);
            return itemsQuery.Execute();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        public IEnumerable<T> GetAllItems(string partitionKey)
        {
            try
            {
                var itemsQuery = context.CreateQuery<T>(tableName).Where(item => item.PartitionKey == partitionKey).ToList();
                return itemsQuery;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IQueryable<T> GetQuery()
        {
            return context.CreateQuery<T>(tableName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rowKey"></param>
        /// <returns></returns>
        public T GetItemById(string rowKey)
        {
            return GetItemById(string.Empty, rowKey);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="partitionKey"></param>
        /// <param name="rowKey"></param>
        /// <returns></returns>
        public T GetItemById(string partitionKey, string rowKey)
        {
            var query = (from item in context.CreateQuery<T>(tableName)
                         where item.RowKey.Equals(rowKey) && item.PartitionKey.Equals(partitionKey) && item.Timestamp >= DateTime.Parse("01/01/1971")
                         select item).AsTableServiceQuery();
            return query.Execute().SingleOrDefault();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rowKey"></param>
        /// <returns></returns>
        public bool DeleteItem(string rowKey)
        {
            return DeleteItem(string.Empty, rowKey);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="partitionKey"></param>
        /// <param name="rowKey"></param>
        /// <returns></returns>
        public bool DeleteItem(string partitionKey, string rowKey)
        {
            try
            {
                context.DeleteObject(this.GetItemById(partitionKey, rowKey));
                context.SaveChangesWithRetries();
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        public bool DeleteAllItems(string partitionKey)
        {
            try
            {
                bool returnBool = true;
                foreach (var item in GetAllItems(partitionKey))
                {
                    returnBool = DeleteItem(item.PartitionKey, item.RowKey);
                }
                return returnBool;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}