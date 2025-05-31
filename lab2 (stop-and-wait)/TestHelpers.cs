using lab2__stop_and_wait_;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public sealed class DeterministicRnd : IRandomProvider
{
    private readonly Random _r;
    public DeterministicRnd(int seed) => _r = new Random(seed);
    public double NextDouble() => _r.NextDouble();
}
public sealed class NoDelay : IDelayProvider
{
    public void Delay(int _) { }
}
