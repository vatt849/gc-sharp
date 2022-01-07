using CommandLine;

namespace gc
{
    internal class CommandLineOptions
    {
        [Option(shortName: 's', longName: "simulate", Required = false, HelpText = "Simulate process or not", Default = false)]
        public bool Simulate { get; set; }

        [Option(shortName: 'y', longName: "autoconfirm", Required = false, HelpText = "Confirm all prompts", Default = false)]
        public bool Autoconfirm { get; set; }

        [Option(shortName: 'c', longName: "check", Required = false, HelpText = "Recheck files after cleaning", Default = false)]
        public bool Check { get; set; }

        [Option(shortName: 'd', longName: "debug", Required = false, HelpText = "Run in debug mode", Default = false)]
        public bool IsDebug { get; set; }

        [Option(shortName: 'v', longName: "verbose", Required = false, HelpText = "Verbose output", Default = false)]
        public bool Verbose { get; set; }
    }
}
