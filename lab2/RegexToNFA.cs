using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab2
{
    public class RegexToNFA
    {
        private static int _stateId;

        public static NFA Build(string regex)
        {
            _stateId = 0;
            Stack<NFA> operands = new();
            string postfix = ToPostfix(regex);
            foreach (char token in postfix)
            {
                if (token == '*')
                {
                    var nfa = operands.Pop();
                    operands.Push(Star(nfa));
                }
                else if (token == '|')
                {
                    var nfa2 = operands.Pop();
                    var nfa1 = operands.Pop();
                    operands.Push(Union(nfa1, nfa2));
                }
                else if (token == '.')
                {
                    var nfa2 = operands.Pop();
                    var nfa1 = operands.Pop();
                    operands.Push(Concat(nfa1, nfa2));
                }
                else
                {
                    operands.Push(Symbol(token));
                }
            }

            return operands.Pop();
        }

        private static string ToPostfix(string regex)
        {
            string output = "";
            Stack<char> stack = new();
            string expanded = AddConcatOperators(regex);
            Dictionary<char, int> precedence = new()
            {
                { '|', 1 }, { '.', 2 }, { '*', 3 }
            };

            foreach (char token in expanded)
            {
                if (char.IsLetterOrDigit(token))
                    output += token;
                else if (token == '(')
                    stack.Push(token);
                else if (token == ')')
                {
                    while (stack.Peek() != '(')
                        output += stack.Pop();
                    stack.Pop();
                }
                else
                {
                    while (stack.Count > 0 && stack.Peek() != '(' &&
                           precedence[stack.Peek()] >= precedence[token])
                        output += stack.Pop();
                    stack.Push(token);
                }
            }

            while (stack.Count > 0)
                output += stack.Pop();

            return output;
        }

        private static string AddConcatOperators(string regex)
        {
            string result = "";
            for (int i = 0; i < regex.Length; i++)
            {
                result += regex[i];
                if (i + 1 < regex.Length &&
                    (char.IsLetterOrDigit(regex[i]) || regex[i] == ')' || regex[i] == '*') &&
                    (char.IsLetterOrDigit(regex[i + 1]) || regex[i + 1] == '('))
                {
                    result += '.';
                }
            }
            return result;
        }

        private static NFA Symbol(char c)
        {
            State start = new(_stateId++);
            State accept = new(_stateId++);
            start.Transitions[c] = new() { accept };
            return new NFA(start, accept);
        }

        private static NFA Concat(NFA first, NFA second)
        {
            first.Accept.EpsilonTransitions.Add(second.Start);
            return new NFA(first.Start, second.Accept);
        }

        private static NFA Union(NFA first, NFA second)
        {
            State start = new(_stateId++);
            State accept = new(_stateId++);
            start.EpsilonTransitions.Add(first.Start);
            start.EpsilonTransitions.Add(second.Start);
            first.Accept.EpsilonTransitions.Add(accept);
            second.Accept.EpsilonTransitions.Add(accept);
            return new NFA(start, accept);
        }

        private static NFA Star(NFA nfa)
        {
            State start = new(_stateId++);
            State accept = new(_stateId++);
            start.EpsilonTransitions.Add(nfa.Start);
            start.EpsilonTransitions.Add(accept);
            nfa.Accept.EpsilonTransitions.Add(nfa.Start);
            nfa.Accept.EpsilonTransitions.Add(accept);
            return new NFA(start, accept);
        }
    }
}
