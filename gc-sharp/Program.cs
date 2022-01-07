using CommandLine;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace gc
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            return await Parser.Default.ParseArguments<CommandLineOptions>(args)
                .MapResult(async (CommandLineOptions opts) =>
                {
                    try
                    {
                        var jsonOpts = new JsonSerializerOptions();
                        jsonOpts.WriteIndented = true;

                        Console.WriteLine($"Options provided:\n{JsonSerializer.Serialize(opts, jsonOpts)}");

                        var timeStart = DateTime.Now;

                        var cleaner = new Cleaner(opts.Simulate, opts.Autoconfirm, opts.Check);

                        await cleaner.CleanUp();

                        Console.WriteLine($"All tasks completed in {DateTime.Now - timeStart}");
                        return 0;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error! {e.Message}\n{e.StackTrace}");
                        return -3; // Unhandled error
                    }
                },
                errs => Task.FromResult(-1)); // Invalid arguments
        }
    }
}
