using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab2
{
    public class NFA
    {
        public State Start;
        public State Accept;

        public NFA(State start, State accept)
        {
            Start = start;
            Accept = accept;
        }
    }
}
