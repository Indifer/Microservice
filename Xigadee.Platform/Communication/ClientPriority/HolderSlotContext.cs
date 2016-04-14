﻿#region using
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endregion
namespace Xigadee
{
    /// <summary>
    /// This class holds the current active slot for the client.
    /// It is used to reference the client.
    /// </summary>
    public class HolderSlotContext
    {
        internal HolderSlotContext(Guid id, long slot, string name, int reserved
            , ClientPriorityHolder holder, Action<HolderSlotContext> complete)
        {
            Id = id;
            Slot = slot;
            Name = name;
            Reserved = reserved;
            mHolder = holder;
            mComplete = complete;
        }

        public readonly Guid Id;
        public readonly long Slot;
        public readonly string Name;
        public readonly int Reserved;

        private readonly ClientPriorityHolder mHolder;
        private readonly Action<HolderSlotContext> mComplete;

        public int? Actual { get; private set; }

        #region Poll()
        /// <summary>
        /// This method is used to poll the underlying client and return any outstanding messages.
        /// </summary>
        /// <returns>Returns a list of payloads from the underlying communication layer.</returns>
        public async Task<List<TransmissionPayload>> Poll()
        {
            var items = await mHolder.Poll();
            if (items != null)
            {
                Actual = items.Count;
                mComplete(this);
            }

            return items;
        } 
        #endregion

        /// <summary>
        /// This is the priority for the client.
        /// </summary>
        public int Priority
        {
            get
            {
                return mHolder.Client.Priority;
            }
        }

        public void Release(bool exception)
        {
            mHolder.Release(Slot, exception);            
        }
    }
}
