
// для запуска надо прописать необходимый код в файл script.mlg,
// поместить его в папку debug и добавить в параметры запуска script.mlg --dump
// (либо просто перетащить файл на экзешник)
// если не нужна отладка, то просто script.mlg

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;


// лексический анализатор

namespace MiniLang;

#region Токены

//типы токенов
public enum TokenType
{
    // Литералы
    Identifier, Number, String,
    // Ключевые слова
    True, False, Function, If, Then, Else, While, Do, Return, Print, Input, And, Or, Not,
    // Операторы и знаки препинания
    Plus, Minus, Star, Slash, Equal, EqualEqual, NotEqual,
    Less, LessEqual, Greater, GreaterEqual,
    Assign,   // =
    LeftParen, RightParen, LeftBrace, RightBrace,
    Comma, Semicolon,
    Eof
}

public readonly record struct Token(TokenType Type, string Lexeme, int Line, int Column);

#endregion

#region Lexer
public sealed class Lexer
{
    private readonly string _src;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    private static readonly Dictionary<string, TokenType> _keywords = new(StringComparer.Ordinal)
    {
        ["true"] = TokenType.True,
        ["false"] = TokenType.False,
        ["function"] = TokenType.Function,
        ["if"] = TokenType.If,
        ["then"] = TokenType.Then,
        ["else"] = TokenType.Else,
        ["while"] = TokenType.While,
        ["do"] = TokenType.Do,
        ["return"] = TokenType.Return,
        ["print"] = TokenType.Print,
        ["input"] = TokenType.Input,
        ["and"] = TokenType.And,
        ["or"] = TokenType.Or,
        ["not"] = TokenType.Not,
    };

    public Lexer(string src) => _src = src;

    public IEnumerable<Token> Scan()
    {
        while (!IsAtEnd)
            yield return NextToken();

        yield return new Token(TokenType.Eof, string.Empty, _line, _col);
    }

    private Token NextToken()
    {
        SkipWhitespaceAndComments();
        if (IsAtEnd)
            return new Token(TokenType.Eof, string.Empty, _line, _col);

        char c = Advance();
        switch (c)
        {
            // односимвольные
            case '(': return Make(TokenType.LeftParen);
            case ')': return Make(TokenType.RightParen);
            case '{': return Make(TokenType.LeftBrace);
            case '}': return Make(TokenType.RightBrace);
            case ',': return Make(TokenType.Comma);
            case ';': return Make(TokenType.Semicolon);
            case '+': return Make(TokenType.Plus);
            case '-': return Make(TokenType.Minus);
            case '*': return Make(TokenType.Star);
            case '/':
                return Make(TokenType.Slash);
            case '=':
                return Make(Match('=') ? TokenType.EqualEqual : TokenType.Assign,
                             Match('=') ? "==" : "=");
            case '!':
                return Make(Match('=') ? TokenType.NotEqual : throw Error("Не ожидалось '!'. Может, вы имели в виду '!='?"),
                             Match('=') ? "!=" : "!");
            case '<':
                return Make(Match('=') ? TokenType.LessEqual : TokenType.Less,
                             Match('=') ? "<=" : "<");
            case '>':
                return Make(Match('=') ? TokenType.GreaterEqual : TokenType.Greater,
                             Match('=') ? ">=" : ">");
            case '"':
                return String();
            default:
                if (char.IsDigit(c)) return Number(c);
                if (char.IsLetter(c) || c == '_') return Identifier(c);
                throw Error($"Неожиданный символ '{c}'.");
        }
    }

    private Token Identifier(char first)
    {
        var startPos = _pos - 1;
        while (!IsAtEnd && (char.IsLetterOrDigit(Peek) || Peek == '_')) Advance();
        var text = _src.Substring(startPos, _pos - startPos);
        var type = _keywords.TryGetValue(text, out var kw) ? kw : TokenType.Identifier;
        return new Token(type, text, _line, _col - text.Length);
    }

    private Token Number(char first)
    {
        var startPos = _pos - 1;
        while (!IsAtEnd && char.IsDigit(Peek)) Advance();
        var text = _src.Substring(startPos, _pos - startPos);
        return new Token(TokenType.Number, text, _line, _col - text.Length);
    }

    private Token String()
    {
        var startLine = _line;
        int startPos = _pos;
        while (!IsAtEnd && Peek != '"')
        {
            if (Peek == '\n') { _line++; _col = 1; }
            Advance();
        }
        if (IsAtEnd) throw ErrorAt(startLine, 1, "Неопределённая строка");
        Advance(); // закрывающая кавычка
        var lexeme = _src.Substring(startPos, _pos - startPos - 1);
        return new Token(TokenType.String, lexeme, startLine, _col - lexeme.Length - 2);
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd)
        {
            switch (Peek)
            {
                case ' ' or '\r' or '\t': Advance(); break;
                case '\n': Advance(); break;
                case '/':
                    if (PeekNext == '/')
                    {
                        while (!IsAtEnd && Peek != '\n') Advance();
                    }
                    else return;
                    break;
                default: return;
            }
        }
    }

    private Token Make(TokenType type, string? lexemeOverride = null)
        => new(type, lexemeOverride ?? _src[_pos - 1].ToString(), _line, _col - 1);

    private char Advance()
    {
        char ch = _src[_pos++];
        _col++;
        return ch;
    }

    private bool Match(char expected)
    {
        if (IsAtEnd || _src[_pos] != expected) return false;
        _pos++; _col++;
        return true;
    }

    private char Peek => IsAtEnd ? '\0' : _src[_pos];
    private char PeekNext => _pos + 1 >= _src.Length ? '\0' : _src[_pos + 1];
    private bool IsAtEnd => _pos >= _src.Length;

    private Exception Error(string message) => new($"[Lex] line {_line}, col {_col}: {message}");
    private static Exception ErrorAt(int line, int col, string message) => new($"[Lex] line {line}, col {col}: {message}");
}
#endregion

// АСТ и утилиты

#region AST
public interface IExpr { T Accept<T>(IExprVisitor<T> v); }
public interface IExprVisitor<out T>
{
    T VisitBinary(BinaryExpr e);
    T VisitUnary(UnaryExpr e);
    T VisitLiteral(LiteralExpr e);
    T VisitVariable(VariableExpr e);
    T VisitAssign(AssignExpr e);
    T VisitLogical(LogicalExpr e);
    T VisitCall(CallExpr e);
}

public sealed record BinaryExpr(IExpr Left, Token Op, IExpr Right) : IExpr
{ public T Accept<T>(IExprVisitor<T> v) => v.VisitBinary(this); }
public sealed record UnaryExpr(Token Op, IExpr Right) : IExpr
{ public T Accept<T>(IExprVisitor<T> v) => v.VisitUnary(this); }
public sealed record LiteralExpr(object? Value) : IExpr
{ public T Accept<T>(IExprVisitor<T> v) => v.VisitLiteral(this); }
public sealed record VariableExpr(Token Name) : IExpr
{ public T Accept<T>(IExprVisitor<T> v) => v.VisitVariable(this); }
public sealed record AssignExpr(Token Name, IExpr Value) : IExpr
{ public T Accept<T>(IExprVisitor<T> v) => v.VisitAssign(this); }
public sealed record LogicalExpr(IExpr Left, Token Op, IExpr Right) : IExpr
{ public T Accept<T>(IExprVisitor<T> v) => v.VisitLogical(this); }
public sealed record CallExpr(IExpr Callee, Token Paren, List<IExpr> Args) : IExpr
{ public T Accept<T>(IExprVisitor<T> v) => v.VisitCall(this); }

// --- Стейтменты
public interface IStmt { void Accept(IStmtVisitor v); }
public interface IStmtVisitor
{
    void VisitExprStmt(ExprStmt s);
    void VisitPrintStmt(PrintStmt s);
    void VisitVarStmt(VarStmt s);
    void VisitBlockStmt(BlockStmt s);
    void VisitIfStmt(IfStmt s);
    void VisitWhileStmt(WhileStmt s);
    void VisitFunctionStmt(FunctionStmt s);
    void VisitReturnStmt(ReturnStmt s);
}

public sealed record ExprStmt(IExpr Expression) : IStmt
{ public void Accept(IStmtVisitor v) => v.VisitExprStmt(this); }
public sealed record PrintStmt(IExpr Expression) : IStmt
{ public void Accept(IStmtVisitor v) => v.VisitPrintStmt(this); }
public sealed record VarStmt(Token Name, IExpr? Initializer) : IStmt
{ public void Accept(IStmtVisitor v) => v.VisitVarStmt(this); }
public sealed record BlockStmt(List<IStmt> Statements) : IStmt
{ public void Accept(IStmtVisitor v) => v.VisitBlockStmt(this); }
public sealed record IfStmt(IExpr Condition, IStmt ThenBranch, IStmt? ElseBranch) : IStmt
{ public void Accept(IStmtVisitor v) => v.VisitIfStmt(this); }
public sealed record WhileStmt(IExpr Condition, IStmt Body) : IStmt
{ public void Accept(IStmtVisitor v) => v.VisitWhileStmt(this); }
public sealed record FunctionStmt(Token Name, List<Token> Params, List<IStmt> Body) : IStmt
{ public void Accept(IStmtVisitor v) => v.VisitFunctionStmt(this); }
public sealed record ReturnStmt(Token Keyword, IExpr? Value) : IStmt
{ public void Accept(IStmtVisitor v) => v.VisitReturnStmt(this); }
#endregion


// синтаксический анализ

#region Parser
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _current;

    public Parser(IEnumerable<Token> tokens) => _tokens = tokens.ToList();

    public List<IStmt> Parse()
    {
        var stmts = new List<IStmt>();
        while (!IsAtEnd) stmts.Add(Declaration());
        return stmts;
    }

    // <declaration> ::= "function" IDENT "(" params? ")" block | statement
    private IStmt Declaration()
    {
        try
        {
            if (Match(TokenType.Function)) return Function("function");
            return Statement();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Synchronize();
            return new ExprStmt(new LiteralExpr(null));
        }
    }

    private FunctionStmt Function(string kind)
    {
        var name = Consume(TokenType.Identifier, $"Ожидалось {kind} название.");
        Consume(TokenType.LeftParen, "Ожидалось '('.");
        var parameters = new List<Token>();
        if (!Check(TokenType.RightParen))
        {
            do
            {
                if (parameters.Count >= 255) Error(Peek, "Превышен лимит в 255 параметров.");
                parameters.Add(Consume(TokenType.Identifier, "Ожидалось имя параметра."));
            } while (Match(TokenType.Comma));
        }
        Consume(TokenType.RightParen, "Ожидалось ')' после параметра");
        Consume(TokenType.LeftBrace, "Ожидалось '{' перед телом функции");
        var body = Block();
        return new FunctionStmt(name, parameters, body);
    }

    // <statement>
    private IStmt Statement()
    {
        if (Match(TokenType.Return)) return ReturnStatement();
        if (Match(TokenType.Print)) return PrintStatement();
        if (Match(TokenType.LeftBrace)) return new BlockStmt(Block());
        if (Match(TokenType.If)) return IfStatement();
        if (Match(TokenType.While)) return WhileStatement();
        return ExpressionStatement();
    }

    private IStmt ReturnStatement()
    {
        var keyword = Previous;
        IExpr? value = null;
        if (!Check(TokenType.Semicolon) && !Check(TokenType.Eof))
            value = Expression();
        OptionalSemicolon();
        return new ReturnStmt(keyword, value);
    }

    private IStmt WhileStatement()
    {
        var cond = Expression();
        Consume(TokenType.Do, "Ожидалось 'do' после условия.");
        var body = Statement();
        return new WhileStmt(cond, body);
    }

    private IStmt IfStatement()
    {
        var cond = Expression();
        Consume(TokenType.Then, "Ожидалось 'then'.");
        var thenBranch = Statement();
        IStmt? elseBranch = null;
        if (Match(TokenType.Else)) elseBranch = Statement();
        return new IfStmt(cond, thenBranch, elseBranch);
    }

    private List<IStmt> Block()
    {
        var stmts = new List<IStmt>();
        while (!Check(TokenType.RightBrace) && !IsAtEnd)
            stmts.Add(Declaration());
        Consume(TokenType.RightBrace, "Ожидалось '}' после блока.");
        return stmts;
    }

    private IStmt PrintStatement()
    {
        var value = Expression();
        OptionalSemicolon();
        return new PrintStmt(value);
    }

    private IStmt ExpressionStatement()
    {
        var expr = Expression();
        OptionalSemicolon();
        return new ExprStmt(expr);
    }

    private void OptionalSemicolon() { if (Match(TokenType.Semicolon)) { } }

    private IExpr Expression() => Assignment();

    private IExpr Assignment()
    {
        var expr = Or();
        if (Match(TokenType.Assign))
        {
            var equals = Previous;
            var value = Assignment();
            if (expr is VariableExpr ve)
                return new AssignExpr(ve.Name, value);
            Error(equals, "Неверный объект присваивания.");
        }
        return expr;
    }

    private IExpr Or()
    {
        var expr = And();
        while (Match(TokenType.Or))
        {
            var op = Previous;
            var right = And();
            expr = new LogicalExpr(expr, op, right);
        }
        return expr;
    }

    private IExpr And()
    {
        var expr = Equality();
        while (Match(TokenType.And))
        {
            var op = Previous;
            var right = Equality();
            expr = new LogicalExpr(expr, op, right);
        }
        return expr;
    }

    private IExpr Equality()
    {
        var expr = Comparison();
        while (Match(TokenType.EqualEqual, TokenType.NotEqual))
        {
            var op = Previous;
            var right = Comparison();
            expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private IExpr Comparison()
    {
        var expr = Term();
        while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual))
        {
            var op = Previous; var right = Term(); expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private IExpr Term()
    {
        var expr = Factor();
        while (Match(TokenType.Plus, TokenType.Minus))
        {
            var op = Previous; var right = Factor(); expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private IExpr Factor()
    {
        var expr = Unary();
        while (Match(TokenType.Star, TokenType.Slash))
        {
            var op = Previous; var right = Unary(); expr = new BinaryExpr(expr, op, right);
        }
        return expr;
    }

    private IExpr Unary()
    {
        if (Match(TokenType.Not, TokenType.Minus))
        {
            var op = Previous; var right = Unary(); return new UnaryExpr(op, right);
        }
        return Call();
    }

    private IExpr Call()
    {
        IExpr expr = Primary();
        while (true)
        {
            if (Match(TokenType.LeftParen))
            {
                expr = FinishCall(expr);
            }
            else break;
        }
        return expr;
    }

    private IExpr FinishCall(IExpr callee)
    {
        var args = new List<IExpr>();
        if (!Check(TokenType.RightParen))
        {
            do
            {
                if (args.Count >= 255) Error(Peek, "Превышен лимит в 255 аргументов.");
                args.Add(Expression());
            } while (Match(TokenType.Comma));
        }
        var paren = Consume(TokenType.RightParen, "Ожидалось ')' после аргументов.");
        return new CallExpr(callee, paren, args);
    }

    private IExpr Primary()
    {
        if (Match(TokenType.False)) return new LiteralExpr(false);
        if (Match(TokenType.True)) return new LiteralExpr(true);
        if (Match(TokenType.Number))
        {
            int value = int.Parse(Previous.Lexeme, CultureInfo.InvariantCulture);
            return new LiteralExpr(value);
        }
        if (Match(TokenType.String)) return new LiteralExpr(Previous.Lexeme);
        if (Match(TokenType.Identifier)) return new VariableExpr(Previous);
        if (Match(TokenType.LeftParen))
        {
            var expr = Expression();
            Consume(TokenType.RightParen, "Ожидалось ')' после выражения.");
            return expr;
        }
        throw Error(Peek, "Ожидалось выражение.");
    }

    // -------------- helpers ---------------

    private bool Match(params TokenType[] types)
    {
        foreach (var t in types)
            if (Check(t)) { Advance(); return true; }
        return false;
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw Error(Peek, message);
    }

    private bool Check(TokenType type) => !IsAtEnd && Peek.Type == type;
    private Token Advance() { if (!IsAtEnd) _current++; return Previous; }
    private bool IsAtEnd => Peek.Type == TokenType.Eof;
    private Token Peek => _tokens[_current];
    private Token Previous => _tokens[_current - 1];

    private Exception Error(Token token, string message)
    {
        var where = token.Type == TokenType.Eof ? " at end" : $" at '{token.Lexeme}'";
        return new Exception($"[Parse] line {token.Line}{where}: {message}");
    }

    private void Synchronize()
    {
        Advance();
        while (!IsAtEnd)
        {
            if (Previous.Type == TokenType.Semicolon) return;
            switch (Peek.Type)
            {
                case TokenType.Function:
                case TokenType.If:
                case TokenType.While:
                case TokenType.Print: return;
            }
            Advance();
        }
    }
}
#endregion

//  интерпретатор

#region Interpreter

public sealed class Environment
{
    private readonly Dictionary<string, object?> _values = new();
    private readonly Environment? _enclosing;

    public Environment(Environment? enclosing = null) => _enclosing = enclosing;

    public void Define(string name, object? value) => _values[name] = value;

    public void Assign(Token name, object? value)
    {
        //if (_values.ContainsKey(name.Lexeme)) { _values[name.Lexeme] = value; return; }
        //if (_enclosing != null) { _enclosing.Assign(name, value); return; }
        //throw new Exception($"Неизвестная переменная '{name.Lexeme}'.");

        if (_values.ContainsKey(name.Lexeme)) { _values[name.Lexeme] = value; return; }
        if (_enclosing != null) { _enclosing.Assign(name, value); return; }
        _values[name.Lexeme] = value;
    }

    public object? Get(Token name)
    {
        if (_values.TryGetValue(name.Lexeme, out var val)) return val;
        if (_enclosing != null) return _enclosing.Get(name);
        throw new Exception($"Неизвестная переменная '{name.Lexeme}'.");
    }

    public bool TryGet(string name, out object? value)
    {
        if (_values.TryGetValue(name, out value)) return true;
        return _enclosing != null && _enclosing.TryGet(name, out value);
    }
}

public interface ICallable { int Arity { get; } object? Call(Interpreter interpreter, List<object?> args); }

public sealed class MiniFunction : ICallable
{
    private readonly FunctionStmt _declaration;
    private readonly Environment _closure;
    public MiniFunction(FunctionStmt decl, Environment closure) { _declaration = decl; _closure = closure; }
    public int Arity => _declaration.Params.Count;
    public object? Call(Interpreter interpreter, List<object?> args)
    {
        var env = new Environment(_closure);
        for (int i = 0; i < _declaration.Params.Count; i++)
            env.Define(_declaration.Params[i].Lexeme, args[i]);

        var savedLast = interpreter.LastResult;
        try
        {
            interpreter.ExecuteBlock(_declaration.Body, env);
        }
        catch (ReturnException ret)
        {
            return ret.Value;
        }

        if (env.TryGet("return", out var rVar)) return rVar;
        return interpreter.LastResult;
        }
    public override string ToString() => $"<fn {_declaration.Name.Lexeme}>";
}

public sealed class ReturnException : Exception { public object? Value; public ReturnException(object? value) => Value = value; }

public sealed class Interpreter : IExprVisitor<object?>, IStmtVisitor
{
    private Environment _globals = new();
    private Environment _env;
    private readonly bool _dump;
    private object? _lastResult;
    public object? LastResult => _lastResult;

    public Interpreter(bool dump = false)
    {
        _dump = dump;
        _env = _globals;
        // встроенные функции
        _globals.Define("print", new NativeFunc(1, args =>
        {
            try { Console.WriteLine(args[0]); }    // поток может быть уже закрыт
            catch (ObjectDisposedException) { }
            return null;
        }));
        _globals.Define("input", new NativeFunc(1, args => { Console.Write(args[0]); return Console.ReadLine(); }));
    }

    public void Interpret(IEnumerable<IStmt> statements)
    {
        foreach (var stmt in statements) stmt.Accept(this);
    }

    public object? VisitLiteral(LiteralExpr e) => e.Value;

    public object? VisitVariable(VariableExpr e) => _env.Get(e.Name);

    public object? VisitAssign(AssignExpr e)
    {
        var value = e.Value.Accept(this);
        _env.Assign(e.Name, value);
        _lastResult = value;
        return value;
    }

    public object? VisitUnary(UnaryExpr e)
    {
        var right = e.Right.Accept(this);
        return e.Op.Type switch
        {
            TokenType.Not => !IsTruthy(right),
            TokenType.Minus => -(int)right!,
            _ => throw new Exception("Неизвестный унарный операнд"),
        };
    }

    public object? VisitBinary(BinaryExpr e)
    {
        var left = e.Left.Accept(this);
        var right = e.Right.Accept(this);
        return e.Op.Type switch
        {
            TokenType.Plus => (int)left! + (int)right!,
            TokenType.Minus => (int)left! - (int)right!,
            TokenType.Star => (int)left! * (int)right!,
            TokenType.Slash => (int)left! / (int)right!,
            TokenType.EqualEqual => Equals(left, right),
            TokenType.NotEqual => !Equals(left, right),
            TokenType.Greater => (int)left! > (int)right!,
            TokenType.GreaterEqual => (int)left! >= (int)right!,
            TokenType.Less => (int)left! < (int)right!,
            TokenType.LessEqual => (int)left! <= (int)right!,
            _ => throw new Exception("Неизвестный бинарный операнд"),
        };
    }

    public object? VisitLogical(LogicalExpr e)
    {
        var left = e.Left.Accept(this);
        if (e.Op.Type == TokenType.Or)
        {
            if (IsTruthy(left)) return true;
        }
        else // And
        {
            if (!IsTruthy(left)) return false;
        }
        return e.Right.Accept(this);
    }

    public object? VisitCall(CallExpr e)
    {
        var callee = e.Callee.Accept(this);
        var args = e.Args.Select(a => a.Accept(this)).ToList();
        if (callee is ICallable fn)
        {
            if (args.Count != fn.Arity) throw new Exception($"Ожидалось {fn.Arity}, получено: {args.Count}.");
            return fn.Call(this, args);
        }
        throw new Exception("Может только вызывать функции.");
    }

    public void VisitExprStmt(ExprStmt s)
    {
        var v = s.Expression.Accept(this);
        _lastResult = v;
        if (_dump) Console.WriteLine($">> {v}");
    }

    public void VisitPrintStmt(PrintStmt s)
    {
        var v = s.Expression.Accept(this);
        _lastResult = v;
        Console.WriteLine(v);
    }

    public void VisitVarStmt(VarStmt s)
    {
        var value = s.Initializer?.Accept(this);
        _env.Define(s.Name.Lexeme, value);
        _lastResult = value;
    }

    public void VisitBlockStmt(BlockStmt s)
    {
        ExecuteBlock(s.Statements, new Environment(_env));
    }

    public void ExecuteBlock(List<IStmt> stmts, Environment env)
    {
        var previous = _env;
        try
        {
            _env = env;
            foreach (var stmt in stmts) stmt.Accept(this);
        }
        finally { _env = previous; }
    }

    public void VisitIfStmt(IfStmt s)
    {
        if (IsTruthy(s.Condition.Accept(this))) s.ThenBranch.Accept(this);
        else s.ElseBranch?.Accept(this);
    }

    public void VisitWhileStmt(WhileStmt s)
    {
        while (IsTruthy(s.Condition.Accept(this))) s.Body.Accept(this);
    }

    public void VisitReturnStmt(ReturnStmt s)
    {
        var val = s.Value?.Accept(this);
        throw new ReturnException(val);
    }

    public void VisitFunctionStmt(FunctionStmt s)
    {
        var fn = new MiniFunction(s, _env);
        _env.Define(s.Name.Lexeme, fn);
    }

    // helpers
    private static bool IsTruthy(object? v) => v switch { null => false, bool b => b, int i => i != 0, _ => true };
}

public sealed class NativeFunc : ICallable
{
    private readonly int _arity;
    private readonly Func<List<object?>, object?> _impl;
    public NativeFunc(int arity, Func<List<object?>, object?> impl) { _arity = arity; _impl = impl; }
    public int Arity => _arity;
    public object? Call(Interpreter interpreter, List<object?> args) => _impl(args);
    public override string ToString() => "<native fn>";
}

#endregion

// точка входа
public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0) { Console.WriteLine("Создайте в папке debug файл script.mlg и пропишите в свойства запуска script.mlg --dump"); return; }
        var path = args[0];
        var dump = args.Contains("--dump");
        var src = File.ReadAllText(path);
        var lexer = new Lexer(src);
        var tokens = lexer.Scan().ToList();
        if (dump)
        {
            Console.WriteLine("Токены:");
            foreach (var t in tokens) Console.WriteLine($"{t.Type} '{t.Lexeme}' ({t.Line}:{t.Column})");
        }
        var parser = new Parser(tokens);
        var statements = parser.Parse();
        if (dump)
        {
            Console.WriteLine("АСТ выражения: " + statements.Count);
        }
        var interp = new Interpreter(dump);
        interp.Interpret(statements);
    }
}