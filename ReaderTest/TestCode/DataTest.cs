using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using DataReaderDeserializer;
using System.Diagnostics;
using System.Threading;
namespace ReaderTest.TestCode
{
    using Model;
   public class DataTest
    {
        private static string FieldString;
        private RunInfo TestMethod<T>(Func<IDataReader,IEnumerable<T>> readData)
        {
            if (string.IsNullOrEmpty(FieldString))
            {
                FieldString= string.Join(",", typeof(T).GetProperties().Select(obj => obj.Name).ToArray());
            }
            string connStr = ConfigurationManager.ConnectionStrings["Test"].ConnectionString;
            using(SqlConnection conn= new SqlConnection(connStr))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = conn;

                cmd.CommandText = string.Format("Select top 10000 {0}  from Person.Person", FieldString);
                IDataReader reader = cmd.ExecuteReader();
                Stopwatch st = new Stopwatch();
                st.Start();
                var list = readData(reader).ToList();
                st.Stop();
                return new RunInfo { RunTime = st.Elapsed.TotalMilliseconds, Count = list.Count };
            }

        }
        class RunInfo
        {
            public double RunTime { get; set; }

            public int Count { get; set; }
        }
        private static object fn;
        private IEnumerable<T> ReadData<T>(IDataReader reader)
        {
            if (fn == null)
            {
                Stopwatch st = new Stopwatch();
                st.Start();
                fn = GetDeserializer<T>.Deserialize(reader);
                st.Stop();
                Console.WriteLine(" 初始化反序列化函数 。。。，用时：{0}ms", st.Elapsed.TotalMilliseconds);
            }
            Func<IDataReader, T> projector = (Func<IDataReader, T>)fn;
             
            try
            {
                while (reader.Read())
                {
                    yield return projector(reader);
                }
            }
            finally
            {
                reader.Dispose();
            }
        }
        public static void Run()
        {
            DataTest test = new DataTest();
            new Func<IDataReader, IEnumerable<Person>>[] { test.ILReadData<Person>,test.ReadData<Person>  }
            .ToList().ForEach(fn =>
            {
                Console.WriteLine("\\\\\\\\\\\\\\\\\\\\\\");
                Enumerable.Range(0, 10).ToList().ForEach(obj =>
                {
                    var time = Enumerable.Range(0, 500).Select(tmp =>
                      {
                          return test.TestMethod(fn).RunTime;
                      }).Average();

                    Console.WriteLine("第{0}次耗时：{1} ms", obj, time);
                });
                Console.WriteLine("\\\\\\\\\\\\\\\\\\\\\\ \n\n");

            });

            /*
            new Func<IDataReader, IEnumerable<Person>>[] {
                test.ReadData<Person>
               // ,
               // test.NewReadData<Person>
            }.ToList().ForEach(deserializer =>
            {
                Enumerable.Range(0, 4).Select(tmp =>
                 {
                     return new Thread((param) =>
                     {
                         Func<IDataReader, IEnumerable<Person>> fn = (Func<IDataReader, IEnumerable<Person>>)param;
                         // Console.WriteLine("\\\\\\\\\\\\\\\\\\\\\\");
                         Enumerable.Range(0, 10).ToList().ForEach(obj =>
                         {
                             var result = test.TestMethod(fn);
                             Console.WriteLine("第{0}次耗时：{1} ms,共{2}条数据, 来自线程：{3} ", obj, result.RunTime, result.Count, Thread.CurrentThread.ManagedThreadId);
                         });
                         //  Console.WriteLine("\\\\\\\\\\\\\\\\\\\\\\");
                     });
                 }).ToList().ForEach(fn =>
                 {
                     fn.Start(deserializer); //fn.Join();
                 });
            });
            */
        }

        private IEnumerable<T> NewReadData<T>(IDataReader reader)
        {
            try
            {
                while (reader.Read())
                {
                    yield return GetDeserializer<T>.ReflectDeserialize(reader);
                }
            }
            finally
            {
                reader.Dispose();
            }
        }

        private static object newfn;
        private IEnumerable<T> ILReadData<T>(IDataReader reader)
        {
            if (newfn == null)
            {
                Stopwatch st = new Stopwatch();
                st.Start();
                newfn = GetDeserializer<T>.Deserialize(reader);
                st.Stop();
                Console.WriteLine(" 初始化反序列化函数(使用IL 代码) 。。。，用时：{0}ms", st.Elapsed.TotalMilliseconds);
            }
            Func<IDataReader, T> projector = (Func<IDataReader, T>)newfn;
            try
            {
                while (reader.Read())
                {
                    yield return projector(reader);
                }
            }
            finally
            {
                reader.Dispose();
            }
        }
    }
}
