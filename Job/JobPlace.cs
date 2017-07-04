using System;
using System.Collections.Generic;
using System.Text;

namespace Job
{
    class JobPlace
    {
        public string PlaceName { get; set; }
        public string EmployerName { get; set; }
        public float Salary { get; set; }
        public int Key { get; set; }
        public DateTime Time { get; set; }

        public JobPlace(string place, string employer_name, float salary, DateTime time, int key)
        {
            PlaceName = place;
            EmployerName = employer_name;
            Salary = salary;
            Time = time;
            Key = key;
        }
    }
}
