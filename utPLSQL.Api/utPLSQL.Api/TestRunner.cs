using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace utPLSQL
{
    /// <summary>
    /// Abstract base class for all TestRunner implementations
    /// </summary>
    /// <typeparam name="T">Type of result class used in callback action</typeparam>
    public abstract class TestRunner<T>
    {
        internal OracleConnection produceConnection;
        internal OracleConnection consumeConnection;

        protected string realtimeReporterId;
        protected string coverageReporterId;

        /// <summary>
        /// Connects to the database. 
        /// The TestRunner uses two connections. One for executing the tests and one for consuming the reuslts
        /// </summary>
        /// <param name="username">Database username</param>
        /// <param name="password">Database password</param>
        /// <param name="database">Database name</param>
        public void Connect(string username, string password, string database)
        {
            var connectionString = $"User Id={username};Password={password};Data Source={database}";

            produceConnection = new OracleConnection(connectionString);
            produceConnection.Open();

            consumeConnection = new OracleConnection(connectionString);
            consumeConnection.Open();
        }
        /// <summary>
        /// Closes both connections
        /// </summary>
        public void Close()
        {
            produceConnection?.Close();
            consumeConnection?.Close();
        }

        /// <summary>
        /// Returns the installed utPLSQL version
        /// </summary>
        /// <returns>Version as string</returns>
        public String GetVersion()
        {
            var cmd = new OracleCommand("select ut.version() from dual", produceConnection);
            OracleDataReader reader = cmd.ExecuteReader();
            reader.Read();
            var version = reader.GetString(0);
            reader.Close();
            return version;
        }

        /// <summary>
        /// Run tests
        /// </summary>
        /// <param name="paths">Path expressions</param>
        public abstract void RunTests(params string[] paths);

        /// <summary>
        /// Run tests with coveage
        /// </summary>
        /// <param name="paths">List of path expressions</param>
        /// <param name="coverageSchemas">List of schemas to cover</param>
        /// <param name="includeObjects">List of objects to include</param>
        /// <param name="excludeObjects">List of objects to exclude</param>
        public abstract void RunTestsWithCoverage(List<string> paths, List<string> coverageSchemas = null, List<string> includeObjects = null, List<string> excludeObjects = null);

        /// <summary>
        /// Consumes the results and calls the callback action on each result
        /// </summary>
        /// <param name="consumer">Typed action that will get the results</param>
        public abstract void ConsumeResult(Action<T> consumer);

        /// <summary>
        /// Returns the HTML coverage report
        /// </summary>
        /// <returns>HTML coverage report</returns>
        public string GetCoverageReport()
        {
            var sb = new StringBuilder();

            var proc = @"DECLARE
                           l_reporter ut_coverage_html_reporter := ut_coverage_html_reporter();
                         BEGIN
                           l_reporter.set_reporter_id(:id);
                           :lines_cursor := l_reporter.get_lines_cursor();
                         END;";

            var cmd = new OracleCommand(proc, consumeConnection);
            cmd.Parameters.Add("id", OracleDbType.Varchar2, ParameterDirection.Input).Value = coverageReporterId;
            cmd.Parameters.Add("lines_cursor", OracleDbType.RefCursor, ParameterDirection.Output);

            // https://stackoverflow.com/questions/2226769/bad-performance-with-oracledatareader
            cmd.InitialLOBFetchSize = -1;

            var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var line = reader.GetString(0);
                sb.Append(line);
            }

            reader.Close();

            return sb.ToString();
        }

        protected string ConvertToUtVarchar2List(List<string> elements)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var element in elements)
            {
                if (!first)
                {
                    sb.Append(",");
                }
                sb.Append("'").Append(element).Append("'");
                first = false;
            }
            return sb.ToString();
        }
    }
}