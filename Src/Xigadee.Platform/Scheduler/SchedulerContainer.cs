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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
 
#endregion
namespace Xigadee
{
    /// <summary>
    /// This class holds the schedule jobs within the Microservice.
    /// </summary>
    public class SchedulerContainer : CollectionContainerBase<Schedule, SchedulerStatistics>
        , IScheduler, ITaskManagerProcess, IRequireDataCollector
    {
        #region Declarations

        private object syncLock = new object();

        /// <summary>
        /// This is the policy for the scheduler.
        /// </summary>
        protected readonly SchedulerPolicy mPolicy;
        #endregion
        #region Constructor
        /// <summary>
        /// This is the default constructor.
        /// </summary>
        /// <param name="policy">The scheduler policy.</param>
        public SchedulerContainer(SchedulerPolicy policy = null)
            : base(null)
        {
            mPolicy = policy ?? new SchedulerPolicy();
        }
        #endregion

        #region Register...
        /// <summary>
        /// This method registers a schedule.
        /// </summary>
        /// <param name="action">The schedule action.</param>
        /// <param name="frequency">The poll frequency.</param>
        /// <param name="name">The optional poll name.</param>
        /// <param name="initialWait">The optional initial wait until the poll begins.</param>
        /// <param name="initialTime">The optional initial time to start the poll.</param>
        /// <param name="shouldExecute">A flag that indicates whether the poll is active. The default is true.</param>
        /// <param name="isInternal">Specifies whether the poll is internal. The default is false.</param>
        /// <returns>Returns the new schedule after it has been added to the collection.</returns>
        public Schedule Register(Func<Schedule, CancellationToken, Task> action
            , TimeSpan? frequency
            , string name = null
            , TimeSpan? initialWait = null
            , DateTime? initialTime = null
            , bool shouldExecute = true
            , bool isInternal = false
            )
        {
            var schedule = new Schedule(action, name)
            {
                Frequency = frequency,
                InitialWait = initialWait,
                InitialTime = initialTime,
                ShouldExecute = shouldExecute,
                IsInternal = isInternal
            };

            return Register(schedule);
        }
        #endregion
        #region Register(Schedule schedule)
        /// <summary>
        /// This method registers a schedule.
        /// </summary>
        /// <param name="schedule">The schedule object.</param>
        /// <returns>Returns the schedule after it has been added to the schedule collection.</returns>
        public Schedule Register(Schedule schedule)
        {
            schedule.Recalculate();

            lock (syncLock)
            {
                Add(schedule);
            }

            return schedule;
        }
        #endregion
        #region Unregister(Schedule schedule)
        /// <summary>
        /// This removes a schedule from the collection.
        /// </summary>
        /// <param name="schedule">The schedule to remove.</param>
        /// <returns>Returns true if the schedule is removed successfully.</returns>
        public bool Unregister(Schedule schedule)
        {
            schedule.ShouldExecute = false;
            bool result = false;

            lock (syncLock)
            {
                result = base.Remove(schedule);
            }

            return result;
        }

        #endregion

        #region StatisticsRecalculate(SchedulerStatistics stats)
        /// <summary>
        /// This method recalculates the stats for the scheduler.
        /// </summary>
        /// <param name="stats">The statistics.</param>
        protected override void StatisticsRecalculate(SchedulerStatistics stats)
        {
            base.StatisticsRecalculate(stats);

            stats.DefaultPollInMs = mPolicy.DefaultPollInMs;
            stats.Schedules = Items.ToList();
        } 
        #endregion

        #region Collector
        /// <summary>
        /// This is the system wide data collector.
        /// </summary>
        public IDataCollection Collector { get; set; }
        #endregion
        #region TaskSubmit
        /// <summary>
        /// This is the action path back to the TaskManager.
        /// </summary>
        public Action<TaskTracker> TaskSubmit
        {
            get; set;
        }
        #endregion
        #region TaskAvailability
        /// <summary>
        /// This is the task availability collection
        /// </summary>
        public ITaskAvailability TaskAvailability { get; set; } 
        #endregion
        #region CanProcess()
        /// <summary>
        /// Check whether we have any schedules.
        /// </summary>
        /// <returns>Returns true of we can process.</returns>
        public bool CanProcess()
        {
            return Status == ServiceStatus.Running && Count > 0 && TaskSubmit != null;
        }
        #endregion

        #region Process()
        /// <summary>
        /// This method processes any outstanding schedules and created a new task.
        /// </summary>
        public void Process()
        {
            Schedule[] schedules = null;
            lock (syncLock)
            {
                schedules = Items.Where((c) => c.ShouldExecute).ToArray();
            }

            foreach (Schedule schedule in schedules)
            {
                try
                {
                    if (!schedule.Start())
                        continue;

                    TaskTracker tracker = TaskTrackerCreate(schedule);

                    //Submit the task to be executed.
                    TaskSubmit(tracker);
                }
                catch (Exception ex)
                {
                    Collector?.LogException($"{nameof(SchedulerContainer)}/{nameof(Process)} execute error for schedule: {schedule.Id}/{schedule.Name}", ex);
                    schedule.Stop(false, true, true, ex);
                }
            }
        }
        #endregion
        #region TaskTrackerCreate(Schedule schedule)
        /// <summary>
        /// This private method builds the payload consistently for the incoming payload.
        /// </summary>
        /// <param name="schedule">The schedule to add to a tracker.</param>
        /// <returns>Returns a tracker of type payload.</returns>
        private TaskTracker TaskTrackerCreate(Schedule schedule)
        {
            TaskTracker tracker = new TaskTracker(TaskTrackerType.Schedule, null);
            tracker.IsLongRunning = schedule.IsLongRunning;

            tracker.Priority = schedule.IsInternal? TaskTracker.PriorityInternal: schedule.ExecutionPriority ?? mPolicy.DefaultTaskPriority;

            tracker.Context = schedule;

            tracker.Name = schedule.Name;

            tracker.Execute = async (token) =>
            {
                try
                {
                    await schedule.Execute(token);
                }
                catch (Exception ex)
                {
                    Collector?.LogException($"Schedule execution failed: {schedule.Name}", ex);
                    schedule.Stop(false, true, true, ex);
                }
            };

            tracker.ExecuteComplete = ScheduleComplete;

            return tracker;
        }
        #endregion

        #region ScheduleComplete(Task task, TaskTracker tracker)
        /// <summary>
        /// This method is used to complete a schedule request and to 
        /// recalculate the next schedule.
        /// </summary>
        /// <param name="tracker">The tracker object.</param>
        /// <param name="failed">Indicates whether an exception was raised during execution of the schedule.</param>
        /// <param name="ex">The exception raised during execution. This will be null under normal operations.</param>
        private void ScheduleComplete(TaskTracker tracker, bool failed, Exception ex)
        {
            var schedule = tracker.Context as Schedule;

            schedule.Stop(!failed, isException: failed, lastEx: ex, exceptionTime: DateTime.UtcNow);
        }
        #endregion
    }
}
