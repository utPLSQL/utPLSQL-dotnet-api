using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace utPLSQL
{
    /// <summary>
    /// Implementation of TestRunner that uses ut_realtime_reporter
    /// </summary>
    public class RealTimeTestRunner : TestRunner<@event>
    {
        public override async Task RunTestsAsync(List<string> paths, Action<@event> consumer)
        {
            if (paths != null && paths.Count > 0)
            {
                string realtimeReporterId = Guid.NewGuid().ToString().Replace("-", "");

                Task taskRun = UtRunAsync(realtimeReporterId, paths);

                Task taskConsume = ConsumeResultAsync(realtimeReporterId, consumer);

                await Task.WhenAll(taskRun, taskConsume);
            }
        }

        public override async Task RunTestsAsync(string path, Action<@event> consumer)
        {
            await RunTestsAsync(new List<string>() { path }, consumer);
        }

        public override async Task<string> RunTestsWithCoverageAsync(List<string> paths, Action<@event> consumer, List<string> coverageSchemas = null, List<string> includeObjects = null, List<string> excludeObjects = null)
        {
            if (paths != null && paths.Count > 0)
            {
                string realtimeReporterId = Guid.NewGuid().ToString().Replace("-", "");
                string coverageReporterId = Guid.NewGuid().ToString().Replace("-", "");

                Task taskRun = UtRunWithCoverageAsync(realtimeReporterId, coverageReporterId, paths, coverageSchemas, includeObjects, excludeObjects);

                Task taskConsume = ConsumeResultAsync(realtimeReporterId, consumer);

                await Task.WhenAll(taskRun, taskConsume);

                return await GetCoverageReportAsync(coverageReporterId);
            }
            else
            {
                return null;
            }
        }

        private async Task UtRunWithCoverageAsync(string realtimeReporterId, string coverageReporterId, List<string> paths, List<string> coverageSchemas, List<string> includeObjects, List<string> excludeObjects)
        {
            await Task.Run(() =>
            {
                var proc = $@"DECLARE
                               l_rt_rep  ut_realtime_reporter      := ut_realtime_reporter();
                               l_cov_rep ut_coverage_html_reporter := ut_coverage_html_reporter();
                             BEGIN
                               l_rt_rep.set_reporter_id(:id);
                               l_rt_rep.output_buffer.init();
                               l_cov_rep.set_reporter_id(:coverage_id);
                               l_cov_rep.output_buffer.init();
                               sys.dbms_output.enable(NULL);
                               ut_runner.run(a_paths => ut_varchar2_list({ConvertToUtVarchar2List(paths)}), ";

                if (coverageSchemas != null && coverageSchemas.Count > 0)
                {
                    proc += $"a_coverage_schemes => ut_varchar2_list({ConvertToUtVarchar2List(coverageSchemas)}), ";
                }

                if (includeObjects != null && includeObjects.Count > 0)
                {
                    proc += $"a_include_objects => ut_varchar2_list({ConvertToUtVarchar2List(includeObjects)}), ";
                }

                if (excludeObjects != null && excludeObjects.Count > 0)
                {
                    proc += $"a_exclude_objects => ut_varchar2_list({ConvertToUtVarchar2List(excludeObjects)}), ";
                }

                proc += @"  a_reporters => ut_reporters(l_rt_rep, l_cov_rep)); 
                            sys.dbms_output.disable; 
                          END;";

                var cmd = new OracleCommand(proc, produceConnection);
                runningCommands.Add(cmd);

                cmd.Parameters.Add("id", OracleDbType.Varchar2, ParameterDirection.Input).Value = realtimeReporterId;
                cmd.Parameters.Add("coverage_id", OracleDbType.Varchar2, ParameterDirection.Input).Value = coverageReporterId;

                cmd.ExecuteNonQuery();

                runningCommands.Remove(cmd);
                cmd.Dispose();
            });
        }

        public override async Task<string> RunTestsWithCoverageAsync(string path, Action<@event> consumer, string coverageSchema = null, List<string> includeObjects = null, List<string> excludeObjects = null)
        {
            return await RunTestsWithCoverageAsync(new List<string>() { path }, consumer, new List<string>() { coverageSchema }, includeObjects, excludeObjects);
        }

        private async Task UtRunAsync(string id, List<string> paths)
        {
            await Task.Run(() =>
            {
                var proc = $@"DECLARE
                               l_reporter ut_realtime_reporter := ut_realtime_reporter();
                             BEGIN
                               l_reporter.set_reporter_id(:id);
                               l_reporter.output_buffer.init();
                               ut_runner.run(a_paths => ut_varchar2_list({ConvertToUtVarchar2List(new List<string>(paths))}), 
                                             a_reporters => ut_reporters(l_reporter));
                             END;";

                var cmd = new OracleCommand(proc, produceConnection);
                runningCommands.Add(cmd);

                cmd.Parameters.Add("id", OracleDbType.Varchar2, ParameterDirection.Input).Value = id;
                cmd.ExecuteNonQuery();

                runningCommands.Remove(cmd);
                cmd.Dispose();
            });
        }

        private async Task ConsumeResultAsync(string id, Action<@event> action)
        {
            await Task.Run(() =>
            {
                var proc = @"DECLARE
                           l_reporter ut_realtime_reporter := ut_realtime_reporter();
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
                    var xml = reader.GetString(0);

                    var serializer = new XmlSerializer(typeof(@event));
                    var @event = (@event)serializer.Deserialize(new StringReader(xml));

                    action.Invoke(@event);
                }

                reader.Close();

                runningCommands.Remove(cmd);
                cmd.Dispose();
            });
        }
    }
}