using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DownloadWikipediaTables
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var saveFilePath = $"{Directory.GetCurrentDirectory()}\\Files";
            _ = Directory.CreateDirectory(saveFilePath);

            while (true)
            {
                Console.WriteLine("Enter the URL of the wikipedia page:");
                var url = Console.ReadLine() ?? throw new Exception("No url set.");
                Console.WriteLine("Enter a file name:");
                var name = Console.ReadLine();
                var outPath = $"{saveFilePath}\\{name ?? "wikitable"}.csv";
                Console.WriteLine("Enter the title or index of the table you want:");
                var selector = Console.ReadLine() ?? "0";
                await GrabData(url, outPath, selector);

                Console.WriteLine("Another? y/n");
                var k = Console.ReadKey();
                Console.WriteLine();
                if (k.KeyChar == 'n')
                {
                    break;
                }
            }
        }

        static async Task GrabData(string url, string outPath, string selector)
        {
            var html = await DownloadHtmlAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tables = doc.DocumentNode
                .SelectNodes("//table[contains(@class,'wikitable')]")
                ?? new HtmlNodeCollection(null);

            if (tables.Count == 0)
            {
                Console.WriteLine("No tables with class 'wikitable' were found.");
                return;
            }

            HtmlNode table = SelectTable(tables, selector);

            var (headers, rows) = ParseTable(table);

            using (var writer = new StreamWriter(outPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                WriteCsvLine(writer, headers);
                foreach (var row in rows)
                    WriteCsvLine(writer, row);

                Console.WriteLine($"Saved {rows.Count} rows to {outPath}");
            }
        }

        static async Task<string> DownloadHtmlAsync(string url)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("WikiTableDownloader/1.0 (adminh@codespirals.dev)");
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                var resp = await http.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }

        static HtmlNode SelectTable(HtmlNodeCollection tables, string selector)
        {
            // If selector is an int, treat as index; else match caption text (contains)
            if (int.TryParse(selector, out int idx))
                return tables[Clamp(idx, 0, tables.Count - 1)];

            foreach (var t in tables)
            {
                var caption = t.SelectSingleNode(".//caption");
                if (caption != null && caption.InnerText.Trim().ToLowerInvariant().Contains(selector.ToLowerInvariant()))
                    return t;
            }
            // fallback to the largest table (by rows)
            return tables.OrderByDescending(t => t.SelectNodes(".//tr")?.Count ?? 0).First();
        }

        static (List<string> headers, List<List<string>> rows) ParseTable(HtmlNode table)
        {
            var headers = new List<string>();
            var headerNodes = table.SelectNodes(".//tr[1]/th") ?? table.SelectNodes(".//th");
            if (headerNodes != null && headerNodes.Count > 0)
            {
                foreach (var th in headerNodes)
                    headers.Add(Clean(th.InnerText));
            }
            else
            {
                // Some wikitables use td in first row instead of th
                var firstRowTds = table.SelectNodes(".//tr[1]/td");
                if (firstRowTds != null)
                    headers.AddRange(firstRowTds.Select(td => Clean(td.InnerText)));
            }

            var rows = new List<List<string>>();
            var trNodes = table.SelectNodes(".//tr") ?? new HtmlNodeCollection(null);

            // Skip header row(s)
            foreach (var tr in trNodes.Skip(1))
            {
                var cells = tr.SelectNodes("./th|./td");
                if (cells == null) continue;

                var row = cells.Select(td => Clean(td.InnerText)).ToList();
                // Pad or trim to header count to keep CSV rectangular
                if (headers.Count > 0)
                {
                    if (row.Count < headers.Count)
                        row.AddRange(Enumerable.Repeat(string.Empty, headers.Count - row.Count));
                    else if (row.Count > headers.Count)
                        row = row.Take(headers.Count).ToList();
                }
                rows.Add(row);
            }

            // If no headers detected, synthesize generic ones
            if (headers.Count == 0 && rows.Count > 0)
                headers = Enumerable.Range(1, rows.Max(r => r.Count)).Select(i => $"Col{i}").ToList();

            return (headers, rows);
        }

        static string Clean(string htmlText)
        {
            // HtmlAgilityPack keeps inner HTML; we want plain text with refs removed
            var text = HtmlEntity.DeEntitize(htmlText)
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ")
                .Trim();

            // Remove reference markers like [1], [note 2]
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[\s*(note)?\s*\d+\s*\]", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // Collapse whitespace
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s{2,}", " ");
            return text;
        }

        static void WriteCsvLine(StreamWriter sw, IList<string> fields)
        {
            sw.WriteLine(string.Join(",", fields.Select(EscapeCsv)));
        }

        static string EscapeCsv(string s)
        {
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }
        static int Clamp(int number, int min = 0, int max = int.MaxValue)
        {
            return number < 0 ? min : number > max ? max : number;
        }
    }
}