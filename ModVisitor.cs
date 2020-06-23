using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ModVisitor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ModVisit : System.Attribute
    {
    }

    public class Setter : Visitor
    {
        public Setter(string script)
        {
            Parse(script, VisitType.WRITE);
        }

        public void set(object value)
        {
            var currData = dictData[innerName];

            for (int i = 0; i < refElements.Count() - 1; i++)
            {
                var currField = refElements[i];
                currData = currField.Get(currData);
            }

            refElements.Last().Set(currData, value);
        }
    }

    public class Getter : Visitor
    {
        public Getter(string script)
        {
            FuncSetStaticValue = (obj)=> staticValue = obj;

            Parse(script, VisitType.READ);
        }

        public object get()
        {
            if(staticValue != null)
            {
                return staticValue;
            }

            var currData = dictData[innerName];

            for (int i = 0; i < refElements.Count() - 1; i++)
            {
                var currField = refElements[i];
                currData = currField.Get(currData);
            }

            return refElements.Last().Get(currData);
        }

        public object staticValue;
    }

    public class Visitor
    {
        internal enum VisitType
        {
            READ = 0x01,
            WRITE = 0x10
        }

        protected static Dictionary<string, object> dictData = new Dictionary<string, object>();

        protected static Dictionary<string, List<ReflectionInfo>> dictReflect = new Dictionary<string, List<ReflectionInfo>>();

        public static void InitData(string key, object gmdata)
        {
            dictData.Add(key, gmdata);
        }

        public static void InitReflect(string key, Type type)
        {
            dictReflect.Add(key, ParseReflectionInfo(type));
        }

        public static void ClearData()
        {
            dictData.Clear();
        }

        static List<ReflectionInfo> ParseReflectionInfo(Type type)
        {
            var rslt = new List<ReflectionInfo>();

            var properies = type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
            var fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields.Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ModVisit))))
            {
                rslt.Add(new ReflectionInfo(new ReflectionInfo.Field(field), ParseReflectionInfo(field.FieldType)));
            }
            foreach (var property in properies.Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ModVisit))))
            {
                rslt.Add(new ReflectionInfo(new ReflectionInfo.Property(property), ParseReflectionInfo(property.PropertyType)));
            }

            return rslt;
        }

        internal void Parse(string script, VisitType vistType)
        {

            raw = script;

            if (TryParseDigitCalc(script, vistType))
            {
                return;
            }

            if (TryParseModExpr(script, vistType))
            {
                return;
            }

            throw new Exception($"parse string faild! script:{script}, visit type:{vistType}");
        }

        private bool TryParseModExpr(string script, VisitType vistType)
        {
            int start = 0;

            List<ReflectionInfo> rootReflection = null;
            ReflectionInfo currReflection = null;

            while (start < raw.Length)
            {
                var matched = Regex.Match(raw.Substring(start), @"^[A-Za-z_]+\.*");
                if (!matched.Success)
                {
                    return false;
                }

                var matchedValue = matched.Value.TrimEnd('.');

                if (start == 0)
                {
                    if (!dictReflect.ContainsKey(matchedValue))
                    {
                        if (vistType == VisitType.WRITE)
                        {
                            return false;
                        }

                        if (matched.Length != raw.Length)
                        {
                            return false;
                        }

                        FuncSetStaticValue(matchedValue);
                        return true;
                    }

                    rootReflection = dictReflect[matchedValue];
                    innerName = matchedValue;
                }
                else
                {

                    if (currReflection == null)
                    {
                        currReflection = rootReflection.Single(x => x.element.Name == matchedValue);
                    }
                    else
                    {
                        currReflection = currReflection.subs.Single(x => x.element.Name == matchedValue);
                    }

                    if ((vistType == VisitType.READ && !currReflection.element.canRead)
                        || (vistType == VisitType.WRITE && !currReflection.element.canWrite))
                    {
                        return false;
                    }

                    refElements.Add(currReflection.element);
                }

                start += matched.Length;
            }

            return true;
        }

        private bool TryParseDigitCalc(string script, VisitType vistType)
        {
            if(vistType == VisitType.WRITE)
            {
                return false;
            }

            var convert = script.Replace(" ", "");

            var rslt = Regex.Match(convert, @"^[\+\-]?[0-9]+\.?[0-9]+(*[\+\-\*/]?[0-9]+\.?[0-9]+)*");
            if (!rslt.Success)
            {
                return false;
            }
            if (rslt.Length != convert.Length)
            {
                return false;
            }

            double value = 0.0;

            int start = 0;
            while (start < raw.Length)
            {
                if (start == 0)
                {
                    var matched_head = Regex.Match(raw.Substring(start), @"^[\+\-]?[0-9]+\.?[0-9]+");
                    if (!matched_head.Success)
                    {
                        return false;
                    }

                    value = double.Parse(matched_head.Value);
                    start += matched_head.Length;
                    continue;
                }

                var matched = Regex.Match(raw.Substring(start), @"^[\+\-\*/][0-9]+\.?[0-9]+");
                if (!matched.Success)
                {
                    return false;
                }

                if (matched.Value.StartsWith("+"))
                {
                    value += double.Parse(matched.Value.Replace("+", ""));
                }
                if (matched.Value.StartsWith("-"))
                {
                    value -= double.Parse(matched.Value.Replace("-", ""));
                }
                if (matched.Value.StartsWith("*"))
                {
                    value *= double.Parse(matched.Value.Replace("*", ""));
                }
                if (matched.Value.StartsWith("/"))
                {
                    value /= double.Parse(matched.Value.Replace("/", ""));
                }
            }

            FuncSetStaticValue(value);
            return true;
        }

        internal string raw;

        protected string innerName;
        protected List<ReflectionInfo.Element> refElements = new List<ReflectionInfo.Element>();
        protected Action<object> FuncSetStaticValue;

        protected class ReflectionInfo
        {
            public abstract class Element
            {
                public abstract string Name{ get;}
                public abstract bool canRead { get; }
                public abstract bool canWrite { get; }
                public abstract object Get(object obj);
                public abstract void Set(object obj, object value);
            }

            public class Field : Element
            {
                internal FieldInfo field;

                public Field(FieldInfo field)
                {
                    this.field = field;
                }

                public override string Name => field.Name;

                public override bool canRead => true;

                public override bool canWrite => true;

                public override object Get(object obj)
                {
                    return field.GetValue(obj);
                }

                public override void Set(object obj, object value)
                {
                    field.SetValue(obj, value);
                }
            }

            public class Property : Element
            {
                internal PropertyInfo property;

                public Property(PropertyInfo property)
                {
                    this.property = property;
                }

                public override string Name => property.Name;

                public override bool canRead => property.CanRead;

                public override bool canWrite => property.CanWrite;

                public override object Get(object obj)
                {
                    return property.GetValue(obj);
                }

                public override void Set(object obj, object value)
                {
                    property.SetValue(obj, value);
                }
            }

            internal Element element;
            internal List<ReflectionInfo> subs = new List<ReflectionInfo>();

            internal ReflectionInfo(Element element, List<ReflectionInfo> subs)
            {
                this.element = element;
                this.subs = subs;
            }
        }
    }
}
