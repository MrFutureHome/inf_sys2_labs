using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab2__stop_and_wait_
{
    //все возможные состояния у отправителя и получателя
    public enum SenderState { Idle, WaitingForAck }
    public enum ReceiverState { WaitingForPacket }

    public sealed class Sender
    {
        private readonly Receiver _receiver;
        private readonly IRandomProvider _rnd;
        private readonly IDelayProvider _delay;
        private readonly double _pktLoss;
        private readonly double _ackLoss;

        public SenderState State { get; private set; } = SenderState.Idle;
        public int Sequence { get; private set; } = 0;
        public int Retries { get; private set; } = 5;

        public Sender(Receiver receiver,
                      IRandomProvider rnd,
                      IDelayProvider delay,
                      double pktLoss,
                      double ackLoss)
        {
            _receiver = receiver;
            _rnd = rnd;
            _delay = delay;
            _pktLoss = pktLoss;
            _ackLoss = ackLoss;
        }

        public void Send(string data, int timeoutMs)
        {
            while (true)
            {
                var pkt = new Packet
                {
                    SequenceNumber = Sequence,
                    Data = data
                };

                Console.WriteLine($"[TX] пакет #{pkt.SequenceNumber}");
                State = SenderState.WaitingForAck;

                // Симуляция потери пакета
                if (_rnd.NextDouble() < _pktLoss)
                {
                    Console.WriteLine("… пакет потерян");
                }
                else
                {
                    // Пакет дошёл до получателя
                    bool fresh = _receiver.Receive(pkt);
                    Console.WriteLine(fresh
                        ? "[RX] пакет принят"
                        : "[RX] дубликат");

                    // Симуляция потери ACK
                    if (_rnd.NextDouble() >= _ackLoss)
                    {
                        Console.WriteLine("[TX] ACK");
                        AckReceived();
                        return; // успешная доставка
                    }

                    Console.WriteLine("… ACK потерян");
                }

                // Таймаут и повтор
                _delay.Delay(timeoutMs);
                Retries++;
                State = SenderState.Idle;
                Console.WriteLine("[FSM] таймаут → повтор");
            }
        }

        private void AckReceived()
        {
            Sequence = 1 - Sequence;
            State = SenderState.Idle;
        }
    }

    public sealed class Receiver
    {
        public ReceiverState State { get; } = ReceiverState.WaitingForPacket;
        private int _expectedSeq = 0;

        // Возвращает true, если пакет новый (не дубликат)
        public bool Receive(Packet p)
        {
            bool fresh = p.SequenceNumber == _expectedSeq;
            if (fresh)
                _expectedSeq = 1 - _expectedSeq;
            return fresh;
        }
    }
}
