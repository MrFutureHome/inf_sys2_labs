using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace lab2__tcp_state_machine_
{

    //перечисления состояний стейт машины (шаблон RFC793 в упрощённом виде)
    public enum TcpState
    {
        CLOSED,
        LISTEN,
        SYN_SENT,
        SYN_RECEIVED,
        ESTABLISHED,
        FIN_WAIT_1,
        FIN_WAIT_2,
        TIME_WAIT,
        CLOSE_WAIT,
        LAST_ACK
    }

    public sealed class TcpStateMachine : IDisposable
    {
        private static readonly TimeSpan TIME_WAIT_DURATION = TimeSpan.FromSeconds(30); // 2×MSL (simplified)

        public TcpState CurrentState { get; private set; } = TcpState.CLOSED;

        /// <summary>
        /// Transition table: (State, Event) → side‑effect & next state.
        /// </summary>
        private readonly Dictionary<(TcpState, TcpEvent), Action> _transitions;

        private readonly Timer _timer = new Timer { AutoReset = false };

        public TcpStateMachine()
        {
            _transitions = BuildTransitions();
            _timer.Elapsed += (_, __) => ProcessEvent(TcpEvent.TIMEOUT);
        }

        /// <summary>
        /// Feed an external event (packet arrival, syscall, timer) into the FSM.
        /// </summary>
        public void ProcessEvent(TcpEvent evt)
        {
            if (_transitions.TryGetValue((CurrentState, evt), out var action))
            {
                action();
            }
            else
            {
                Console.WriteLine($"[ERROR] Event {evt} is invalid in state {CurrentState}.");
            }
        }

        // таблица переходов с рандомными эффектами (остановка/начало таймера)
        private Dictionary<(TcpState, TcpEvent), Action> BuildTransitions()
        {
            var t = new Dictionary<(TcpState, TcpEvent), Action>();

            void Add(TcpState from, TcpEvent e, TcpState to, Action? sideEffect = null)
            {
                t[(from, e)] = () =>
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {from} --({e})-> {to}");
                    CurrentState = to;
                    sideEffect?.Invoke();

                    // Manage TIME‑WAIT timer
                    if (to == TcpState.TIME_WAIT)
                    {
                        _timer.Interval = TIME_WAIT_DURATION.TotalMilliseconds;
                        _timer.Start();
                    }
                    else
                    {
                        _timer.Stop();
                    }
                };
            }

            // сами состояния
            // пассивное открытое (сервер)
            Add(TcpState.CLOSED, TcpEvent.APP_PASSIVE_OPEN, TcpState.LISTEN);
            Add(TcpState.LISTEN, TcpEvent.RCV_SYN, TcpState.SYN_RECEIVED);
            Add(TcpState.SYN_RECEIVED, TcpEvent.RCV_ACK, TcpState.ESTABLISHED);

            // активное открытое (клиент)
            Add(TcpState.CLOSED, TcpEvent.APP_ACTIVE_OPEN, TcpState.SYN_SENT);
            Add(TcpState.SYN_SENT, TcpEvent.RCV_SYN_ACK, TcpState.ESTABLISHED);

            // соединение установлено
            Add(TcpState.ESTABLISHED, TcpEvent.APP_SEND, TcpState.ESTABLISHED, () => Console.WriteLine("Data segment sent"));
            Add(TcpState.ESTABLISHED, TcpEvent.RCV_ACK, TcpState.ESTABLISHED, () => Console.WriteLine("ACK processed"));
            Add(TcpState.ESTABLISHED, TcpEvent.RCV_RST, TcpState.CLOSED);

            // активное закрытое (клиент)
            Add(TcpState.ESTABLISHED, TcpEvent.APP_CLOSE, TcpState.FIN_WAIT_1);
            Add(TcpState.FIN_WAIT_1, TcpEvent.RCV_ACK, TcpState.FIN_WAIT_2);
            Add(TcpState.FIN_WAIT_2, TcpEvent.RCV_FIN, TcpState.TIME_WAIT);
            Add(TcpState.TIME_WAIT, TcpEvent.TIMEOUT, TcpState.CLOSED);

            // пассивное открытое (сервер)
            Add(TcpState.ESTABLISHED, TcpEvent.RCV_FIN, TcpState.CLOSE_WAIT);
            Add(TcpState.CLOSE_WAIT, TcpEvent.APP_CLOSE, TcpState.LAST_ACK);
            Add(TcpState.LAST_ACK, TcpEvent.RCV_ACK, TcpState.CLOSED);

            return t;
        }

        public void Dispose() => _timer.Dispose();
    }

    //возможные события влияющие на стейт машину
    public enum TcpEvent
    {
        // события на уровне приложения
        APP_PASSIVE_OPEN,   // Сервер: socket(), bind(), listen()
        APP_ACTIVE_OPEN,    // Клиент: connect()
        APP_SEND,           // приложение отправляет данные
        APP_CLOSE,          // приложение закрывается

        // события на уровне пакета
        RCV_SYN,
        RCV_ACK,
        RCV_SYN_ACK,
        RCV_FIN,
        RCV_FIN_ACK,
        RCV_RST,

        // события на уровне таймера
        TIMEOUT
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            using var fsm = new TcpStateMachine();

            Console.WriteLine("TCP FSM demo. Enter events (e.g., APP_ACTIVE_OPEN, RCV_SYN). Type 'exit' to quit.\n");
            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine();
                if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
                    break;

                if (Enum.TryParse<TcpEvent>(input?.Trim(), true, out var evt))
                {
                    fsm.ProcessEvent(evt);
                    Console.WriteLine($"Current state: {fsm.CurrentState}\n");
                }
                else
                {
                    Console.WriteLine("Unknown event.\n");
                }
            }
        }
    }
}
