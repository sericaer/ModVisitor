using ModVisitor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReflectionVisitor
{
    public class GMData
    {
        [ModVisit]
        public Taishou taishou;

        [ModVisit]
        public int year;

        public int score;

        public GMData()
        {
            taishou = new Taishou();
        }
    }

    public class Taishou
    {
        [ModVisit]
        public string name;

        [ModVisit]
        public int age;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Visitor.InitReflect("GMData", typeof(GMData));
            
            var script = "GMData.taishou.name";

            Setter set = new Setter(script);

            var gmdata = new GMData();
            Visitor.InitData("GMData", gmdata);

            set.set("test_name");

            Console.WriteLine(gmdata.taishou.name);

            Getter get = new Getter(script);

            Console.WriteLine(get.get());

        }
    }

    
}
