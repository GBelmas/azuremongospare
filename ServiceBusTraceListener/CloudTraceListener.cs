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
//Pas oubliez de rajouter Microsoft.ServiceBus dans les reference des projets qui utilisenet ce listener en copy Local = true

namespace vNext.AzureTools.ServiceBusTraceListener
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.ServiceModel;
    using System.ServiceModel.Description;
    using Microsoft.ServiceBus;
    using System.Text;
    using Microsoft.WindowsAzure.ServiceRuntime;

    public class CloudTraceListener : TraceListener
    {
        ChannelFactory<ITraceChannel> traceChannelFactory;
        ITraceChannel traceChannel;
        object writeMutex;
        int maxRetries = 3;

        public CloudTraceListener()
        {
            try
            {
                string servicePath = ConfigurationManager.AppSettings["CloudTraceServicePath"];
                string serviceNamespace = ConfigurationManager.AppSettings["CloudTraceServiceNamespace"];
                string issuerName = ConfigurationManager.AppSettings["CloudTraceIssuerName"];
                string issuerSecret = ConfigurationManager.AppSettings["CloudTraceIssuerSecret"];

                Initialize(servicePath, serviceNamespace, issuerName, issuerSecret);
            }
            catch (Exception) { }
            catch { }
        }

        public CloudTraceListener(string servicePath, string serviceNamespace, string issuerName, string issuerSecret)
        {
            Initialize(servicePath, serviceNamespace, issuerName, issuerSecret);
        }

        void Initialize(string servicePath, string serviceNamespace, string issuerName, string issuerSecret)
        {
            this.writeMutex = new object();

            //Construct a Service Bus URI
            Uri uri = ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, servicePath);

            //Create a Behavior for the Credentials
            TransportClientEndpointBehavior sharedSecretServiceBusCredential = new TransportClientEndpointBehavior();
            sharedSecretServiceBusCredential.CredentialType = TransportClientCredentialType.SharedSecret;
            sharedSecretServiceBusCredential.Credentials.SharedSecret.IssuerName = issuerName;
            sharedSecretServiceBusCredential.Credentials.SharedSecret.IssuerSecret = issuerSecret;

            //Create a Channel Factory
            traceChannelFactory = new ChannelFactory<ITraceChannel>(new NetEventRelayBinding(), new EndpointAddress(uri));
            traceChannelFactory.Endpoint.Behaviors.Add(sharedSecretServiceBusCredential);
        }

        public override bool IsThreadSafe
        {
            get
            {
                return true;
            }
        }

        public override void Close()
        {
            try
            {
                this.traceChannel.Close();
                this.traceChannelFactory.Close();
            }
            catch (Exception)
            { }
            catch
            {
                //catch non-CLS exceptions
            }
        }

        private void LockWrapper(Action action)
        {
            lock (this.writeMutex)
            {
                int retry = 0;
                for (; ; )
                {
                    try
                    {
                        EnsureChannel();
                        action.Invoke();
                        return;
                    }
                    catch (Exception)
                    {
                        if (++retry > maxRetries)
                        {
                            //throw;
                            return;
                        }
                    }
                    catch
                    {
                        //catch non-CLS exception
                    }
                }
            }
        }

        public override void Write(string message)
        {
            LockWrapper(delegate { this.traceChannel.Write(message); });
        }

        public override void Write(object o)
        {
            LockWrapper(delegate { this.traceChannel.Write(o.ToString()); });
        }

        public override void Write(object o, string category)
        {
            LockWrapper(delegate { this.traceChannel.Write(o.ToString(), category); });
        }

        public override void Write(string message, string category)
        {
            LockWrapper(delegate { this.traceChannel.Write(message, category); });
        }

        public override void WriteLine(string message)
        {
            LockWrapper(delegate { this.traceChannel.WriteLine(message); });
        }

        public override void WriteLine(object o)
        {
            LockWrapper(delegate { this.traceChannel.WriteLine(o.ToString()); });
        }

        public override void WriteLine(object o, string category)
        {
            LockWrapper(delegate { this.traceChannel.WriteLine(o.ToString(), category); });
        }

        public override void WriteLine(string message, string category)
        {
            LockWrapper(delegate { this.traceChannel.WriteLine(message, category); });
        }

        public override void Fail(string message)
        {
            LockWrapper(delegate { this.traceChannel.Fail(message); });
        }

        public override void Fail(string message, string detailMessage)
        {
            LockWrapper(delegate { this.traceChannel.Fail(message, detailMessage); });
        }

        void EnsureChannel()
        {
            if (this.traceChannel != null &&
                this.traceChannel.State == CommunicationState.Opened)
            {
                return;
            }
            else
            {
                if (this.traceChannel != null)
                {
                    this.traceChannel.Abort();
                    this.traceChannel = null;
                }

                this.traceChannel = this.traceChannelFactory.CreateChannel();
                this.traceChannel.Open();
            }
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            LockWrapper(delegate { this.traceChannel.WriteAzureLog(eventCache, source, eventType, RoleEnvironment.DeploymentId, RoleEnvironment.CurrentRoleInstance.Role.Name, RoleEnvironment.CurrentRoleInstance.Id, ""); });
        }


        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            LockWrapper(delegate { this.traceChannel.WriteAzureLog(eventCache, source, eventType, RoleEnvironment.DeploymentId, RoleEnvironment.CurrentRoleInstance.Role.Name, RoleEnvironment.CurrentRoleInstance.Id, message); });
        }

    }
}
