using System;
using System.IO;
using Circuit;
using System.Collections.Generic;
using Util;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using ComputerAlgebra;
using System.Text;
using ComputerAlgebra.LinqCompiler;

namespace Tests
{
    public static class CircuitExtensions
    {
        public static T Add<T>(this Circuit.Circuit circuit, T t, string name, string a, string c) where T : TwoTerminal
        {
            t.Name = name;
            circuit.Components.Add(t);
            var an = circuit.Nodes[a];
            var cn = circuit.Nodes[c];
            t.ConnectTo(an, cn);
            return t;
        }

        public static Triode AddTriode(this Circuit.Circuit circuit, string name, string p, string g, string k)
        {
            var t = new Triode();
            t.Name = name;
            circuit.Components.Add(t);
            t.Plate.ConnectTo(circuit.Nodes[p]);
            t.Grid.ConnectTo(circuit.Nodes[g]);
            t.Cathode.ConnectTo(circuit.Nodes[k]);
            // TODO: incorporate Blum model
            t.Model = TriodeModel.DempwolfZolzer;
            return t;
        }

        public static KurtBlum12AX7 Add12AX7(this Circuit.Circuit circuit, string name, string p, string g, string k)
        {
            var t = new KurtBlum12AX7();
            t.Name = name;
            circuit.Components.Add(t);
            t.Plate.ConnectTo(circuit.Nodes[p]);
            t.Grid.ConnectTo(circuit.Nodes[g]);
            t.Cathode.ConnectTo(circuit.Nodes[k]);
            return t;
        }

        public static Potentiometer AddPotentiometer(this Circuit.Circuit circuit, string name, string a, string c, string w)
        {
            var p = new Potentiometer();
            p.Name = name;
            circuit.Components.Add(p);
            p.Anode.ConnectTo(circuit.Nodes[a]);
            p.Cathode.ConnectTo(circuit.Nodes[c]);
            p.Wiper.ConnectTo(circuit.Nodes[w]);
            return p;
        }
    }

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand()
                .WithCommand("test", "Run tests", c => c
                    .WithArgument<string>("pattern", "Glob pattern for files to test")
                    .WithOption<bool>(new[] { "--plot" }, "Plot results")
                    .WithOption<bool>(new[] { "--stats" }, "Write statistics")
                    .WithOption(new[] { "--samples" }, () => 4800, "Samples")
                    .WithHandler(CommandHandler.Create<string, bool, bool, int, int, int, int>(Test)))
                .WithCommand("benchmark", "Run benchmarks", c => c
                    .WithArgument<string>("pattern", "Glob pattern for files to benchmark")
                    .WithHandler(CommandHandler.Create<string, int, int, int>(Benchmark)))
                .WithCommand("exportnet", "Export to SPICE netlist", c => c
                    .WithArgument<string>("inPath", "Input .schx file path")
                    .WithArgument<string>("outPath", "Output .net file path")
                    .WithHandler(CommandHandler.Create<string, string, int, int, int>(ExportNetList))
                )
                .WithCommand("exportasc", "Export to SPICE asc", c => c
                    .WithArgument<string>("inPath", "Input .schx file path")
                    .WithArgument<string>("outPath", "Output .asc file path")
                    .WithHandler(CommandHandler.Create<string, string, int, int, int>(ExportAsc))
                )
                .WithCommand("iicp", "hard-coded IIC+ circuit", c => c
                    .WithHandler(CommandHandler.Create<int, int, int>(DumbIICP))
                )
                .WithGlobalOption(new Option<int>("--sampleRate", () => 48000, "Sample Rate"))
                .WithGlobalOption(new Option<int>("--oversample", () => 8, "Oversample"))
                .WithGlobalOption(new Option<int>("--iterations", () => 8, "Iterations"));

            return await rootCommand.InvokeAsync(args);
        }

        private static void DumbIICP(int sampleRate, int oversample, int iterations)
        {
            ILog log = new ConsoleLog() { Verbosity = MessageType.Info };

            var circuit = new Circuit.Circuit();
            Potentiometer vol1, treble, gain;
            VariableResistor bass, mid, master;

            // V1 N019 0 wavefile=di-cut.wav
            var input = circuit.Add(new Input(), "V1", "N019", "0");
            circuit.Add(new Resistor(), "R27", "N004", "N015").Resistance = Quantity.Parse("100k");
            circuit.Add(new Resistor(), "R22", "N008", "N007").Resistance = Quantity.Parse("100k");

            (vol1 = circuit.AddPotentiometer("RVOL1", "N020", "0", "N008")).Resistance = Quantity.Parse("1M");
            //circuit.Add(new Resistor(), "R24", "N020", "N008").Resistance = Quantity.Parse("{1Meg*(1-vol1)}");
            //circuit.Add(new Resistor(), "R25", "0", "N020").Resistance = Quantity.Parse("{1Meg*(vol1)}");

            circuit.Add(new Capacitor(), "C15", "N005", "N004").Capacitance = Quantity.Parse("750p");
            circuit.Add(new Capacitor(), "C14", "N005", "N004").Capacitance = Quantity.Parse("250p");
            circuit.Add(new Capacitor(), "C16", "N016", "N015").Capacitance = Quantity.Parse(".1µ");
            circuit.Add(new Capacitor(), "C17", "N025", "N015").Capacitance = Quantity.Parse(".047µ");
            circuit.Add(new Capacitor(), "C18", "N020", "N008").Capacitance = Quantity.Parse("180p");

            (treble = circuit.AddPotentiometer("RTREBLE", "N005", "N016", "N007")).Resistance = Quantity.Parse("250k");
            //circuit.Add(new Resistor(), "RA_TREBLE", "N005", "N007").Resistance = Quantity.Parse("{250k*(1-treble)}");
            //circuit.Add(new Resistor(), "RC_TREBLE", "N007", "N016").Resistance = Quantity.Parse("{250k*(treble)}");

            (bass = circuit.Add(new VariableResistor(), "RBASS", "N016", "N025")).Resistance = Quantity.Parse("250k");
            // circuit.Add(new Resistor(), "RA_BASS", "N016", "N025").Resistance = Quantity.Parse("{250k*(1-bass)}");
            // circuit.Add(new Resistor(), "RC_BASS", "N025", "N025").Resistance = Quantity.Parse("{250k*(bass)}");

            (mid = circuit.Add(new VariableResistor(), "RMID", "N025", "0")).Resistance = Quantity.Parse("10k");
            // circuit.Add(new Resistor(), "RA_MID", "N025", "0").Resistance = Quantity.Parse("{10k*(1-mid)}");
            // circuit.Add(new Resistor(), "RC_MID", "0", "0").Resistance = Quantity.Parse("{10k*(mid)}");
            circuit.Add(new VoltageSource(), "VE", "N003", "0").Voltage = Quantity.Parse("405");
            circuit.Add(new Resistor(), "R5", "N003", "N004").Resistance = Quantity.Parse("150k");
            circuit.Add12AX7("XV1A", "N004", "N019", "N033");
            circuit.Add(new Resistor(), "R13", "N019", "0").Resistance = Quantity.Parse("1M");
            circuit.Add(new Resistor(), "R2", "N033", "0").Resistance = Quantity.Parse("1.5k");
            circuit.Add(new Capacitor(), "C6", "N033", "0").Capacitance = Quantity.Parse(".47µ");
            circuit.Add(new Capacitor(), "C7", "N033", "0").Capacitance = Quantity.Parse("22µ");
            circuit.Add12AX7("XV1B", "N009", "N020", "N029");
            circuit.Add(new Capacitor(), "C19A", "N029", "0").Capacitance = Quantity.Parse("22µ");
            circuit.Add(new Resistor(), "R26", "N029", "0").Resistance = Quantity.Parse("1.5k");
            circuit.Add(new Resistor(), "R27A", "N003", "N009").Resistance = Quantity.Parse("100k");
            circuit.Add(new Capacitor(), "C20", "N001", "N009").Capacitance = Quantity.Parse(".1µ");
            circuit.Add(new Resistor(), "R35", "N001", "0").Resistance = Quantity.Parse("100k");
            circuit.Add(new Resistor(), "R51", "N011", "N026").Resistance = Quantity.Parse("680k");
            (gain = circuit.AddPotentiometer("RGAIN", "N026", "0", "N030")).Resistance = Quantity.Parse("1M");
            // circuit.Add(new Resistor(), "RA_GAIN", "N026", "N030").Resistance = Quantity.Parse("{1Meg*(1-gain)}");
            // circuit.Add(new Resistor(), "RC_GAIN", "N030", "0").Resistance = Quantity.Parse("{1Meg*(gain)}");
            circuit.Add(new Resistor(), "R52", "N030", "0").Resistance = Quantity.Parse("475k");
            circuit.Add(new Capacitor(), "C35", "N036", "N030").Capacitance = Quantity.Parse("120p");
            circuit.Add(new Resistor(), "R53", "N036", "0").Resistance = Quantity.Parse("1.5k");
            circuit.Add(new Capacitor(), "C36", "N036", "0").Capacitance = Quantity.Parse("2.2µ");
            circuit.Add(new Resistor(), "R36", "N002", "N001").Resistance = Quantity.Parse("3.3M");
            circuit.Add(new Capacitor(), "C24", "N002", "N001").Capacitance = Quantity.Parse("20p");
            circuit.Add(new Resistor(), "R37", "N002", "0").Resistance = Quantity.Parse("680k");
            circuit.Add12AX7("XV3B", "N017", "N030", "N036");
            circuit.Add(new VoltageSource(), "VC", "N010", "0").Voltage = Quantity.Parse("410");
            circuit.Add(new Resistor(), "R54", "N017", "N010").Resistance = Quantity.Parse("82k");
            circuit.Add(new Capacitor(), "C38", "N031", "0").Capacitance = Quantity.Parse("1000p");
            circuit.Add(new Resistor(), "R56", "N031", "0").Resistance = Quantity.Parse("68k");
            circuit.Add(new Resistor(), "R55", "N031", "N018").Resistance = Quantity.Parse("270k");
            circuit.Add(new Capacitor(), "C37", "N018", "N017").Capacitance = Quantity.Parse(".022µ");
            circuit.Add12AX7("XV4A", "N027", "N031", "N035");
            circuit.Add(new Resistor(), "R57", "N035", "0").Resistance = Quantity.Parse("3.3k");
            circuit.Add(new Capacitor(), "C40", "N035", "0").Capacitance = Quantity.Parse(".22µ");
            circuit.Add(new Resistor(), "R40", "N027", "N010").Resistance = Quantity.Parse("274k");
            circuit.Add(new Capacitor(), "C28", "N028", "N027").Capacitance = Quantity.Parse(".047µ");
            circuit.Add(new Capacitor(), "C27", "N002", "N028").Capacitance = Quantity.Parse("250p");
            circuit.Add(new Resistor(), "R39", "N002", "N028").Resistance = Quantity.Parse("220k");
            circuit.Add12AX7("XV2B", "N021", "N002", "N037");
            circuit.Add(new Resistor(), "R44", "N037", "0").Resistance = Quantity.Parse("1.5k");
            circuit.Add(new Resistor(), "R43", "N006", "N021").Resistance = Quantity.Parse("100k");
            circuit.Add(new Capacitor(), "C33", "N022", "N021").Capacitance = Quantity.Parse(".047µ");
            circuit.Add(new Resistor(), "R45", "N023", "N022").Resistance = Quantity.Parse("47k");
            circuit.Add(new Resistor(), "R46", "N023", "0").Resistance = Quantity.Parse("47k");
            circuit.Add(new Resistor(), "R47", "N023", "N034").Resistance = Quantity.Parse("150k");
            circuit.Add(new Resistor(), "R49", "N034", "0").Resistance = Quantity.Parse("4.7k");
            circuit.Add12AX7("XV2A", "N012", "N024", "N032");
            circuit.Add(new Resistor(), "R63", "N006", "N012").Resistance = Quantity.Parse("120k");
            circuit.Add(new Capacitor(), "C43", "N013", "N012").Capacitance = Quantity.Parse(".047µ");

            (master = circuit.Add(new VariableResistor(), "RMASTER", "N014", "0")).Resistance = Quantity.Parse("1M");
            // circuit.Add(new Resistor(), "RC_MASTER", "0", "0").Resistance = Quantity.Parse("{1Meg*(master)}");
            // circuit.Add(new Resistor(), "RA_MASTER", "N014", "0").Resistance = Quantity.Parse("{1Meg*(1-master)}");

            circuit.Add(new VoltageSource(), "VC2", "N006", "0").Voltage = Quantity.Parse("410");
            circuit.Add(new Resistor(), "R48", "N024", "0").Resistance = Quantity.Parse("47k");
            circuit.Add(new Capacitor(), "C1", "N002", "0").Capacitance = Quantity.Parse("47p");
            circuit.Add(new Capacitor(), "C2", "N032", "0").Capacitance = Quantity.Parse(".47µ");
            circuit.Add(new Capacitor(), "C3", "N032", "0").Capacitance = Quantity.Parse("15µ");
            circuit.Add(new Resistor(), "R1", "N032", "0").Resistance = Quantity.Parse("1k");
            circuit.Add(new Resistor(), "R3", "0", "0").Resistance = Quantity.Parse("68k");
            circuit.Add(new Resistor(), "R4", "N024", "N034").Resistance = Quantity.Parse("2.2k");
            circuit.Add(new Capacitor(), "C4", "N001", "N011").Capacitance = Quantity.Parse(".02µ");
            circuit.Add(new Resistor(), "R6", "0", "0").Resistance = Quantity.Parse("6.8k");
            circuit.Add(new Capacitor(), "C5", "N002", "0").Capacitance = Quantity.Parse("500p");
            circuit.Add(new Resistor(), "R7", "N002", "0").Resistance = Quantity.Parse("100k");
            circuit.Add(new Resistor(), "R9", "N014", "N013").Resistance = Quantity.Parse("15k");
            circuit.Add(new Resistor(), "R10", "N005", "N005").Resistance = Quantity.Parse("10M");
            circuit.Add(new Resistor(), "R11", "0", "0").Resistance = Quantity.Parse("15k");
            var output = circuit.Add(new Speaker(), "Vout", "N014", "0");
            output.Impedance = Quantity.Parse("8");

            // set wipe values for Potentiometers and VariableResistors:
            treble.Wipe = 0.8;
            mid.Wipe = 0.33;
            bass.Wipe = 0.05;
            gain.Wipe = 0.5;
            master.Wipe = 0.5;
            vol1.Wipe = 0.75;

            var analysis = circuit.Analyze();
            var ts = TransientSolution.Solve(analysis, (Real)1 / (sampleRate * oversample));

            Simulation s = new Simulation(ts)
            {
                Oversample = oversample,
                Iterations = iterations,
                Input = new[] { Component.DependentVariable(input.Name, Component.t) },
                Output = new[] { output.Anode.V },
                Log = log,
            };

            CodeGen code = s.GenerateCode();

            foreach (var decl in code.Decls)
            {
                log.WriteLine(MessageType.Info,
                    "\tvar {0} {1}",
                    decl.Name,
                    decl.Type
                );
            }

            foreach (var expr in code.Code)
            {
                if (expr.NodeType == System.Linq.Expressions.ExpressionType.Label)
                {
                    log.WriteLine(MessageType.Info, "{0}:", (expr as System.Linq.Expressions.LabelExpression).Target.Name);
                }
                else
                {
                    log.WriteLine(MessageType.Info, "\t{0}", expr.ToString());
                }
            }
        }

        private static Dictionary<string, int> Prefixes = new Dictionary<string, int>()
        {
            { "f", -15 },
            { "p", -12 },
            { "n", -9 },
            { "u", -6 },
            { "m", -3 },
            { "", 0 },
            { "k", 3 },
            { "M", 6 },
            { "G", 9 },
            { "T", 12 },
        };
        private static int MinPrefix = Prefixes.Values.Min();
        private static int MaxPrefix = Prefixes.Values.Max();

        private static String Fmt(Expression Value)
        {
            String format = "G5";
            StringBuilder SB = new StringBuilder();

            Constant constant = Value as Constant;
            if (constant != null)
            {
                if (format != null && format.StartsWith("+"))
                {
                    if (constant.Value >= 0)
                        SB.Append("+");
                    format = format.Remove(0, 1);
                }

                // Find out how many digits the format has.
                double round = 1.0;
                for (int significant = 12; significant > 0; --significant)
                {
                    round = 1.0 + 5 * Math.Pow(10.0, -significant);

                    if (double.Parse(round.ToString(format)) != 1.0)
                        break;
                }

                // Find the order of magnitude of the value.
                Real order = Real.Log10(Real.Abs(constant.Value.IsNaN() ? 0 : constant.Value) * round);
                if (order < -20) order = 0;
                else if (order == Real.Infinity) order = 0;

                int prefix = Math.Max(Math.Min((int)Real.Floor(order / 3) * 3, MaxPrefix), MinPrefix);

                Value /= (((Real)10) ^ prefix);
                SB.Append(Value.ToString(format, null));
                SB.Append(Prefixes.Single(i => i.Value == prefix).Key);
            }
            else if (Value != null)
            {
                SB.Append(Convert.ToString((object)Value, null));
            }
            else
            {
                SB.Append("0");
            }

            return SB.ToString().Trim();
        }

        public static void ExportNetList(string inPath, string outPath, int sampleRate, int oversample, int iterations)
        {
            var log = new ConsoleLog() { Verbosity = MessageType.Info };

            log.WriteLine(MessageType.Info, inPath);
            var circuit = Schematic.Load(inPath, log).Build();
            circuit.Name = Path.GetFileNameWithoutExtension(inPath);

            using (var sw = new StreamWriter(outPath))
            {
                sw.WriteLine($"* {circuit.Name}");
                foreach (var cmp in circuit.Components)
                {
                    switch (cmp)
                    {
                        case Resistor x:
                            sw.WriteLine($"R{cmp.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {Fmt(x.Resistance)}");
                            break;
                        case Capacitor x:
                            sw.WriteLine($"C{cmp.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {Fmt(x.Capacitance)}");
                            break;
                        case Input x:
                            // FIXME?
                            sw.WriteLine($"V_SRC_{cmp.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} SINE(0 0.6447 440) AC");
                            break;
                        case Triode x:
                            sw.WriteLine($"XU{cmp.Name} {x.Plate.ConnectedTo.Name} {x.Grid.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} NH12AX7");
                            break;
                        case Potentiometer x:
                            sw.WriteLine($"XR_{cmp.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {x.Wiper.ConnectedTo.Name} potentiometer R={Fmt(x.Resistance)} wiper=.5");
                            break;
                        case VariableResistor x:
                            // FIXME?
                            sw.WriteLine($"XR_{cmp.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {x.Anode.ConnectedTo.Name} potentiometer R={Fmt(x.Resistance)} wiper=.5");
                            break;
                        case VoltageSource x:
                            // FIXME?
                            sw.WriteLine($"V_SRC_{cmp.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {Fmt(x.Voltage)} DC");
                            break;
                        case Speaker x:
                            sw.WriteLine($"R_SPKR_{cmp.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {Fmt(x.Impedance)}");
                            break;
                        default:
                            //sw.WriteLine($"* {cmp.GetType().FullName} = {cmp.ToString()}");
                            break;
                    }
                }
                sw.WriteLine(".INC dmtriodep.inc");
                sw.WriteLine(".subckt potentiometer A W C");
                sw.WriteLine(".param w=limit(wiper,1m,.999)");
                sw.WriteLine("R0 A C {R*(1-w)}");
                sw.WriteLine("R1 C B {R*(w)}");
                sw.WriteLine(".ends POT");
                sw.WriteLine(".tran 100m");
            }
        }

        public static void ExportAsc(string inPath, string outPath, int sampleRate, int oversample, int iterations)
        {
            var log = new ConsoleLog() { Verbosity = MessageType.Info };

            log.WriteLine(MessageType.Info, inPath);
            var sch = Schematic.Load(inPath, log);
            //sch.Name = Path.GetFileNameWithoutExtension(inPath);

            using (var sw = new StreamWriter(outPath, false, Encoding.ASCII))
            {
                sw.WriteLine($"Version 4");
                sw.WriteLine($"SHEET 1 6400 3200");
                foreach (var e in sch.Elements)
                {
                    switch (e)
                    {
                        case Wire w:
                            sw.WriteLine($"WIRE {w.A.x} {w.A.y} {w.B.x} {w.B.y}");
                            break;
                        case Circuit.Symbol s:
                            switch (s.Component)
                            {
                                case Resistor x:
                                    // sw.WriteLine($"SYMBOL C:\\Program\\ Files\\LTC\\SwCADIII\\lib\\sym\\res {s.Position.x-16} {s.Position.y-16-20} R0");
                                    sw.WriteLine($"SYMBOL livespice-res {s.Position.x} {s.Position.y} R0");
                                    sw.WriteLine($"SYMATTR InstName R{x.Name}");
                                    sw.WriteLine($"SYMATTR Value {Fmt(x.Resistance)}");
                                    break;
                                case Capacitor x:
                                    // sw.WriteLine($"SYMBOL C:\\Program\\ Files\\LTC\\SwCADIII\\lib\\sym\\cap {s.Position.x-16} {s.Position.y-20} R0");
                                    sw.WriteLine($"SYMBOL livespice-cap {s.Position.x} {s.Position.y} R0");
                                    sw.WriteLine($"SYMATTR InstName C{x.Name}");
                                    sw.WriteLine($"SYMATTR Value {Fmt(x.Capacitance)}");
                                    break;
                                case Input x:
                                    // FIXME?
                                    // sw.WriteLine($"V_SRC_{x.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} SINE(0 0.6447 440) AC");
                                    break;
                                case Triode x:
                                    // sw.WriteLine($"XU{x.Name} {x.Plate.ConnectedTo.Name} {x.Grid.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} NH12AX7");
                                    break;
                                case Potentiometer x:
                                    // sw.WriteLine($"XR_{x.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {x.Wiper.ConnectedTo.Name} potentiometer R={Fmt(x.Resistance)} wiper=.5");
                                    break;
                                case VariableResistor x:
                                    // FIXME?
                                    // sw.WriteLine($"XR_{x.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {x.Anode.ConnectedTo.Name} potentiometer R={Fmt(x.Resistance)} wiper=.5");
                                    break;
                                case VoltageSource x:
                                    // FIXME?
                                    // sw.WriteLine($"V_SRC_{x.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {Fmt(x.Voltage)} DC");
                                    break;
                                case Speaker x:
                                    // sw.WriteLine($"R_SPKR_{x.Name} {x.Anode.ConnectedTo.Name} {x.Cathode.ConnectedTo.Name} {Fmt(x.Impedance)}");
                                    break;
                                default:
                                    //sw.WriteLine($"* {cmp.GetType().FullName} = {cmp.ToString()}");
                                    break;
                            }
                            break;
                    }
                }
                sw.WriteLine();
                // sw.WriteLine(".INC dmtriodep.inc");
                // sw.WriteLine(".subckt potentiometer A W C");
                // sw.WriteLine(".param w=limit(wiper,1m,.999)");
                // sw.WriteLine("R0 A C {R*(1-w)}");
                // sw.WriteLine("R1 C B {R*(w)}");
                // sw.WriteLine(".ends POT");
                // sw.WriteLine(".tran 100m");
            }
        }

        public static void Test(string pattern, bool plot, bool stats, int sampleRate, int samples, int oversample, int iterations)
        {
            var log = new ConsoleLog() { Verbosity = MessageType.Info };
            var tester = new Test();

            foreach (var circuit in GetCircuits(pattern, log))
            {
                var outputs = tester.Run(circuit, t => Harmonics(t, 0.5, 82, 2), sampleRate, samples, oversample, iterations, log: log);
                if (plot)
                {
                    tester.PlotAll(circuit.Name, outputs);
                }
                if (stats)
                {
                    tester.WriteStatistics(circuit.Name, outputs);
                }
            }
        }

        public static void Benchmark(string pattern, int sampleRate, int oversample, int iterations)
        {
            var log = new ConsoleLog() { Verbosity = MessageType.Info };
            var tester = new Test();
            string fmt = "{0,-40}{1,12:G4}{2,12:G4}{3,12:G4}{4,12:G4}";
            System.Console.WriteLine(fmt, "Circuit", "Analysis (ms)", "Solve (ms)", "Sim (kHz)", "Realtime x");
            foreach (var circuit in GetCircuits(pattern, log))
            {
                double[] result = tester.Benchmark(circuit, t => Harmonics(t, 0.5, 82, 2), sampleRate, oversample, iterations, log: log);
                double analyzeTime = result[0];
                double solveTime = result[1];
                double simRate = result[2];
                string name = circuit.Name;
                if (name.Length > 39)
                    name = name.Substring(0, 39);
                System.Console.WriteLine(fmt, name, analyzeTime * 1000, solveTime * 1000, simRate / 1000, simRate / sampleRate);
            }
        }

        private static IEnumerable<Circuit.Circuit> GetCircuits(string glob, ILog log) => Globber.Glob(glob).Select(filename =>
        {
            log.WriteLine(MessageType.Info, filename);
            var circuit = Schematic.Load(filename, log).Build();
            circuit.Name = Path.GetFileNameWithoutExtension(filename);
            return circuit;
        });

        // Generate a function with the first N harmonics of f0.
        private static double Harmonics(double t, double A, double f0, int N)
        {
            double s = 0;
            for (int i = 1; i <= N; ++i)
                s += Math.Sin(t * f0 * 2 * 3.1415 * i) / N;
            return A * s;
        }
    }
}
