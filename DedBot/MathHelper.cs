using System;
using System.IO;
using System.Linq;

namespace DedBot
{
    class MathHelper
    {
        public static TimeSpan getAverage(int? amount)
        {
            var now = DateTime.Now.ToShortDateString();
            TimeSpan totalTime = new TimeSpan();
            var todayDeaths = File.ReadLines(@"deaths.txt").Where(l => l.Contains(now)).ToList();
            int amountInt = 0;
            if (!amount.HasValue)
            {
                amountInt = todayDeaths.Count;
            }
            else
            {
                if (amount > todayDeaths.Count)
                {
                    amount = todayDeaths.Count;
                }
                amountInt = amount.Value;
            }

            for (int i = 0; i < amountInt - 1; i++)
            {
                var time = DateTime.Parse(todayDeaths[todayDeaths.Count - 1 - i]) - DateTime.Parse(todayDeaths[todayDeaths.Count - 1 - i - 1]);
                totalTime += time;

            }

            var average = totalTime / (amountInt - 1);
            return average;
        }

    }
}
