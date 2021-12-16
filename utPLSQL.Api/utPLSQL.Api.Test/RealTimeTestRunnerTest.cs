using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace utPLSQL
{
    [TestClass]
    public class RealTimeTestRunnerTest
    {
        const string username = "TESTS_OWNER";
        const string password = "pass";
        const string database = "XE";

        [TestMethod]
        public async Task TestRunTests()
        {
            var testRunner = new RealTimeTestRunner();

            testRunner.Connect(username, password, database);

            var events = new List<@event>();
            await testRunner.RunTestsAsync("TESTS_OWNER.TEST_PKG_TEST_ME", @event =>
            {
                events.Add(@event);
            });

            Assert.AreEqual("pre-run", events[0].type);
            Assert.AreEqual("post-run", events.Last().type);

            testRunner.Close();
        }

        [TestMethod]
        public async Task TestConnectAs()
        {
            var testRunner = new RealTimeTestRunner();

            testRunner.Connect(username: "sys", password: "oracle", database: database, connectAs: "sysdba");

            try
            {
                await testRunner.RunTestsAsync("TESTS_OWNER.TEST_PKG_TEST_ME", @event => { });

                Assert.Fail();
            }
            catch (OracleException e)
            {
                Assert.IsTrue(e.Message.StartsWith("ORA-06598"));

                testRunner.Close();
            }
        }

        [TestMethod]
        public async Task TestRunTestsWithCoverage()
        {
            var testRunner = new RealTimeTestRunner();

            testRunner.Connect(username, password, database);

            var events = new List<@event>();

            string report = await testRunner.RunTestsWithCoverageAsync(path: "TESTS_OWNER.TEST_PKG_TEST_ME", consumer: @event => { events.Add(@event); },
                                                                       coverageSchema: "CODE_OWNER", includeObjects: new List<string>() { "PKG_TEST_ME" });
            Logger.LogMessage(report);

            Assert.AreEqual("pre-run", events[0].type);
            Assert.AreEqual("post-run", events.Last().type);

            testRunner.Close();
        }

        [TestMethod]
        public void TestRunTestsAndAbort()
        {
            var testRunner = new RealTimeTestRunner();

            testRunner.Connect(username, password, database);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            testRunner.RunTestsAsync("ut3_tester.test_ut_test", @event => { });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            testRunner.Close();
        }

        [TestMethod]
        public async Task TestRunTestsTwoTimes()
        {
            var testRunner = new RealTimeTestRunner();

            testRunner.Connect(username, password, database);

            var events1 = new List<@event>();
            Task task1 = testRunner.RunTestsAsync("TESTS_OWNER.TEST_PKG_TEST_ME", @event =>
            {
                events1.Add(@event);
            });

            var events2 = new List<@event>();
            Task task2 = testRunner.RunTestsAsync("TESTS_OWNER.TEST_PKG_TEST_ME", @event =>
             {
                 events2.Add(@event);
             });

            await Task.WhenAll(task1, task2);

            testRunner.Close();
        }

        [TestMethod]
        public void TestGetVersion()
        {
            var testRunner = new RealTimeTestRunner();

            testRunner.Connect(username, password, database);

            var version = testRunner.GetVersion();

            Assert.AreEqual("v3.1.11.3559", version);

            testRunner.Close();
        }

        // [TestMethod] Disabled
        public void TestGetVersionWhenNotInstalled()
        {
            var testRunner = new RealTimeTestRunner();

            testRunner.Connect(username, password, database);

            try
            {
                var version = testRunner.GetVersion();
                Assert.Fail();
            }
            catch (OracleException e)
            {
                Assert.AreEqual("ORA-00904: \"UT\".\"VERSION\": ungültige ID", e.Message);

                testRunner.Close();
            }
        }
    }
}
