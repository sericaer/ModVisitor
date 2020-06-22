using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ModVisitor
{
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
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

            for (int i = 0; i < fieldDepths.Count() - 1; i++)
            {
                var currField = fieldDepths[i];
                currData = currField.GetValue(currData);
            }

            fieldDepths.Last().SetValue(currData, value);
        }
    }

    public class Getter : Visitor
    {
        public Getter(string script)
        {
            Parse(script, VisitType.READ);
        }

        public object get()
        {
            if(is_static)
            {
                return raw;
            }

            var currData = dictData[innerName];

            for (int i = 0; i < fieldDepths.Count() - 1; i++)
            {
                var currField = fieldDepths[i];
                currData = currField.GetValue(currData);
            }

            return fieldDepths.Last().GetValue(currData);
        }
    }

    public class Visitor
    {
        internal enum VisitType
        {
            READ,
            WRITE
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
            var fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields.Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ModVisit))))
            {
                rslt.Add(new ReflectionInfo(field, ParseReflectionInfo(field.FieldType)));

            }
            return rslt;
        }

        internal void Parse(string script, VisitType vistType)
        {
            raw = script;

            int start = 0;

            List<ReflectionInfo> rootReflection = null;
            ReflectionInfo currReflection = null;

            while (start < raw.Length)
            {
                var matched = Regex.Match(raw.Substring(start), @"^[A-Za-z_]+\.*");
                if (!matched.Success)
                {
                    throw new Exception();
                }

                var matchedValue = matched.Value.TrimEnd('.');

                try
                {
                    if (start == 0)
                    {
                        if (!dictReflect.ContainsKey(matchedValue))
                        {
                            if(vistType == VisitType.WRITE)
                            {
                                throw new Exception();
                            }

                            if (matched.Length != raw.Length)
                            {
                                throw new Exception();
                            }

                            return;
                        }

                        rootReflection = dictReflect[matchedValue];
                        innerName = matchedValue;
                    }
                    else
                    {

                        if (currReflection == null)
                        {
                            currReflection = rootReflection.Single(x => x.field.Name == matchedValue);
                        }
                        else
                        {
                            currReflection = currReflection.subs.Single(x => x.field.Name == matchedValue);
                        }

                        fieldDepths.Add(currReflection.field);
                    }

                    start += matched.Length;
                }
                catch (Exception e)
                {
                    throw new Exception($"Parse failed! '{matchedValue}' in '{script}' ", e);
                }

                
            }
        }

        internal bool is_static
        {
            get
            {
                return fieldDepths.Count() == 0;
            }
        }

        internal string raw;

        protected string innerName;
        protected List<FieldInfo> fieldDepths = new List<FieldInfo>();

        protected class ReflectionInfo
        {
            internal FieldInfo field;
            internal List<ReflectionInfo> subs = new List<ReflectionInfo>();

            public ReflectionInfo(FieldInfo field, List<ReflectionInfo> subs)
            {
                this.field = field;
                this.subs = subs;
            }
        }
    }
}
