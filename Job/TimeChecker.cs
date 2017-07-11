using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Job
{
    static class TimeChecker
    {
        private static Dictionary<long, Employer> dictionary;
        private static Timer Timer;

        public static void SetEmployersList(Dictionary<long, Employer> list)
        {
            dictionary = list;
        }

        private static async Task Check(Object source, object e)
        {
            foreach (var item in dictionary.Values)
            {
                await item.Notify();
            }
        }

        public static void Start()
        {
            Timer = new Timer(async x => await Check(null, null), null, 0, 60000);
        }
    }
}
