﻿#region Copyright
// Copyright Hitachi Consulting
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xigadee
{
    /// <summary>
    /// This abstract harness holds a message listener and allows unit testing to be performed on it.
    /// </summary>
    /// <typeparam name="L">The listener type.</typeparam>
    public abstract class ListenerHarness<L>: MessagingHarness<L>
        where L:class, IListener, IService, IRequireSharedServices
    {
        /// <summary>
        /// This override sets the priority partitions.
        /// </summary>
        /// <param name="service">The service.</param>
        protected override void Configure(L service)
        {
            base.Configure(service);

            service.PriorityPartitions = PriorityPartitions;
        }

        /// <summary>
        /// This method starts the listener and prioritises the clients.
        /// </summary>
        public override void Start()
        {
            base.Start();
            Clients = new ClientPriorityCollection(new IListener[] { Service }.ToList(), null, PollAlgorithm,0);
        }
        /// <summary>
        /// This is the poll algorithm.
        /// </summary>
        public virtual IListenerClientPollAlgorithm PollAlgorithm => new MultipleClientPollSlotAllocationAlgorithm();
        /// <summary>
        /// This is the client priority collection.
        /// </summary>
        public ClientPriorityCollection Clients { get; set; }

        #region PriorityPartitions
        /// <summary>
        /// This is the set of default priority partitions. Override if you wish to change.
        /// </summary>
        public virtual List<ListenerPartitionConfig> PriorityPartitions => ListenerPartitionConfig.Init(0, 1).ToList();
        #endregion
    }
}
