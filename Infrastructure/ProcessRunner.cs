using System;
using System.Diagnostics;

namespace OptiMaster
{
    internal interface IProcessRunner
    {
        string Run(string fileName, string arguments);
    }

    internal sealed class ProcessRunner : IProcessRunner
    {
        public string Run(string fileName, string arguments)
        {
            try
            {
                var start = new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(start))
                {
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    return outputTask.Result + errorTask.Result;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
