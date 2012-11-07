using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;
using Microsoft.WindowsAzure;

namespace TestSnapshots
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {

                string BlobURL = "deploy/Testdrivesnapshot.vhd";

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=http;AccountName=mongodbwest2;AccountKey=wZcR60wAy+zltHPV7CXJsvBo/rnZHV2FIqg+UA+H1pIhkYl4j0qRZ+GgI5V8IJhngh2DOxI+sS46KddPFWg0Xw==");
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                //Get a reference to a blob.
                CloudBlob blob = blobClient.GetBlobReference(BlobURL);

                //Take a snapshot of the blob.
                CloudBlob snapshot = blob.CreateSnapshot();

                //Get the snapshot timestamp.
                DateTime timestamp = (DateTime)snapshot.Attributes.Snapshot;

                //Use the timestamp to get a second reference to the snapshot.
                CloudBlob snapshot2 = new CloudBlob(BlobURL, timestamp, blobClient);

                CloudDrive Snapshotdrive = new CloudDrive(snapshot2.Uri, storageAccount.Credentials);
                string path = Snapshotdrive.Mount(0, DriveMountOptions.None);

                Console.WriteLine("Mounted on " + path);
                Console.ReadLine();

                Snapshotdrive.Unmount();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Exception : {0} ({1}) at {2}", ex.Message, ex.InnerException == null ? "" : ex.InnerException.Message, ex.StackTrace));
            }
            


        }
    }
}
