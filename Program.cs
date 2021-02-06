using System;
using System.Collections.Generic;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebScraper_test
{
    class Program
    {
        static ScrapingBrowser _scrapingBrowser = new ScrapingBrowser();
        
        static void Main(string[] args)
        {
            var nuorodos = new List<string>();
            Console.WriteLine("Gathering 'DELFI.LT' links, please wait...");
            nuorodos = GetMainPageLinks("https://www.delfi.lt");

            //Nuorodos po isfiltravimo
            Console.WriteLine("Filering gathered links...");
            nuorodos = LinkFilter(nuorodos);
            //Nuorodos su ju pavadinimais
            Console.WriteLine("Fetching link details...");
            var linkDetailsList = GetLinkDetails(nuorodos);
            //Dazniausiai pasikartojaciu zodziu isrinkimui
            var dictionary = new List<string>();
            //Isfiltruojami smulkus zodziai ir isimami skirybos zenklai
            WordFilterAndFix(dictionary, linkDetailsList);

            //Isrenkami dazniausiai pasikartojantys zodziai ir sugrupuojami
            //su atitinkamais linkais, suskaicuojamas linku kiekis
            var keyWords = new List<KeyWord>();
            var mostCommon = new List<string>();
            GetMostFrequent(dictionary, mostCommon);
            getFinalResult(linkDetailsList, mostCommon, keyWords);


            //Spausdinimas
            Console.WriteLine("Total links scraped: " + linkDetailsList.Count);
            Console.WriteLine("");

            for (int i = 0; i < keyWords.Count; i++)
            {
                Console.WriteLine("+--------------------------------------------------------------------------------------------------------------------------+");
                Console.WriteLine("| " + (i + 1));
                Console.WriteLine("| Keyword: " + keyWords[i].Word);
                Console.WriteLine("| Hits: " + keyWords[i].Hits);
                Console.WriteLine("+------------------------Related Links-------------------------------------------------------------------------------------+");
                for (int j = 0; j < keyWords[i].Urls.Count; j++)
                {
                    Console.WriteLine("| " + (j + 1) + ". "+ keyWords[i].Urls[j]);
                }
                Console.WriteLine("+--------------------------------------------------------------------------------------------------------------------------+");
                Console.WriteLine("");
            }

        }

        //Metodas uzpildo KeyWord objekta t.y. daugiausiai pasikartojanti zodi,
        //su juo susijusias nuorodas ir ju kieki
        static void getFinalResult(List<LinkDetails> linkDetails, List<String> commonWords, List<KeyWord> keyWords)
        {
            for (int i = 0; i < commonWords.Count; i++)
            {
                int hits = 0;
                var keyWord = new KeyWord();
                keyWord.Word = commonWords[i];

                for (int j = 0; j < linkDetails.Count; j++)
                {
                    foreach(var word in linkDetails[j].Words)
                    {
                        if(commonWords[i].Equals(word))
                        {
                            hits++;
                            keyWord.Urls.Add(linkDetails[j].Url);
                            break;
                        }
                        
                    }
                }

                keyWord.Hits = hits;
                keyWords.Add(keyWord);
            }
        }

        //Metodas isrenka daugiausiai straipsniu pavadinimuose pasitaikancius zodzius
        static void GetMostFrequent(List<string> values, List<string> keywords)
        {
            var result = new Dictionary<string, int>();

            foreach (string value in values)
            {
                if (result.TryGetValue(value, out int count))
                {
                    // Jei ne null, padidinam kieki 1
                    result[value] = count + 1;
                }
                else
                {
                    // Jei null, sukuriam value
                    result.Add(value, 1);
                }
            }

            // Isrikuojam mazejancia tvarka pagal pasikartojimu kieki
            var sorted = from pair in result
                         orderby pair.Value descending
                         select pair;

            for (int i = 0; i < 20; i++)
            {
                keywords.Add(sorted.ElementAt(i).Key); 
            }
        }


        //Metodas ismeta nereiksmingus zodzius,
        //taip pat istrina simbolius
        static void WordFilterAndFix(List<string> wordLib, List<LinkDetails> linkList)
        {
            for(int i = 0; i < linkList.Count; i++)
            {
                string title = linkList[i].Title;
                var words = new List<string>();
                words = title.Split(" ").ToList();

                List<char> charsToRemove = new List<char>() { '„','“',':', ',' ,'.',' ','?','–','—'};
                List<string> wordsToRemove = new List<string>() {"ir","apie","į","kad","su","iš","kaip","dėl","ar","tik",
                                                             "po","jau","per","net","tai","bet","iki","kas","už","o","ko",
                                                             "kai","ne","dar","be","bus","nei","ką","jo","jam","prieš",
                                                             "kuo","tarp","mes","nuo","bei","yra","ant","to","vos","jie"};
                String chars = "[" + String.Concat(charsToRemove) + "]";

                for(int z = words.Count - 1; z > -1; z--)
                {
                    words[z] = Regex.Replace(words[z], chars, String.Empty);

                    if (words[z]==string.Empty)
                    {
                        words.RemoveAt(z);
                    }
                    else
                    {
                        for(int j = wordsToRemove.Count - 1; j > -1; j--)
                        {
                            if (words[z].Equals(wordsToRemove[j]))
                            {
                                words.RemoveAt(z);
                            }                      
                        }
                    }
                }

                linkList[i].Words = words;

                wordLib.AddRange(words);
            }
        }

        //Metodas išfiltruoja nereikalingas nuorodas,
        //taip pat ištrina duplicate linkus, kurie veda į tą patį post'ą.
        static List<String> LinkFilter(List<string> links)
        {
            for(int i = links.Count - 1; i > -1; i--)
            {
                if(links[i].Contains("www.facebook.com") || links[i].Contains("com=1") || 
                   links[i].Contains("play.google.com")  || links[i].Contains("www.delfi.lt/ru") || 
                   links[i].Contains("www.delfi.lt/en"))
                {
                    links.RemoveAt(i);
                }                 
            }

            var distinct = links.Distinct().ToList();

            return distinct;
        }

        //Metodas gražina visas front page esančias nuorodas
        static List<string> GetMainPageLinks(string url)
        {
            var homePageLinks = new List<string>();
            var html = GetHtml(url);

            //linkai ieškomi "anchor" html tag'uose
            var links = html.CssSelect("a");

            foreach(var link in links)
            {
                //visuose href attributuose ieskom "?id=" stringo
                // ".html" netinka, nes Delfi.lt RESTful api naudoja    
                if(link.Attributes["href"].Value.Contains("?id="))
                {
                    homePageLinks.Add(link.Attributes["href"].Value);
                }
            }

            return homePageLinks;
        }

        //Metodas sukuria nuorodos objektą "LinkDetails", 
        //t.y kiekvienai nuorodai priskiria jos title.
        static List<LinkDetails> GetLinkDetails(List<string> urls)
        {
            var linkDetailList = new List<LinkDetails>();

            foreach(var url in urls)
            {
                var htmlNode = GetHtml(url);
                var linkDetails = new LinkDetails();

                //Nukerpamos "- DELFI..." galūnės lengvesniam sortinimui ir agregavimui
                var trim = htmlNode.OwnerDocument.DocumentNode.SelectSingleNode("//html/head/title").InnerText;
                var trimRes = trim.Substring(0, trim.LastIndexOf("-"));

                linkDetails.Title = trimRes;
                linkDetails.Url = url;
                linkDetailList.Add(linkDetails);
            }

            return linkDetailList;
        }

        static HtmlNode GetHtml(string url)
        {
            //Encodingas Lietuviškom raidėm konsolėj atvaizduoti
            var encoding = System.Text.Encoding.GetEncoding("utf-8");
            _scrapingBrowser.Encoding = encoding;

            //Page'o html getteris
            WebPage webpage = _scrapingBrowser.NavigateToPage(new Uri(url));
            return webpage.Html;
        }

        //Nuorodos klasė
        //Title - nuorodos pavadinimas
        //Url - nuoroda
        //Words - nuorodos pavadinimas suskaldytas i pavienius zodzius
        public class LinkDetails
        {
            public string Title { get; set; }
            public string Url { get; set; }
            public List<string> Words {get; set;}
        }

        //Galutiniu duomenu klase.
        //Word - dazniausiai pasikartojantis zodis
        //Hits - kiek su zodziu susijusiu nuorodu
        //Urls - su zodziu susijusiu nuorodu sarasas
        public class KeyWord
        {
            public string Word { get; set; }
            public int Hits { get; set; }
            public List<string> Urls {get; set;}

            public KeyWord()
            {
                this.Urls = new List<string>();
            }
        }
    }
}
