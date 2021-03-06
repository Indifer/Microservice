﻿using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xigadee;

namespace Test.Xigadee
{
    [TestClass]
    public class Harness1
    {
        public class CommandHarnessTest1: CommandBase
        {
            public CommandHarnessTest1(CommandPolicy policy = null) :base(policy)
            {

            }

            [CommandContract("one","two")]
            public void Command1(TransmissionPayload inPayload, List<TransmissionPayload> outPayload)
            {

            }

            [JobSchedule("1")]
            public void Schedule1()
            {

            }
        }

        [TestMethod]
        public void PartialCommandContractWithNoChannelIdSet()
        {
            var policy = new CommandPolicy();
            policy.ChannelId = null;
            CommandHarness<CommandHarnessTest1> harness;

            try
            {
                harness = new CommandHarness<CommandHarnessTest1>(() => new CommandHarnessTest1(policy));
                harness.Start();
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is CommandChannelIdNullException);
            }
        }


        [TestMethod]
        public void PartialCommandContractWithChannelIdSet()
        {
            var policy = new CommandPolicy();
            policy.ChannelId = "fredo";
            CommandHarness<CommandHarnessTest1> harness;

            harness = new CommandHarness<CommandHarnessTest1>(() => new CommandHarnessTest1(policy));
            harness.Start();
            Assert.IsTrue(harness.RegisteredCommands.Count == 1);
        }
    }
}
