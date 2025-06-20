using System;
using System.Collections.Generic;
using System.ComponentModel;
using ComputerAlgebra;

namespace Circuit
{
    [Category("Vacuum Tubes")]
    [DisplayName("12AX7")]
    public class KurtBlum12AX7 : Component
    {
        private double mu = 96.20;
        [Serialize, Description("Voltage gain.")]
        public double Mu { get { return mu; } set { mu = value; NotifyChanged(nameof(Mu)); } }

        private double ex = 1.437;
        [Serialize]
        public double Ex { get { return ex; } set { ex = value; NotifyChanged(nameof(Ex)); } }

        private double kg1 = 613.4;
        [Serialize]
        public double Kg1 { get { return kg1; } set { kg1 = value; NotifyChanged(nameof(Kg1)); } }

        private double kp = 740.3;
        [Serialize]
        public double Kp { get { return kp; } set { kp = value; NotifyChanged(nameof(Kp)); } }

        private double kvb = 1672.0;
        [Serialize]
        public double Kvb { get { return kvb; } set { kvb = value; NotifyChanged(nameof(Kvb)); } }

        private double rgi = 2000.0;
        [Serialize]
        public double Rgi { get { return rgi; } set { rgi = value; NotifyChanged(nameof(Rgi)); } }

        private Terminal p, g, k;
        public override IEnumerable<Terminal> Terminals
        {
            get
            {
                yield return p;
                yield return g;
                yield return k;
            }
        }
        [Browsable(false)]
        public Terminal Plate { get { return p; } }
        [Browsable(false)]
        public Terminal Grid { get { return g; } }
        [Browsable(false)]
        public Terminal Cathode { get { return k; } }

        public KurtBlum12AX7()
        {
            p = new Terminal(this, "P");
            g = new Terminal(this, "G");
            k = new Terminal(this, "K");
            Name = "V1";
        }

        public override void Analyze(Analysis Mna)
        {
            var n0 = new Node();
            var n5 = new Node();
            var n7 = new Node();

            Expression Vpk = p.V - k.V;
            Expression Vgk = g.V - k.V;

            Expression E1 = Call.Ln(1.0 + Call.Exp(Kp * (1.0 / Mu + Vgk * Binary.Power(Kvb + Vpk * Vpk, -0.5)))) * Vpk / Kp;

            Resistor.Analyze(Mna, n7, n0, Quantity.Parse("1G"));
            VoltageSource.Analyze(Mna, n7, n0, E1);

            Resistor.Analyze(Mna, p, k, Quantity.Parse("1G"));
            CurrentSource.Analyze(Mna, p, k, 0.5 * (PWR(E1, Ex) + PWRS(E1, Ex)) / Kg1);

            Resistor.Analyze(Mna, g, n5, Rgi);
            // D3 5 3 DX     ; FOR GRID CURRENT
            // .MODEL DX D(IS=1N RS=1 CJO=10PF TT=1N)
            Diode.Analyze(Mna, n5, k, Quantity.Parse("1N"), 1.0);

            // if (SimulateCapacitances)
            // {
            //     Capacitor.Analyze(Mna, Name + "_cgp", p, g, _cgp);
            //     Capacitor.Analyze(Mna, Name + "_cgk", g, k, _cgk);
            //     Capacitor.Analyze(Mna, Name + "_cpk", p, k, _cpk);
            // }
        }

        protected internal override void LayoutSymbol(SymbolLayout Sym)
        {
            Sym.AddTerminal(p, new Coord(0, 20), new Coord(0, 5));
            Sym.AddWire(new Coord(-10, 5), new Coord(10, 5));

            Sym.AddTerminal(g, new Coord(-20, 0), new Coord(-12, 0));
            for (int i = -10; i < 10; i += 8)
                Sym.AddWire(new Coord(i, 0), new Coord(i + 4, 0));

            Sym.AddTerminal(k, new Coord(-10, -20), new Coord(-10, -7), new Coord(-8, -5), new Coord(8, -5), new Coord(10, -7));

            Sym.AddCircle(EdgeType.Black, new Coord(0, 0), 20);

            if (PartNumber != null)
                Sym.DrawText(() => PartNumber, new Coord(-2, 20), Alignment.Far, Alignment.Near);
            Sym.DrawText(() => Name, new Point(-8, -20), Alignment.Near, Alignment.Far);
        }

        private static Expression PWR(Expression x, Expression y)
        {
            return Binary.Power(Call.Abs(x), y);
        }

        private static Expression PWRS(Expression x, Expression y)
        {
            return Call.Sign(x) * Binary.Power(Call.Abs(x), y);
        }
    }
}