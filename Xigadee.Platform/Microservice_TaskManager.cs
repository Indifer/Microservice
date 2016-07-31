﻿#region using

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endregion
namespace Xigadee
{
    //TaskManager
    public partial class Microservice
    {
        #region Declarations
        /// <summary>
        /// This class contains the running tasks and provides a breakdown of the current availability for new tasks.
        /// </summary>
        private TaskManager mTaskManager;
        /// <summary>
        /// This is the scheduler container.
        /// </summary>
        private SchedulerContainer mScheduler;
        #endregion

        #region TaskManagerInitialise()
        /// <summary>
        /// This method initialises the process loop components.
        /// </summary>
        protected virtual void TaskManagerInitialise()
        {
            mScheduler = InitialiseSchedulerContainer();

            mTaskManager = InitialiseTaskManager();
        }
        #endregion
        #region TaskManagerStart()
        /// <summary>
        /// This method starts the processing process loop.
        /// </summary>
        protected virtual void TaskManagerStart()
        {
            TaskManagerRegisterProcesses();

            ServiceStart(mTaskManager);

            ServiceStart(mScheduler);
        }
        #endregion
        #region TaskManagerStop()
        /// <summary>
        /// This method stops the process loop.
        /// </summary>
        protected virtual void TaskManagerStop()
        {
            ServiceStop(mScheduler);

            ServiceStop(mTaskManager);
        }
        #endregion

        #region --> Process ...
        /// <summary>
        /// This method creates a service message and injects it in to the execution path and bypasses the listener infrastructure.
        /// </summary>
        /// <typeparam name="C">The message contract.</typeparam>
        /// <param name="package">The objet package to process.</param>
        /// <param name="ChannelPriority">The prioirty that the message should be processed. The default is 1. If this message is not a valid value, it will be matched to the nearest valid value.</param>
        /// <param name="options">The process options.</param>
        /// <param name="release">The release action which is called when the payload has been executed.</param>
        /// <param name="isDeadLetterMessage">A flag indicating whether the message is a deadletter replay. These messages may be treated differently
        /// by the receiving commands.</param>
        public void Process<C>(object package = null
            , int ChannelPriority = 1
            , ProcessOptions options = ProcessOptions.RouteExternal | ProcessOptions.RouteInternal
            , Action<bool, Guid> release = null
            , bool isDeadLetterMessage = false)
            where C : IMessageContract
        {
            string channelId, messageType, actionType;
            ServiceMessageHelper.ExtractContractInfo<C>(out channelId, out messageType, out actionType);

            Process(channelId, messageType, actionType, package, ChannelPriority, options, release, isDeadLetterMessage);
        }
        /// <summary>
        /// This method creates a service message and injects it in to the execution path and bypasses the listener infrastructure.
        /// </summary>
        /// <param name="ChannelId">The incoming channel. This must be supplied.</param>
        /// <param name="MessageType">The message type. This may be null.</param>
        /// <param name="ActionType">The message action. This may be null.</param>
        /// <param name="package">The objet package to process.</param>
        /// <param name="ChannelPriority">The prioirty that the message should be processed. The default is 1. If this message is not a valid value, it will be matched to the nearest valid value.</param>
        /// <param name="options">The process options.</param>
        /// <param name="release">The release action which is called when the payload has been executed.</param>
        /// <param name="isDeadLetterMessage">A flag indicating whether the message is a deadletter replay. These messages may be treated differently
        /// by the receiving commands.</param>
        public void Process(string ChannelId, string MessageType = null, string ActionType = null
            , object package = null
            , int ChannelPriority = 1
            , ProcessOptions options = ProcessOptions.RouteExternal | ProcessOptions.RouteInternal
            , Action<bool, Guid> release = null
            , bool isDeadLetterMessage = false)
        {
            var header = new ServiceMessageHeader(ChannelId, MessageType, ActionType);

            Process(header, package, ChannelPriority, options, release, isDeadLetterMessage);
        }

        /// <summary>
        /// This method creates a service message and injects it in to the execution path and bypasses the listener infrastructure.
        /// </summary>
        /// <param name="header">The message header to identify the recipient.</param>
        /// <param name="package">The objet package to process.</param>
        /// <param name="ChannelPriority">The prioirty that the message should be processed. The default is 1. If this message is not a valid value, it will be matched to the nearest valid value.</param>
        /// <param name="options">The process options.</param>
        /// <param name="release">The release action which is called when the payload has been executed.</param>
        /// <param name="isDeadLetterMessage">A flag indicating whether the message is a deadletter replay. These messages may be treated differently
        /// by the receiving commands.</param>
        public void Process(ServiceMessageHeader header
            , object package = null
            , int ChannelPriority = 1
            , ProcessOptions options = ProcessOptions.RouteExternal | ProcessOptions.RouteInternal
            , Action<bool, Guid> release = null
            , bool isDeadLetterMessage = false)
        {

            var message = new ServiceMessage(header);
            message.ChannelPriority = ChannelPriority;
            if (package != null)
                message.Blob = mSerializer.PayloadSerialize(package);

            Process(message, options, release, isDeadLetterMessage);
        }

        /// <summary>
        /// This method injects a service message in to the execution path and bypasses the listener infrastructure.
        /// </summary>
        /// <param name="message">The service message.</param>
        /// <param name="options">The process options.</param>
        /// <param name="release">The release action which is called when the payload has been executed.</param>
        /// <param name="isDeadLetterMessage">A flag indicating whether the message is a deadletter replay. These messages may be treated differently
        /// by the receiving commands.</param>
        public void Process(ServiceMessage message
            , ProcessOptions options = ProcessOptions.RouteExternal | ProcessOptions.RouteInternal
            , Action<bool, Guid> release = null
            , bool isDeadLetterMessage = false)
        {
            var payload = new TransmissionPayload(message, release: release, options: options, isDeadLetterMessage: isDeadLetterMessage);

            Process(payload);
        }

        /// <summary>
        /// This method injects a payload in to the execution path and bypasses the listener infrastructure.
        /// </summary>
        /// <param name="payload">The transmission payload to execute.</param>
        public void Process(TransmissionPayload payload)
        {
            ValidateServiceStarted();

            mTaskManager.ExecuteOrEnqueue(payload, "Incoming Process method request");
        }
        #endregion

        #region TaskManagerProcessRegister()
        /// <summary>
        /// 
        /// </summary>
        protected virtual void TaskManagerRegisterProcesses()
        {
            mTaskManager.ProcessRegister("SchedulesProcess"
                , 5, mScheduler);

            mTaskManager.ProcessRegister("ListenersProcess"
                , 4, mCommunication);

            mTaskManager.ProcessRegister("Overload Check EventSource"
                , 3, mEventSource);

            mTaskManager.ProcessRegister("Overload Check Logger"
                , 2, mLogger);
        }
        #endregion

    }
}
