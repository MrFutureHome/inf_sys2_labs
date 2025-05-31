using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab2__stop_and_wait_
{
    public interface IRandomProvider
    {
        double NextDouble();
    }

    public sealed class DefaultRandomProvider : IRandomProvider
    {
        private readonly Random _rnd;
        public DefaultRandomProvider(int? seed = null)
        {
            _rnd = seed.HasValue
                ? new Random(seed.Value)
                : new Random();
        }
        public double NextDouble() => _rnd.NextDouble();
    }
}
