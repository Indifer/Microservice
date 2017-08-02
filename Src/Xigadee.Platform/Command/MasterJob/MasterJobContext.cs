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

#region using
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Xigadee;
#endregion
namespace Xigadee
{
    /// <summary>
    /// This context holds the current state of the master job process for the command.
    /// </summary>
    public class MasterJobContext
    {
        /// <summary>
        /// This event can is fired when the state of the master job is changed.
        /// </summary>
        public event EventHandler<MasterJobStateChangeEventArgs> OnMasterJobStateChange;
        /// <summary>
        /// Initializes a new instance of the <see cref="MasterJobContext"/> class.
        /// </summary>
        public MasterJobContext(string name, MasterJobNegotiationStrategyBase strategy)
        {
            Name = name;
            Partners = new ConcurrentDictionary<string, MasterJobPartner>();
            Jobs = new Dictionary<Guid, MasterJobHolder>();
            Strategy = strategy ?? new MasterJobNegotiationStrategy();
        }
        /// <summary>
        /// Gets the name or the master job.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// This collection holds the list of master job standby partners.
        /// </summary>
        public ConcurrentDictionary<string, MasterJobPartner> Partners { get; }
        /// <summary>
        /// Gets the master jobs.
        /// </summary>
        public Dictionary<Guid, MasterJobHolder> Jobs { get; }

        /// <summary>
        /// The current status.
        /// </summary>
        public DateTime? mCurrentMasterReceiveTime;

        public string mCurrentMasterServiceId;

        private int mCurrentMasterPollAttempts =0;

        public int MasterPollAttempts { get { return mCurrentMasterPollAttempts; } }


        public void MasterRecordSet(string remoteServiceId)
        {
            mCurrentMasterServiceId = remoteServiceId;
            mCurrentMasterReceiveTime = DateTime.UtcNow;
        }

        public void MasterRecordClear(bool iAmMaster = true)
        {
            mCurrentMasterServiceId = iAmMaster?"ACTIVE":"";
            mCurrentMasterReceiveTime = DateTime.UtcNow;
        }

        public void MasterPollAttemptsIncrement()
        {
            mCurrentMasterPollAttempts++;
        }

        public bool MasterPollAttemptsExceeded()
        {
            return Strategy.PollAttemptsExceeded(State, mCurrentMasterPollAttempts);
        }

        /// <summary>
        /// The timestamp for the last negotiation message out.
        /// </summary>
        public DateTime? MessageLastOut { get; set; }
        /// <summary>
        /// The timestamp for the last negotiation message received.
        /// </summary>
        public DateTime? MessageLastIn { get; set; }

        /// <summary>
        /// The state change counter.
        /// </summary>
        public long StateChangeCounter { get { return mStateChangeCounter; } }

        #region State
        private MasterJobState mState;
        private object mLockState = new object();
        private long mStateChangeCounter = 0;
        /// <summary>
        /// This boolean property identifies whether this job is the master job for the particular 
        /// NegotiationMessageType.
        /// </summary>
        public virtual MasterJobState State
        {
            get
            {
                return mState;
            }
            set
            {
                MasterJobState? oldState;

                lock (mLockState)
                {
                    if (value != mState)
                    {
                        oldState = mState;
                        mState = value;
                        mCurrentMasterPollAttempts = 0;
                    }
                    else
                        oldState = null;
                }

                try
                {
                    if (oldState.HasValue)
                    {
                        OnMasterJobStateChange?.Invoke(this, new MasterJobStateChangeEventArgs(oldState.Value, value, Interlocked.Increment(ref mStateChangeCounter)));
                    }
                }
                catch { }
            }
        }
        #endregion

        #region MasterJobPartnerAdd(string originatorServiceId, bool isStandby)
        /// <summary>
        /// The method add the MasterJob Partner.
        /// </summary>
        /// <param name="originatorServiceId">The originator service identifier.</param>
        /// <param name="isStandby">if set to <c>true</c> [is standby].</param>
        public void PartnerAdd(string originatorServiceId, bool isStandby)
        {
            var record = new MasterJobPartner(originatorServiceId, isStandby);
            Partners.AddOrUpdate(record.ServiceId, s => record, (s, o) => record);
        }
        #endregion

        public Schedule MasterJobSchedule { get; private set; }

        /// <summary>
        /// Initialises the poll schedule.
        /// </summary>
        /// <param name="execute">The execute function.</param>
        /// <returns>Returns the schedule.</returns>
        public Schedule InitialiseSchedule(Func<Schedule, CancellationToken, Task> execute)
        {
            //Register the schedule used for poll requests.
            var schedule = new MasterJobPollSchedule(execute, Name);

            schedule.Frequency = Strategy.InitialPollFrequency;
            schedule.InitialWait = Strategy.InitialPollWait;
            schedule.IsLongRunning = false;

            MasterJobSchedule = schedule;
            return schedule;
        }

        public void Start()
        {
            Partners.Clear();
        }
        /// <summary>
        /// Gets the negotiation strategy.
        /// </summary>
        public MasterJobNegotiationStrategyBase Strategy { get; }

        /// <summary>
        /// This method sets the next poll time based on the current state and the number of poll attempts.
        /// </summary>
        /// <param name="schedule">The schedule.</param>
        public void SetNextPollTime(Schedule schedule)
        {
            Strategy.SetNextPollTime(schedule, State, mCurrentMasterPollAttempts);
        }
    }
}