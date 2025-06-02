using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HttpLogAnalyzer
{
    class Program
    {
        // Структура для хранения опций, переданных в командной строке
        private class Options
        {
            public string Format { get; set; } = "clf";   // "clf", "combined" или "custom"
            public string LogFile { get; set; } = null;   // путь к файлу логов
            public string ConfigFile { get; set; } = null; // для custom‐формата: путь к JSON
            public int TopIp { get; set; } = 0;    // --topip N
            public int TopUrl { get; set; } = 0;   // --topurl N
            public int TopUa { get; set; } = 0;    // --topua N
            public bool ShowCodes { get; set; } = false;  // --codes
            public string SearchTerm { get; set; } = null; // --search term
            public DateTime? FromDate { get; set; } = null; // --from yyyy-MM-dd
            public DateTime? ToDate { get; set; } = null;   // --to yyyy-MM-dd
            public string Bucket { get; set; } = null;      // --bucket hour|day
        }

        // Если формат = "custom", ожидаемый JSON‐конфиг:
        // {
        //   "regex": "^(?<ip>\\S+) \\S+ \\S+ \\[(?<date>[^\\]]+)\\] \\\"(?<request>[^\\\"]+)\\\" (?<status>\\d{3}) (?<size>\\S+)(?: \\\"(?<referer>[^\\\"]*)\\\" \\\"(?<useragent>[^\\\"]*)\\\")?$",
        //   "dateFormat": "dd/MMM/yyyy:HH:mm:ss zzz"
        // }
        private class CustomConfig
        {
            public string Regex { get; set; }
            public string DateFormat { get; set; }
        }

        static void Main(string[] args)
        {
            Options opt;
            try
            {
                opt = ParseArguments(args);
                if (opt == null)
                {
                    // Пользователь запросил помощь или передал некорректные параметры
                    return;
                }
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Ошибка: {ex.Message}");
                ShowHelp();
                return;
            }

            // Проверка наличия файла логов
            if (string.IsNullOrEmpty(opt.LogFile) || !File.Exists(opt.LogFile))
            {
                Console.Error.WriteLine("Ошибка: файл логов не указан или не существует.");
                return;
            }

            // Подготовка регулярного выражения и строки формата даты
            Regex lineRegex = null;
            string dateFormat = null;
            if (opt.Format.Equals("clf", StringComparison.OrdinalIgnoreCase))
            {
                // CLF: IP ident authuser [date] "request" status size
                // Пример: 127.0.0.1 - frank [10/Oct/2000:13:55:36 -0700] "GET /apache_pb.gif HTTP/1.0" 200 2326
                // Названные группы: ip, date, request, status, size
                string clfPattern =
                    @"^(?<ip>\S+) \S+ \S+ \[(?<date>[^\]]+)\] ""(?<request>[^""]+)"" (?<status>\d{3}) (?<size>\S+)$";
                lineRegex = new Regex(clfPattern, RegexOptions.Compiled);
                dateFormat = "dd/MMM/yyyy:HH:mm:ss zzz";
            }
            else if (opt.Format.Equals("combined", StringComparison.OrdinalIgnoreCase))
            {
                // Combined: CLF + "referer" "useragent"
                // Пример: 127.0.0.1 - frank [10/Oct/2000:13:55:36 -0700] "GET /apache_pb.gif HTTP/1.0" 200 2326 "http://example.com/start.html" "Mozilla/4.08 [en] (Win98; I ;Nav)"
                // Названные группы: ip, date, request, status, size, referer, useragent
                string combPattern =
                    @"^(?<ip>\S+) \S+ \S+ \[(?<date>[^\]]+)\] ""(?<request>[^""]+)"" (?<status>\d{3}) (?<size>\S+) ""(?<referer>[^""]*)"" ""(?<useragent>[^""]*)""$";
                lineRegex = new Regex(combPattern, RegexOptions.Compiled);
                dateFormat = "dd/MMM/yyyy:HH:mm:ss zzz";
            }
            else if (opt.Format.Equals("custom", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(opt.ConfigFile) || !File.Exists(opt.ConfigFile))
                {
                    Console.Error.WriteLine("Ошибка: для custom‐формата необходимо указать корректный JSON‐файл через --config.");
                    return;
                }

                // Считываем JSON из opt.ConfigFile
                CustomConfig cfg = null;
                try
                {
                    string jsonText = File.ReadAllText(opt.ConfigFile, Encoding.UTF8);
                    cfg = JsonSerializer.Deserialize<CustomConfig>(jsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Ошибка при чтении custom‐конфига: {ex.Message}");
                    return;
                }

                if (string.IsNullOrEmpty(cfg.Regex) || string.IsNullOrEmpty(cfg.DateFormat))
                {
                    Console.Error.WriteLine("Ошибка: JSON‐конфиг должен содержать свойства \"regex\" и \"dateFormat\".");
                    return;
                }

                try
                {
                    lineRegex = new Regex(cfg.Regex, RegexOptions.Compiled);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Ошибка: некорректное регулярное выражение в JSON: {ex.Message}");
                    return;
                }

                dateFormat = cfg.DateFormat;
            }
            else
            {
                Console.Error.WriteLine($"Ошибка: неизвестный формат \"{opt.Format}\". Ожидаются: clf, combined или custom.");
                return;
            }

            // Параллельные словари для подсчёта
            var ipCounts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
            var urlCounts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
            var uaCounts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
            var statusCounts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
            var bucketCounts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

            // Если требуется поиск, приведём к нижнему регистру для сравнения
            string searchLower = opt.SearchTerm?.ToLowerInvariant();

            // Обработка строк файла параллельно
            try
            {
                File.ReadLines(opt.LogFile)
                    .AsParallel()
                    .WithDegreeOfParallelism(Environment.ProcessorCount)
                    .ForAll(line =>
                    {
                        if (string.IsNullOrWhiteSpace(line)) return;

                        var m = lineRegex.Match(line);
                        if (!m.Success) return;

                        // Парсим дату (убираем скобки, затем корректируем формат смещения)
                        string rawDate = m.Groups["date"].Value; // например: 10/Oct/2000:13:55:36 -0700
                        string dateForParse = rawDate;
                        // Если смещение без двоеточия (например, -0700), вставляем:
                        int lastSpace = rawDate.LastIndexOf(' ');
                        if (lastSpace >= 0 && rawDate.Length - lastSpace == 6)
                        {
                            string offset = rawDate.Substring(lastSpace + 1); // "-0700"
                            if (offset.Length == 5 && (offset[0] == '+' || offset[0] == '-'))
                            {
                                // вставляем двоеточие: "-07:00"
                                string corrected = offset.Insert(3, ":");
                                dateForParse = rawDate.Substring(0, lastSpace + 1) + corrected;
                            }
                        }

                        DateTime logDateUtc;
                        try
                        {
                            logDateUtc = DateTime.ParseExact(
                                dateForParse,
                                dateFormat,
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                        }
                        catch
                        {
                            // Невозможно распарсить дату — пропускаем строку
                            return;
                        }

                        // Фильтрация по диапазону дат, если указано
                        if (opt.FromDate.HasValue)
                        {
                            // Сравниваем по дате (игнорируем время)
                            if (logDateUtc.Date < opt.FromDate.Value.Date) return;
                        }
                        if (opt.ToDate.HasValue)
                        {
                            if (logDateUtc.Date > opt.ToDate.Value.Date) return;
                        }

                        // Разбор поля request
                        string request = m.Groups["request"].Value; // например: GET /path HTTP/1.0
                        string url = null;
                        {
                            var parts = request.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                                url = parts[1];
                            else
                                url = request;
                        }

                        // Поиск по URL, если указано --search
                        if (!string.IsNullOrEmpty(searchLower))
                        {
                            if (url == null || !url.ToLowerInvariant().Contains(searchLower))
                                return;
                        }

                        // IP
                        string ip = m.Groups["ip"].Value;
                        ipCounts.AddOrUpdate(ip, 1, (_, prev) => prev + 1);

                        // URL
                        if (url != null)
                            urlCounts.AddOrUpdate(url, 1, (_, prev) => prev + 1);

                        // User-Agent (в Combined и, возможно, custom)
                        if (m.Groups["useragent"].Success)
                        {
                            string ua = m.Groups["useragent"].Value;
                            if (!string.IsNullOrEmpty(ua))
                                uaCounts.AddOrUpdate(ua, 1, (_, prev) => prev + 1);
                        }

                        // Status‐код
                        string status = m.Groups["status"].Value;
                        statusCounts.AddOrUpdate(status, 1, (_, prev) => prev + 1);

                        // Считаем по «корзинам времени» (hour или day), если нужно
                        if (!string.IsNullOrEmpty(opt.Bucket))
                        {
                            string key;
                            if (opt.Bucket.Equals("hour", StringComparison.OrdinalIgnoreCase))
                                key = logDateUtc.ToString("yyyy-MM-dd HH:00");
                            else if (opt.Bucket.Equals("day", StringComparison.OrdinalIgnoreCase))
                                key = logDateUtc.ToString("yyyy-MM-dd");
                            else
                                key = null;

                            if (key != null)
                                bucketCounts.AddOrUpdate(key, 1, (_, prev) => prev + 1);
                        }
                    });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ошибка при чтении или обработке файла: {ex.Message}");
                return;
            }

            // Результаты обработки

            // 1) TOP IP
            if (opt.TopIp > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"=== TOP {opt.TopIp} IP (по количеству запросов) ===");
                foreach (var kv in ipCounts
                    .OrderByDescending(kv => kv.Value)
                    .Take(opt.TopIp))
                {
                    Console.WriteLine($"{kv.Key.PadRight(20)} : {kv.Value,8}");
                }
            }

            // 2) TOP URL
            if (opt.TopUrl > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"=== TOP {opt.TopUrl} URL (по количеству запросов) ===");
                foreach (var kv in urlCounts
                    .OrderByDescending(kv => kv.Value)
                    .Take(opt.TopUrl))
                {
                    Console.WriteLine($"{kv.Key.PadRight(50)} : {kv.Value,8}");
                }
            }

            // 3) TOP User-Agent
            if (opt.TopUa > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"=== TOP {opt.TopUa} User-Agent (по количеству запросов) ===");
                foreach (var kv in uaCounts
                    .OrderByDescending(kv => kv.Value)
                    .Take(opt.TopUa))
                {
                    Console.WriteLine($"{kv.Key.PadRight(60)} : {kv.Value,8}");
                }
            }

            // 4) Статистика по статус‐кодам
            if (opt.ShowCodes)
            {
                Console.WriteLine();
                Console.WriteLine("=== Распределение HTTP Status Codes ===");
                foreach (var kv in statusCounts.OrderBy(kv => kv.Key))
                {
                    Console.WriteLine($"  {kv.Key} : {kv.Value}");
                }
            }

            // 5) Статистика по «корзинам времени»
            if (!string.IsNullOrEmpty(opt.Bucket))
            {
                Console.WriteLine();
                Console.WriteLine($"=== Статистика по интервалам ({opt.Bucket}) ===");
                foreach (var kv in bucketCounts.OrderBy(kv => kv.Key))
                {
                    Console.WriteLine($"  {kv.Key} : {kv.Value}");
                }
            }

            // Если ни одна из команд не указана, выведем общее число обработанных строк
            if (opt.TopIp <= 0 && opt.TopUrl <= 0 && opt.TopUa <= 0 && !opt.ShowCodes && string.IsNullOrEmpty(opt.Bucket) && string.IsNullOrEmpty(opt.SearchTerm))
            {
                long total = ipCounts.Values.Sum();
                Console.WriteLine();
                Console.WriteLine($"Всего обработано запросов: {total}");
                Console.WriteLine("Укажите опции (--topip, --topurl, --codes и т.п.) для детальной статистики.");
            }
        }

        private static Options ParseArguments(string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelp();
                return null;
            }

            var opt = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "-h":
                    case "--help":
                        ShowHelp();
                        return null;

                    case "-f":
                    case "--format":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException("После --format должно идти значение (clf, combined или custom).");
                        opt.Format = args[++i];
                        break;

                    case "-l":
                    case "--log":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException("После --log должен идти путь к файлу логов.");
                        opt.LogFile = args[++i];
                        break;

                    case "--config":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException("После --config должен идти путь к JSON‐файлу для custom.");
                        opt.ConfigFile = args[++i];
                        break;

                    case "--topip":
                        if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out int ti))
                            throw new ArgumentException("После --topip должен идти положительный целый аргумент.");
                        opt.TopIp = ti;
                        i++;
                        break;

                    case "--topurl":
                        if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out int tu))
                            throw new ArgumentException("После --topurl должен идти положительный целый аргумент.");
                        opt.TopUrl = tu;
                        i++;
                        break;

                    case "--topua":
                        if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out int tua))
                            throw new ArgumentException("После --topua должен идти положительный целый аргумент.");
                        opt.TopUa = tua;
                        i++;
                        break;

                    case "--codes":
                        opt.ShowCodes = true;
                        break;

                    case "--search":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException("После --search должен идти поисковый термин.");
                        opt.SearchTerm = args[++i];
                        break;

                    case "--from":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException("После --from должна идти дата в формате yyyy-MM-dd.");
                        if (!DateTime.TryParseExact(args[i + 1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fromDt))
                            throw new ArgumentException("Дата после --from должна быть в формате yyyy-MM-dd.");
                        opt.FromDate = fromDt;
                        i++;
                        break;

                    case "--to":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException("После --to должна идти дата в формате yyyy-MM-dd.");
                        if (!DateTime.TryParseExact(args[i + 1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime toDt))
                            throw new ArgumentException("Дата после --to должна быть в формате yyyy-MM-dd.");
                        opt.ToDate = toDt;
                        i++;
                        break;

                    case "--bucket":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException("После --bucket должно идти значение 'hour' или 'day'.");
                        string b = args[++i].ToLowerInvariant();
                        if (b != "hour" && b != "day")
                            throw new ArgumentException("Значение --bucket может быть только 'hour' или 'day'.");
                        opt.Bucket = b;
                        break;

                    default:
                        throw new ArgumentException($"Неизвестная опция \"{a}\". Используйте --help для справки.");
                }
            }

            // Обязательные параметры: формат и лог
            if (string.IsNullOrEmpty(opt.Format))
                throw new ArgumentException("Не задан --format.");
            if (string.IsNullOrEmpty(opt.LogFile))
                throw new ArgumentException("Не задан --log (путь к файлу логов).");

            return opt;
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Использование:");
            Console.WriteLine("  dotnet run -- -f <формат> -l <файл_логов> [опции]\n");
            Console.WriteLine("Обязательные параметры:");
            Console.WriteLine("  -f, --format <формат>   Формат логов: clf, combined или custom");
            Console.WriteLine("  -l, --log <путь>       Путь к файлу логов для анализа\n");
            Console.WriteLine("Опции:");
            Console.WriteLine("  --config <путь>        (только для custom) JSON‐файл с двумя полями:");
            Console.WriteLine("                         \"regex\" (строка-шаблон) и \"dateFormat\" (формат даты)");
            Console.WriteLine("  --topip <N>            Вывести топ N IP по числу запросов");
            Console.WriteLine("  --topurl <N>           Вывести топ N URL по числу запросов");
            Console.WriteLine("  --topua <N>            Вывести топ N User-Agent по числу запросов");
            Console.WriteLine("  --codes                Показать распределение HTTP Status Codes");
            Console.WriteLine("  --search <термин>      Фильтровать только запросы, где URL содержит <термин>");
            Console.WriteLine("  --from <yyyy-MM-dd>    Начало диапазона дат (включительно)");
            Console.WriteLine("  --to <yyyy-MM-dd>      Окончание диапазона дат (включительно)");
            Console.WriteLine("  --bucket <hour|day>    Группировать статистику по «час» или «день\"");
            Console.WriteLine("  -h, --help             Показать справку\n");
            Console.WriteLine("Примеры:");
            Console.WriteLine("  dotnet run -- -f combined -l access.log --topip 10 --codes");
            Console.WriteLine("  dotnet run -- -f custom --config myformat.json -l access.log --search \"/api/\" --from 2025-01-01 --to 2025-01-31");
        }
    }
}
