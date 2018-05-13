using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReaderTest
{
    using Model;
    using TestCode;
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                DataTest.Run();
                //SetterTest.Run();
            }
            catch(Exception e)
            {
                throw e;
            }
            finally
            {
                Console.ReadKey();
            }
        }
    }
}
