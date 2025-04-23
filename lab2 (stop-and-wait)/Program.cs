using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace lab2__stop_and_wait_
{
    class Packet
    {
        public int SequenceNumber;
        public string Data;
    }
    internal class Program
    {
        static Random random = new Random();

        const float packetLossProbability = 0.5f; // вероятность потери пакета (0.0–1.0)
        const float ackLossProbability = 0.2f;    // вероятность потери ACK (0.0–1.0)
        const int timeoutMilliseconds = 2000;     // таймаут ожидания ACK
        const int totalPacketsToSend = 5;         // количество пакетов для передачи

        static bool IsLost(float lossProbability)
        {
            return random.NextDouble() < lossProbability;
        }

        static void Sender()
        {
            int sequenceNumber = 0;
            for (int i = 0; i < totalPacketsToSend;)
            {
                Packet packet = new Packet { SequenceNumber = sequenceNumber, Data = $"Data-{i}" };
                Console.WriteLine($"\n[Отправитель] Отправка пакета #{packet.SequenceNumber}");

                if (!IsLost(packetLossProbability))
                {
                    Console.WriteLine($"[Сеть] Пакет #{packet.SequenceNumber} доставлен получателю");
                    bool ackReceived = Receiver(packet);

                    if (ackReceived)
                    {
                        Console.WriteLine($"[Отправитель] ACK получен для пакета #{packet.SequenceNumber}");
                        sequenceNumber = 1 - sequenceNumber; // переключение между 0 и 1
                        i++;
                    }
                    else
                    {
                        Console.WriteLine($"[Отправитель] ACK потерян. Повторная отправка пакета #{packet.SequenceNumber}");
                        Thread.Sleep(timeoutMilliseconds);
                    }
                }
                else
                {
                    Console.WriteLine($"[Сеть] Пакет #{packet.SequenceNumber} потерян. Ожидание таймаута...");
                    Thread.Sleep(timeoutMilliseconds);
                }
            }

            Console.WriteLine("\n[Отправитель] Все пакеты успешно переданы!");
        }

        static bool Receiver(Packet packet)
        {
            Console.WriteLine($"[Получатель] Получен пакет #{packet.SequenceNumber}: \"{packet.Data}\"");

            if (IsLost(ackLossProbability))
            {
                Console.WriteLine($"[Сеть] ACK для пакета #{packet.SequenceNumber} потерян");
                return false;
            }

            Console.WriteLine($"[Получатель] Отправка ACK для пакета #{packet.SequenceNumber}");
            return true;
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
