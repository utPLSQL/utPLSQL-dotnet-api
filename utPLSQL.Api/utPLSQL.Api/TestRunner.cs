using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;

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

        protected List<OracleCommand> runningCommands = new List<OracleCommand>();

        /// <summary>
        /// Connects to the database. 
        /// The TestRunner uses two connections. One for executing the tests and one for consuming the results
        /// </summary>
        /// <param name="username">Database username</param>
        /// <param name="password">Database password</param>
        /// <param name="database">Database name</param>
        /// <param name="connectAs">Connect as</param>
        public void Connect(string username, string password, string database, string connectAs = null)
        {
            string connectionString;
            if (string.IsNullOrEmpty(connectAs))
            {
                connectionString = $"User Id={username};Password={password};Data Source={database}";
            }
            else
            {
                connectionString = $"User Id={username};DBA Privilege={connectAs};Password={password};Data Source={database}";
            }

            foreach (OracleCommand command in runningCommands)
            {
                command.Cancel();
            }

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
            runningCommands.Add(cmd);

            OracleDataReader reader = cmd.ExecuteReader();
            reader.Read();

            var version = reader.GetString(0);

            reader.Close();

            runningCommands.Remove(cmd);
            cmd.Dispose();

            return version;
        }

        /// <summary>
        /// Run tests
        /// </summary>
        /// <param name="paths">Path expressions</param>
        /// <param name="consumer">Callback for each result</param>
        public abstract Task RunTestsAsync(List<string> paths, Action<T> consumer);

        /// <summary>
        /// Run tests
        /// </summary>
        /// <param name="path">One single path expression</param>
        /// <param name="consumer">Callback for each result</param>
        public abstract Task RunTestsAsync(string path, Action<T> consumer);

        /// <summary>
        /// Run tests with coveage
        /// </summary>
        /// <param name="paths">List of path expressions</param>
        /// <param name="consumer">Callback for each result</param>
        /// <param name="coverageSchemas">List of schemas to cover</param>
        /// <param name="includeObjects">List of objects to include</param>
        /// <param name="excludeObjects">List of objects to exclude</param>
        /// <returns>Report as HTML</returns>
        public abstract Task<string> RunTestsWithCoverageAsync(List<string> paths, Action<T> consumer, List<string> coverageSchemas = null, List<string> includeObjects = null, List<string> excludeObjects = null);

        /// <summary>
        /// Run tests with coveage
        /// </summary>
        /// <param name="path">The path</param>
        /// <param name="consumer">Callback for each result</param>
        /// <param name="coverageSchema">The schemas to cover</param>
        /// <param name="includeObjects">List of objects to include</param>
        /// <param name="excludeObjects">List of objects to exclude</param>
        /// <returns>Report as HTML</returns>
        public abstract Task<string> RunTestsWithCoverageAsync(string path, Action<T> consumer, string coverageSchema = null, List<string> includeObjects = null, List<string> excludeObjects = null);

        protected string GetCoverageReport(string id)
        {
            var sb = new StringBuilder();

            var proc = @"DECLARE
                           l_reporter ut_coverage_html_reporter := ut_coverage_html_reporter();
                         BEGIN
                           l_reporter.set_reporter_id(:id);
                           :lines_cursor := l_reporter.get_lines_cursor();
                         END;";

            var cmd = new OracleCommand(proc, consumeConnection);
            runningCommands.Add(cmd);

            cmd.Parameters.Add("id", OracleDbType.Varchar2, ParameterDirection.Input).Value = id;
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

            runningCommands.Remove(cmd);
            cmd.Dispose();

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