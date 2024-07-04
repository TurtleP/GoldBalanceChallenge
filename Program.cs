using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using static System.Net.Mime.MediaTypeNames;
using OpenQA.Selenium.Support.UI;
using System.Security.Claims;

class Program
{
    #region XPaths
    
    // The input fields for the left and right bowls.
    private static readonly string LeftBowlInput = "left_{0}";
    private static readonly string RightBowlInput = "right_{0}";

    // The input fields for the coins. Used to select the fake coin.
    private static readonly string CoinInput = "coin_{0}";

    private static readonly By ResetButton = By.XPath("//div//button[contains(text(), 'Reset')]");
    private static readonly By WeighButton = By.Id("weigh");

    // The result of the weighing. Sadly.. there was no easy way to fetch this. There's *two* divs with the same id.
    private static readonly By Result = By.XPath("//div[contains(text(), 'Result')]//../button[@id='reset']");

    // The weighings list. Could just use the tag name, but I'll play it safe.
    private static readonly By Weighings = By.XPath("//div[contains(text(), 'Weighings')]//..//li");

    #endregion

    #region Fields
    
    private static IWebDriver _driver;
    private static readonly string ChallengeUrl = "http://sdetchallenge.fetch.com/";

    private static readonly uint[] Coins = Enumerable.Range(0, 9).Select(x => (uint)x).ToArray();

    enum Bowl
    {
        BowlLeft,
        BowlRight
    };

    #endregion

    /// <summary>
    /// Returns a By object for the specified bowl and square.
    /// </summary>
    /// <param name="bowlId"></param>
    /// <param name="square"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private static By GetBowl(Bowl bowlId, uint square)
    {
        string bowlInput = bowlId switch
        {
            Bowl.BowlLeft => LeftBowlInput,
            Bowl.BowlRight => RightBowlInput,
            _ => throw new NotImplementedException()
        };

        return By.Id(string.Format(bowlInput, square));
    }

    /// <summary>
    /// Selects the coin with the specified id.
    /// </summary>
    /// <param name="coin"></param>
    private static void SelectCoin(uint coin)
    {
        try
        {
            var coinInput = string.Format(CoinInput, coin);
            _driver.FindElement(By.Id(coinInput)).Click();
        } 
        catch (NoSuchElementException e)
        {
            Console.WriteLine(e);
        }
    }

    /// <summary>
    /// Gets the elements for the specified bowl.
    /// </summary>
    /// <param name="bowlId"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    private static IWebElement?[] GetBowlElements(Bowl bowlId, int size)
    {
        var elements = new IWebElement?[size];

        for (int index = 0; index < size; index++)
        {
            try
            {
                var selector = GetBowl(bowlId, (uint)index);
                elements[index] = _driver.FindElement(selector);
            }
            catch (NoSuchElementException e)
            {
                Console.WriteLine(e);
                elements[index] = null;
            }
        }

        return elements;
    }

    /// <summary>
    /// Fill the specified bowl with the specified gold ids.
    /// </summary>
    /// <param name="bowlId"></param>
    /// <param name="goldIds"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static void FillBowl(Bowl bowlId, uint[] goldIds)
    { 
        var elements = GetBowlElements(bowlId, goldIds.Length);

        for (int index = 0; index < goldIds.Length; index++)
        {
            if (goldIds[index] >= Coins.Length)
                throw new ArgumentOutOfRangeException(nameof(goldIds), "Gold id must be in the range [0, 9).");

            var element = elements[index];
            element?.SendKeys(goldIds[index].ToString());
        }
    }

    /// <summary>
    /// Clears the bowls by clicking Reset.
    /// </summary>
    private static void ClearBowls()
    {
        try
        {
            _driver.FindElement(ResetButton).Click();
            new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until((d) =>
                d.FindElements(By.Id("left_")).All(e => e.Text == string.Empty) &&
                d.FindElements(By.Id("right_")).All(e => e.Text == string.Empty)
            );
        }
        catch (NoSuchElementException e)
        {
            Console.WriteLine(e);
        }
    }

    /// <summary>
    /// Weight the bowls and return the result.
    /// </summary>
    /// <returns></returns>
    private static string WeighBowls()
    {
        try
        {
            _driver.FindElement(WeighButton).Click();
            Thread.Sleep(TimeSpan.FromSeconds(5));

            return _driver.FindElement(Result).Text;
        }
        catch (NoSuchElementException e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks the result of the weighing and returns the fake coin.
    /// </summary>
    /// <param name="coins"></param>
    /// <returns></returns>
    private static uint CheckResult(uint[] coins)
    {
        if (coins.Length == 1)
            return coins[0];

        string result;
        ClearBowls();

        /* If there are only two coins, we can just weigh them and return the fake coin. */
        if (coins.Length == 3)
        {

            FillBowl(Bowl.BowlLeft, [coins[0]]);
            FillBowl(Bowl.BowlRight, [coins[1]]);

            result = WeighBowls();

            return result switch
            {
                "=" => coins[2],
                "<" => coins[0],
                _ => coins[1]
            };
        }

        /* Split the coins into three groups. */

        int groupSize = coins.Length / 3;

        uint[] groupOne = coins.Take(groupSize).ToArray();
        uint[] groupTwo = coins.Skip(groupSize).Take(groupSize).ToArray();
        uint[] groupThree = coins.Skip(groupSize * 2).ToArray();

        FillBowl(Bowl.BowlLeft, groupOne);
        FillBowl(Bowl.BowlRight, groupTwo);

        result = WeighBowls();

        return result switch
        {
            "=" => CheckResult(groupThree),
            "<" => CheckResult(groupOne),
            _ => CheckResult(groupTwo)
        };
    }

    public static void Main(string[] args)
    {
        try
        { 
            var service = ChromeDriverService.CreateDefaultService();
            service.EnableVerboseLogging = false;
            service.SuppressInitialDiagnosticInformation = true;

            var options = new ChromeOptions();
            options.AddArgument("--disable-logging");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--log-level=3");
            options.AddExcludedArguments("excludeSwitches", "enable-logging");

            _driver = new ChromeDriver(service, options);
            _driver.Navigate().GoToUrl(ChallengeUrl);
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

            var windowHandles = _driver.WindowHandles;

            uint fakeCoin = CheckResult(Coins);
            Console.WriteLine($"The fake coin is: {fakeCoin}");

            var element = _driver.FindElement(By.Id("coin_" + fakeCoin));
            element?.Click();

            var alert = _driver.SwitchTo().Alert();
            Console.WriteLine(alert.Text);
            alert.Accept();

            var weights = _driver.FindElements(Weighings);
            Console.WriteLine($"Took {weights.Count} weighings:");

            foreach (var weight in weights)
                Console.WriteLine($"  {weight.Text}");

            Console.ReadLine();
        }
        catch (NoSuchElementException e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            _driver.Quit();
            _driver.Dispose();
        }
    }
}
