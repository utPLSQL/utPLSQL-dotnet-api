using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml.Serialization;

namespace utPLSQL
{
    /// <summary>
    /// Implementation of TestRunner that uses ut_realtime_reporter
    /// </summary>
    public class RealTimeTestRunner : TestRunner<@event>
    {
        public override void RunTests(params string[] paths)
        {
            if (paths != null && paths.Length > 0)
            {
                realtimeReporterId = Guid.NewGuid().ToString().Replace("-", "");

                var proc = $@"DECLARE
                               l_reporter ut_realtime_reporter := ut_realtime_reporter();
                             BEGIN
                               l_reporter.set_reporter_id(:id);
                               l_reporter.output_buffer.init();
                               ut_runner.run(a_paths => ut_varchar2_list({ConvertToUtVarchar2List(new List<string>(paths))}), 
                                             a_reporters => ut_reporters(l_reporter));
                             END;";

                var cmd = new OracleCommand(proc, produceConnection);
                cmd.Parameters.Add("id", OracleDbType.Varchar2, ParameterDirection.Input).Value = realtimeReporterId;
                cmd.ExecuteNonQuery();
            }
        }

        public override void RunTestsWithCoverage(List<string> paths, List<string> coverageSchemas = null, List<string> includeObjects = null, List<string> excludeObjects = null)
        {
            if (paths != null && paths.Count > 0)
            {
                realtimeReporterId = Guid.NewGuid().ToString().Replace("-", "");
                coverageReporterId = Guid.NewGuid().ToString().Replace("-", "");

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
                cmd.Parameters.Add("id", OracleDbType.Varchar2, ParameterDirection.Input).Value = realtimeReporterId;
                cmd.Parameters.Add("coverage_id", OracleDbType.Varchar2, ParameterDirection.Input).Value = coverageReporterId;

                cmd.ExecuteNonQuery();
            }
        }

        public override void ConsumeResult(Action<@event> action)
        {
            var proc = @"DECLARE
                           l_reporter ut_realtime_reporter := ut_realtime_reporter();
                         BEGIN
                           l_reporter.set_reporter_id(:id);
                           :lines_cursor := l_reporter.get_lines_cursor();
                         END;";

            var cmd = new OracleCommand(proc, consumeConnection);
            cmd.Parameters.Add("id", OracleDbType.Varchar2, ParameterDirection.Input).Value = realtimeReporterId;
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
        }
    }
}