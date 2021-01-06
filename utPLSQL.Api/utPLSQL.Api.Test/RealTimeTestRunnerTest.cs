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
        private RealTimeTestRunner testRunner;

        [TestInitialize]
        public void Before()
        {
            testRunner = new RealTimeTestRunner();
        }

        [TestCleanup]
        public void After()
        {
            testRunner.Close();
        }

        [TestMethod]
        public async Task TestRunTests()
        {
            testRunner.Connect(username: "toscamtest", password: "toscamtest", database: "CA40");

            var events = new List<@event>();
            await testRunner.RunTestsAsync("toscamtest", @event =>
            {
                events.Add(@event);
            });

            Assert.AreEqual("pre-run", events[0].type);
            Assert.AreEqual("post-run", events.Last().type);
        }

        [TestMethod]
        public async Task TestConnectAs()
        {
            testRunner.Connect(username: "sys", password: "Oradoc_db1", database: "ORCLPDB1", connectAs: "SYSDBA");

            try
            {
                await testRunner.RunTestsAsync("toscamtest", @event => { });

                Assert.Fail();
            }
            catch (OracleException e)
            {
                Assert.IsTrue(e.Message.StartsWith("ORA-06598"));
            }
        }

        [TestMethod]
        public async Task TestRunTestsWithCoverage()
        {
            testRunner.Connect(username: "toscamtest", password: "toscamtest", database: "CA40");

            var events = new List<@event>();

            string report = await testRunner.RunTestsWithCoverageAsync(path: "toscamtest", consumer: @event => { events.Add(@event); },
                                                                       coverageSchema: "toscam", includeObjects: new List<string>() { "pa_m720", "pa_m770" });

            Assert.AreEqual("pre-run", events[0].type);
            Assert.AreEqual("post-run", events.Last().type);

            Logger.LogMessage(report);
        }


        [TestMethod]
        public void TestRunTestsAndAbort()
        {
            testRunner.Connect(username: "toscamtest", password: "toscamtest", database: "CA40");

            testRunner.RunTestsAsync("toscamtest", @event => { });
        }

        [TestMethod]
        public async Task TestRunTestsTwoTimes()
        {
            testRunner.Connect(username: "toscamtest", password: "toscamtest", database: "CA40");

            testRunner.RunTestsAsync("toscamtest", @event => { });

            await testRunner.RunTestsAsync("toscamtest", @event => { });
        }

        [TestMethod]
        public void TestGetVersion()
        {
            testRunner.Connect(username: "toscamtest", password: "toscamtest", database: "CA40");

            string version = testRunner.GetVersion();

            Assert.AreEqual("v3.1.7.3096", version);
        }

        // [TestMethod] Disabled
        public void TestGetVersionWhenNotInstalled()
        {
            testRunner.Connect(username: "sakila", password: "sakila", database: "ORCLPDB1");

            try
            {
                string version = testRunner.GetVersion();
                Assert.Fail();
            }
            catch (OracleException e)
            {
                Assert.AreEqual("ORA-00904: \"UT\".\"VERSION\": ungültige ID", e.Message);
            }
        }
    }
}
