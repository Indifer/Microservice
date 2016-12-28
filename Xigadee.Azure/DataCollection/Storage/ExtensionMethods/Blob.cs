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
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Xigadee
{
    public static partial class AzureStorageHelper
    {
        /// <summary>
        /// This method serializes incoming objects in to standard JSON format encoded as UTF8.
        /// </summary>
        /// <param name="e">The incoming EventBase.</param>
        /// <returns>Returns the byte array.</returns>
        public static AzureStorageBinary DefaultJsonBinarySerializer(EventBase e, MicroserviceId id)
        {
            var jObj = JObject.FromObject(e);
            var body = jObj.ToString();
            return new AzureStorageBinary { Blob = Encoding.UTF8.GetBytes(body) };
        }

        //Statistics
        public static string StatisticsMakeId(EventBase ev, MicroserviceId msId)
        {
            var e = ev as MicroserviceStatistics;

            string Id = $"{e.StorageId}.json";
            return Id;
        }
        public static string StatisticsMakeFolder(EventBase ev, MicroserviceId msId)
        {
            var e = ev as MicroserviceStatistics;

            string Directory = string.Format("Statistics/{0}/{1:yyyy-MM-dd}/{1:HH}", msId.Name, DateTime.UtcNow);

            return  Directory;
        }
        //Logger
        public static string LoggerMakeId(EventBase ev, MicroserviceId msId)
        {
            var e = ev as LogEvent;
            
            string Id = $"{ev.TraceId}.json";

            return Id;
        }
        public static string LoggerMakeFolder(EventBase ev, MicroserviceId msId)
        {
            var e = ev as LogEvent;

            string level = Enum.GetName(typeof(LoggingLevel), e.Level);
            string Directory = string.Format("{0}/{1}/{2:yyyy-MM-dd}/{2:HH}", msId.Name, level, DateTime.UtcNow);

            //if (e is ILogStoreName)
            //    return ((ILogStoreName)logEvent).StorageId;

            //// If there is a category specified and it contains valid digits or characters then make it part of the log name to make it easier to filter log events
            //if (!string.IsNullOrEmpty(logEvent.Category) && logEvent.Category.Any(char.IsLetterOrDigit))
            //    return string.Format("{0}_{1}_{2}", logEvent.GetType().Name, new string(logEvent.Category.Where(char.IsLetterOrDigit).ToArray()), Guid.NewGuid().ToString("N"));

            //return string.Format("{0}_{1}", logEvent.GetType().Name, Guid.NewGuid().ToString("N"));

            return Directory;
        }
        //Event Source
        public static string EventSourceMakeId(EventBase ev, MicroserviceId msId)
        {
            var e = ev as EventSourceEntry;

            string Id = string.Format("{0}.json", string.Join("_", e.Key.Split(Path.GetInvalidFileNameChars())));
            return Id;
        }
        public static string EventSourceMakeFolder(EventBase ev, MicroserviceId msId)
        {
            var e = ev as EventSourceEntry;

            string Directory = string.Format("{0}/{1:yyyy-MM-dd}/{2}", msId.Name, e.UTCTimeStamp, e.EntityType);

            return Directory;
        }
    }
}
