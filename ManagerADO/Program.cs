using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerADO
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("***** LazyAdmin Manager Console UI *****\n");
            
            string cnStr = ConfigurationManager.ConnectionStrings["StoreSqlProvider"].ConnectionString;

        }
    }
}
