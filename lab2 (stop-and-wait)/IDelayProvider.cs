using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace lab2__stop_and_wait_
{
    public interface IDelayProvider
    {
        void Delay(int ms);
    }
    public sealed class RealDelay : IDelayProvider
    {
        public void Delay(int ms) => Thread.Sleep(ms);
    }
}
