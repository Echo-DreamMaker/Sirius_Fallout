using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using Content.IntegrationTests;
using Content.Server.Maps;
#if DEBUG
using BenchmarkDotNet.Configs;
#else
using Robust.Benchmarks.Configs;
#endif
using Robust.Shared.Prototypes;

namespace Content.Benchmarks
{
    internal static class Program
    {

        public static void Main(string[] args)
        {
#if DEBUG
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nWARNING: YOU ARE RUNNING A DEBUG BUILD, USE A RELEASE BUILD FOR AN ACCURATE BENCHMARK");
            Console.WriteLine("THE DEBUG BUILD IS ONLY GOOD FOR FIXING A CRASHING BENCHMARK\n");
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());
#else
            var config = DefaultSQLConfig.Instance;
           // config.BuildTimeout.Add(new TimeSpan(0, 10, 0));
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
#endif
        }
    }
}
