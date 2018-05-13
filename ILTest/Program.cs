using System;
using System.Reflection;
using System.Reflection.Emit;
namespace ILTest
{
    using Code;
    class Program
    {
        public static void Main()
        {
            try
            {
                Test.Run();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            Console.ReadKey();

            
        }
    }
}
