using Newtonsoft.Json.Linq;
using System.Diagnostics.Metrics;

namespace MiniLang.tests;



[TestClass]
public class LexerTests
{
    [TestMethod]
    public void Tokenize_SimpleExpression_ProducesExpectedTokens()
    {
        const string src = "print(1+2*3)";
        var tokens = new Lexer(src).Scan().ToList();
        var types = tokens.Select(t => t.Type).ToList();
        CollectionAssert.AreEqual(new[]
        {
            TokenType.Print, TokenType.LeftParen,
            TokenType.Number, TokenType.Plus, TokenType.Number,
            TokenType.Star,  TokenType.Number, TokenType.RightParen,
            TokenType.Eof
        }, types);
    }

    [DataTestMethod]
    [DataRow("\"hello\"", TokenType.String)]
    [DataRow("123", TokenType.Number)]
    public void Tokenize_Literals_CorrectType(string literal, TokenType expected)
    {
        var token = new Lexer(literal).Scan().First();
        Assert.AreEqual(expected, token.Type);
        Assert.AreEqual(literal.Replace("\"", string.Empty), token.Lexeme);
    }
}

[TestClass]
public class ParserTests
{
    private static IExpr ParseExpr(string src)
    {
        var tokens = new Lexer(src).Scan();
        var parser = new Parser(tokens);
        var stmt = (ExprStmt)parser.Parse().Single();
        return stmt.Expression;
    }

    [TestMethod]
    public void Parser_BinaryExpression_BuildsAst()
    {
        var expr = ParseExpr("1 + 2 * 3");
        Assert.IsInstanceOfType(expr, typeof(BinaryExpr));
        var bin = (BinaryExpr)expr;
        Assert.AreEqual(TokenType.Plus, bin.Op.Type);
        Assert.IsInstanceOfType(bin.Right, typeof(BinaryExpr));
    }
}

[TestClass]
public class SystemScriptTests
{
    static string Normalize(string s)
    => s.Replace("\r\n", "\n").TrimEnd();

    private static (string Output, object? Last) RunScript(string fileName)
    {
        var src = File.ReadAllText(fileName);
        var writer = new StringWriter();
        var oldOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var tokens = new Lexer(src).Scan();
            var stmts = new Parser(tokens).Parse();
            var interp = new Interpreter();
            interp.Interpret(stmts);
            return (writer.ToString().TrimEnd(), interp.LastResult);
        }
        finally { Console.SetOut(oldOut); }
    }

    [DataTestMethod]
    [DataRow("Scripts/Arithmetic.mlg", "8", 8)]
    [DataRow("Scripts/Square.mlg", "9", 9)]
    public void SimpleScripts(string path, string expectedOut, int expectedResult)
    {
        var (outStr, last) = RunScript(path);
        Assert.AreEqual(expectedOut, outStr);
        Assert.AreEqual(expectedResult, (int)last!);
    }

    [TestMethod]
    public void WhileLoop()
    {
        var expected = string.Join("\n", Enumerable.Range(0, 10));
        var (outStr, _) = RunScript("Scripts/WhileLoop.mlg");
        Assert.AreEqual(Normalize(expected), Normalize(outStr));
    }

    [TestMethod]
    public void Fibonacci()
    {
        var seq = new[] { 0, 1, 1, 2, 3, 5, 8, 13, 21, 34 };
        var expected = string.Join("\n", seq);
        var (outStr, _) = RunScript("Scripts/Fibonacci.mlg");
        Assert.AreEqual(Normalize(expected), Normalize(outStr));
    }
}

