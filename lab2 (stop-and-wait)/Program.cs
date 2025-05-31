using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace lab2__stop_and_wait_
{
    internal class Program
    {
        const int totalPackets = 5;
        const int timeoutMs = 1000;
        const double pktLossProb = 0.2;
        const double ackLossProb = 0.2;

        static void Main()
        {
            Console.WriteLine("=== Stop-and-Wait (FSM) ===\n");

            var rnd = new DefaultRandomProvider();
            var delay = new RealDelay();
            var receiver = new Receiver();
            var sender = new Sender(receiver, rnd, delay,
                                      pktLossProb, ackLossProb);

            for (int i = 0; i < totalPackets; i++)
            {
                sender.Send($"Data-{i}", timeoutMs);
            }

            Console.WriteLine("\n[OK] Все пакеты доставлены.");
            Console.ReadKey();
        }
    }
}
