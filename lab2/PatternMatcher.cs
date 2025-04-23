using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab2
{
    public class PatternMatcher
    {
        private readonly string pattern;
        private readonly int[] prefixFunction;

        public PatternMatcher(string pattern)
        {
            this.pattern = pattern;
            this.prefixFunction = BuildPrefixFunction(pattern);
        }

        private int[] BuildPrefixFunction(string pattern)
        {
            int n = pattern.Length;
            int[] pi = new int[n];
            int j = 0;

            for (int i = 1; i < n; i++)
            {
                while (j > 0 && pattern[i] != pattern[j])
                {
                    j = pi[j - 1];
                }
                if (pattern[i] == pattern[j])
                {
                    j++;
                }
                pi[i] = j;
            }

            return pi;
        }

        public List<bool> Process(string input)
        {
            List<bool> result = new List<bool>();
            int j = 0;

            for (int i = 0; i < input.Length; i++)
            {
                while (j > 0 && input[i] != pattern[j])
                {
                    j = prefixFunction[j - 1];
                }

                if (input[i] == pattern[j])
                {
                    j++;
                }

                if (j == pattern.Length)
                {
                    result.Add(true);
                    j = prefixFunction[j - 1];
                }
                else
                {
                    result.Add(false);
                }
            }

            return result;
        }
    }
}
