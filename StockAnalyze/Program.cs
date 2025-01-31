
using Azure;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using Microsoft.Playwright;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Newtonsoft.Json;
using OpenAI.Chat;
using System.Text.Json;
using System.Xml;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;

class Program
{

    public static string promt = "";
    public static string stock = "3029";
    public static double tempature = 0.7;
    public static string model = "gpt-4o";
    public static string apiKey = "";

    static async Task Main(string[] args)
    {
        try
        {
            var newsTask = GetNews(stock);
            var stockTask = GetStockPrice(stock);
            //var companyTask = GetCompanyContext(stock);
            //var anueTask = GetAnueNews(stock);
            await Task.WhenAll(newsTask, stockTask);
            string crawlData = await newsTask;
            string stockData = await stockTask;
            await SendToAI(apiKey, crawlData, stock, stockData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Eroor:{ex}");
        }
    }

    //將爬蟲內容傳給openAi
    static async Task SendToAI(string apiKey, string crawltext, string stock, string price)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
         model,
         apiKey
         );



        var kernel = builder.Build();

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            //MaxTokens = 100,   
            Temperature = tempature
        };


     


        var prompt1 = $@"
  {{$input}} 請扮演一位財務分析師，根據近期一段時間的新聞內容分析該公司的財務健康情況並提供建議，同時將新聞內容做成摘要，股票代號為{stock}。
  以下是新聞資料：{crawltext}。
  請忽略不相關資料，並用繁體中文回答。";

        var function1 = kernel.CreateFunctionFromPrompt(prompt1);

        // 第一階段：執行財務分析師角色
        var response1 = await kernel.InvokeAsync(function1, new(executionSettings));


        Console.WriteLine($"[ASSISTANT1]: {response1.ToString()}");


        var prompt2 = $@"
  {{$input}} 請扮演一位股票交易員，根據以下股票近一個月股價變化做出投資建議，並分析短、中、長期投資進出依據，股票代號為{stock}，近一個月股價變化如下：{price}。
  請用繁體中文回答。";

        var function2 = kernel.CreateFunctionFromPrompt(prompt2);

        // 第二階段：執行股票交易員角色，使用第一階段的結果
        var response2 = await kernel.InvokeAsync(function2, new(executionSettings));


        Console.WriteLine($"[ASSISTANT2]: {response2.ToString()}");

        var prompt3 = $@"
  {{$input}} 請扮演一位財金報告寫手，根據以下資訊撰寫分析報告：
  1. 財務分析：{response1.ToString()}
  2. 投資建議：{response2.ToString()}
  股票代號為{stock}，並將公司資訊稍微帶入產業內容即可，不需個人聯絡資訊。
  資料內容盡量不要刪減內容，只要彙整即可
  請用繁體中文回答。";

        var function3 = kernel.CreateFunctionFromPrompt(prompt3);


        // 第三階段：執行財金報告寫手角色，整合前兩階段的結果
        var response4 = await kernel.InvokeAsync(function3, new(executionSettings));

        Console.WriteLine($"[ASSISTANT3]: {response4.ToString()}");

        if (response4 != null)
        {
            await Creatpdf(response4.ToString());
        }
    }

    //指定路徑產pdf
    static async Task Creatpdf(string responseText)
    {
        string fontPath = @"C:\Windows\Fonts\msyh.ttc,0";
        var font = PdfFontFactory.CreateFont(fontPath, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
        //自行替換路徑
        string pdfPath = "";


        using (var writer = new PdfWriter(pdfPath))
        {
            using (var pdf = new PdfDocument(writer))
            {
                // 創建頁面
                var document = new iText.Layout.Document(pdf);

                // 將字串加入 PDF 文件
                document.Add(new Paragraph(responseText).SetFont(font).SetFontSize(12));
            }
        }
    }







    //從google rss抓新聞標題配上網址(抓YAHOO新聞)
    public static async Task<string> GetNews(string stock)
    {
        List<string> list = new List<string>();
        using (var client = new HttpClient())
        {
            // 發送 HTTP 請求取得 RSS 資料
            string url = $"https://news.google.com/news/rss/search/section/q/{stock}/?hl=zh-tw&gl=TW&ned=zh-tw_tw";

            string body = await client.GetStringAsync(url);

            // 解析 XML
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(body);

            // 取得 RSS 項目
            XmlNodeList items = xmlDoc.SelectNodes("//rss/channel/item");

            //// 把 XML 資料轉換為 C# 物件
            //var newsItems = new List<NewsItem>();
            foreach (XmlNode item in items)
            {
                string title = item.SelectSingleNode("title")?.InnerText;
                string link = item.SelectSingleNode("link")?.InnerText;
                string time = item.SelectSingleNode("pubDate")?.InnerText;
                if (DateTime.TryParse(time, out var parsedTime))
                {
                    DateTime now = DateTime.Now;
                    //加入過去一個月的新聞
                    if (title.Split('-')[^1].Trim() == "Yahoo奇摩股市" && parsedTime >= DateTime.Now.AddDays(-30))
                    {
                        Console.WriteLine($"加入文章連結: {title}");
                        list.Add(link);
                    }
                }
            }

        }
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            //自行替換路徑
            ExecutablePath = "",
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-gpu" }
        });
        List<string> crawl = new List<string>();
        foreach (var s in list)
        {

            // 開啟新頁面
            var newspage = await browser.NewPageAsync();

            await newspage.GotoAsync(s);
            string text = await newspage.Locator(".caas-body").InnerTextAsync();
            Console.WriteLine($"新聞內容：{text}");
            crawl.Add(text);

        }
        string Data = string.Join("\n", crawl);
        return Data;
    }

    //上市上櫃股價回傳
    public static async Task<string> GetStockPrice(string stock)
    {
        var price = "";
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            //自行替換路徑
            ExecutablePath = "",
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-gpu" }
        });


        //上市股價
        var apiRequestContext = await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            IgnoreHTTPSErrors = true // 忽略 SSL憑證
        });


        //本月股價變化(證交所)
        string targetUrl = $"https://www.twse.com.tw/rwd/zh/afterTrading/STOCK_DAY?date={DateTime.Now:yyyyMMdd}&stockNo={stock}&response=json";

        var response = await apiRequestContext.GetAsync(targetUrl);
        if (!response.Ok)
        {
            Console.WriteLine($"Failed to fetch data. Status: {response.Status}");
            return null;
        }
        var jsonResponse = await response.JsonAsync();
        var parsedResponse = JsonConvert.DeserializeObject<dynamic>(jsonResponse.ToString());
        Console.WriteLine(parsedResponse);
        //上市沒有就找上櫃
        if (parsedResponse.total == 0)
        {
            price = "";
            var months = new[] { DateTime.Now.Month.ToString(), DateTime.Now.AddMonths(-1).Month.ToString() };
            foreach (var month in months)
            {
                //上櫃股價
                var context = await browser.NewContextAsync();
                var page = await context.NewPageAsync();
                await page.GotoAsync("https://www.tpex.org.tw/zh-tw/mainboard/trading/info/stock-pricing.html");
                await page.FillAsync("#___auto1", stock);
                await page.SelectOptionAsync(".select-month.selectobj", new SelectOptionValue { Value = month });
                await page.ClickAsync("div.tables-tools button[type='submit']");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                var table = await page.QuerySelectorAsync("table.F1.L1.R2_");
                if (table == null)
                {
                    Console.WriteLine("上櫃無資料。");
                    return null;
                }
                var headers = await page.EvaluateAsync<string[]>(@"
            Array.from(document.querySelectorAll('table.F1.L1.R2_ thead th')).map(th => th.textContent.trim())");
                var rows = await page.EvaluateAsync<string[][]>(@"
            Array.from(document.querySelectorAll('table.F1.L1.R2_ tbody tr')).map(row =>
                Array.from(row.querySelectorAll('td')).map(td => td.textContent.trim())
            )");
                var tableData = new List<Dictionary<string, string>>();
                foreach (var row in rows)
                {
                    var rowData = new Dictionary<string, string>();
                    for (int i = 0; i < headers.Length; i++)
                    {
                        rowData[headers[i]] = row[i];
                    }
                    tableData.Add(rowData);
                }

                // 顯示結果
                foreach (var row in tableData)
                {
                    price += string.Join(", ", row);
                }
            }
        }
        else
        {
            price += (JsonConvert.DeserializeObject<dynamic>(jsonResponse.ToString())).ToString();
            Console.WriteLine(price);
            //上個月的股價變化(證交所)
            targetUrl = $"https://www.twse.com.tw/rwd/zh/afterTrading/STOCK_DAY?date={DateTime.Now.AddMonths(-1):yyyyMMdd}&stockNo={stock}&response=json";

            response = await apiRequestContext.GetAsync(targetUrl);
            if (!response.Ok)
            {
                Console.WriteLine($"Failed to fetch data. Status: {response.Status}");
                return null;
            }
            jsonResponse = await response.JsonAsync();
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            price += (JsonConvert.DeserializeObject<dynamic>(jsonResponse.ToString())).ToString();
        }
        Console.WriteLine(price);
        return price;
    }
}