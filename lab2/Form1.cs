using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace lab2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void findMatches(string pattern, string input)
        {
            //PatternMatcher matcher = new PatternMatcher(pattern);
            List<bool> result = PatternMatcher.DetectPattern(input, pattern);
            txtBoxOutput.Text = "[" + string.Join(", ", result).ToLower() + "]";
        }

        public static string PrintNFA(State state, HashSet<int> visited)
        {
            string output="";
            if (visited.Contains(state.Id))
                return output;

            visited.Add(state.Id);

            foreach (var kvp in state.Transitions)
            {
                foreach (var target in kvp.Value)
                {
                    output += $"  {state.Id} --'{kvp.Key}'--> {target.Id}\n";
                    //PrintNFA(target, visited);
                }
            }

            foreach (var target in state.EpsilonTransitions)
            {
                output += $"  {state.Id} --ε--> {target.Id}\n";
                //PrintNFA(target, visited);
            }
            return output;
        }

        private void btnFindMatches_Click(object sender, EventArgs e)
        {
            findMatches(txtBoxPattern.Text.Trim(), txtBoxString.Text.Trim());
        }

        private void btnBuildNFA_Click(object sender, EventArgs e)
        {
            string regex = txtBoxRegularExp.Text.Trim();
            var nfa = RegexToNFA.Build(regex);

            txtBoxStartCondition.Text = nfa.Start.Id.ToString();
            txtBoxFinalCondition.Text = nfa.Accept.Id.ToString();

            txtBoxOutput2.Text = PrintNFA(nfa.Start, new HashSet<int>());
        }
    }
}
