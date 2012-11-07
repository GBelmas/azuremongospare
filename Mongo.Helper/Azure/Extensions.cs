using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;
using System.Diagnostics;

namespace Helpers.Azure
{
    /// <summary>
    /// Adds some extensions to the <see cref="CloudBlob"/> class.
    /// </summary>
    public static class BlobExtensions
    {
        /// <summary>
        /// Checks if the given <see cref="CloudBlob">blob</see> exists.
        /// </summary>
        /// <param name="blob">Blob to check.</param>
        /// <returns>True if exists, otherwise return false.</returns>
        /// <exception cref="StorageClientException">Throw the exception if <see cref="StorageErrorCode"/> different to ResourceNotFound.s</exception>
        public static bool Exists(this CloudBlob blob)
        {
            try
            {
                blob.FetchAttributes();
                return true;
            }
            catch (StorageClientException e)
            {
                if (e.ErrorCode == StorageErrorCode.ResourceNotFound)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }       
    }

}
