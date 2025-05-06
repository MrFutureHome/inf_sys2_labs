using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab2
{
    public class State
    {
        public int Id;
        public Dictionary<char, List<State>> Transitions = new();
        public List<State> EpsilonTransitions = new();

        public State(int id)
        {
            Id = id;
        }
    }
}
