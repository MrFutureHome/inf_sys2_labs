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
        private readonly int[] prefix;
        private int currentState;

        public PatternMatcher(string pattern)
        {
            this.pattern = pattern;
            this.prefix = ComputePrefixFunction(pattern);
            this.currentState = 0;
        }

        private int[] ComputePrefixFunction(string s)
        {
            int n = s.Length;
            int[] prefix = new int[n];
            prefix[0] = 0;
            int k = 0;
            for (int i = 1; i < n; i++)
            {
                while (k > 0 && s[k] != s[i])
                    k = prefix[k - 1];
                if (s[k] == s[i])
                    k++;
                prefix[i] = k;
            }
            return prefix;
        }

        public bool ProcessChar(char c)
        {
            while (currentState > 0 && pattern[currentState] != c)
                currentState = prefix[currentState - 1];

            if (pattern[currentState] == c)
                currentState++;

            if (currentState == pattern.Length)
            {
                currentState = prefix[currentState - 1];
                return true;
            }

            return false;
        }

        public static List<bool> DetectPattern(string input, string pattern)
        {
            PatternMatcher detector = new PatternMatcher(pattern);
            List<bool> result = new List<bool>();

            foreach (char c in input)
                result.Add(detector.ProcessChar(c));

            return result;
        }
    }
}
