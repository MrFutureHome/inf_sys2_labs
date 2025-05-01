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
            PatternMatcher matcher = new PatternMatcher(pattern);
            List<bool> result = PatternMatcher.DetectPattern(input, pattern);
            txtBoxOutput.Text = "[" + string.Join(", ", result).ToLower() + "]";
        }

        private void btnFindMatches_Click(object sender, EventArgs e)
        {
            findMatches(txtBoxPattern.Text.Trim(), txtBoxString.Text.Trim());
        }
    }
}
