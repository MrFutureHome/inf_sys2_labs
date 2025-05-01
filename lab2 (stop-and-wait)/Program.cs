using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace lab2__stop_and_wait_
{
    //перечисление возможных состояний у отправителя и получателя
    enum SenderState
    {
        Idle,
        WaitingForAck,
        Timeout
    }

    enum ReceiverState
    {
        WaitingForPacket
    }

    class Packet
    {
        public int SequenceNumber;
        public string Data;
    }
    internal class Program
    {
        const int totalPacketsToSend = 5;
        const float packetLossProbability = 0.2f;
        const float ackLossProbability = 0.2f;
        const int timeoutMilliseconds = 1000;

        static Random random = new Random();
        static SenderState senderState = SenderState.Idle;
        static ReceiverState receiverState = ReceiverState.WaitingForPacket;

        static bool IsLost(float probability) => random.NextDouble() < probability;

        static bool ReceiverFSM(Packet packet)
        {
            if (IsLost(packetLossProbability))
            {
                Console.WriteLine($"[Сеть] Пакет #{packet.SequenceNumber} потерян");
                return false;
            }

            if (receiverState == ReceiverState.WaitingForPacket)
            {
                Console.WriteLine($"[Получатель] Получен пакет \"{packet.Data}\"");

                if (IsLost(ackLossProbability))
                {
                    Console.WriteLine($"[Сеть] ACK для пакета #{packet.SequenceNumber} потерян");
                    return false;
                }

                Console.WriteLine($"[Получатель] Отправлен ACK для пакета #{packet.SequenceNumber}");
                return true;
            }

            return false;
        }

        static void Sender()
        {
            int packetsSent = 0;
            int sequenceNumber = 0;

            while (packetsSent < totalPacketsToSend)
            {
                Packet packet = new Packet
                {
                    //sequenceNumber чередуется только между 1 и 0, чтобы отличать повторную передачу и новый пакет
                    SequenceNumber = sequenceNumber,
                    Data = $"Data-{packetsSent}"
                };

                switch (senderState)
                {
                    case SenderState.Idle:
                        Console.WriteLine($"\n[Отправитель] Отправка пакета #{packet.SequenceNumber}");
                        senderState = SenderState.WaitingForAck;
                        goto case SenderState.WaitingForAck;

                    case SenderState.WaitingForAck:
                        bool ackReceived = ReceiverFSM(packet);

                        if (ackReceived)
                        {
                            Console.WriteLine($"[Отправитель] Получен ACK для пакета #{packet.SequenceNumber}");
                            sequenceNumber = 1 - sequenceNumber;
                            packetsSent++;
                            senderState = SenderState.Idle;
                        }
                        else
                        {
                            Console.WriteLine($"[Отправитель] ACK не получен. Ожидание таймаута...");
                            Thread.Sleep(timeoutMilliseconds);
                            senderState = SenderState.Timeout;
                        }
                        break;

                    case SenderState.Timeout:
                        Console.WriteLine($"[Отправитель] Повторная отправка пакета #{packet.SequenceNumber}");
                        senderState = SenderState.WaitingForAck;
                        break;
                }
            }

            Console.WriteLine("\n[Отправитель] Все пакеты успешно переданы.");
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Симулятор протокола Stop-and-Wait ===\n");
            Sender();
            Console.WriteLine("\n=== Завершено ===");
            Console.ReadKey();
        }
    }
}
