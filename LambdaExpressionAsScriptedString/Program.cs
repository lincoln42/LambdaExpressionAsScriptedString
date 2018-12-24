using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LambdaExpressionAsScriptedString
{
    class Program
    {
        static void Main(string[] args)
        {
            var str = @"{A1: true, A2: true, A3: [""a"", ""b""], A4: [4, 5] }";

            dynamic userSelections = JObject.Parse(str);

            var expressions = new List<string>()
            {
                @"HashSet<string> func(dynamic d) {
                    var ret = new HashSet<string>();
                    try {
                       if (d.A1 == true && d.A3[0] == ""a"") { ret.Add(""X1""); }
                       if (d.A2 != true && d.A4[1] == 5) { ret.Add(""X2,X4""); }
                       if (d.A2 == true && d.A3[1] == ""b"") { ret.Add(""X5,X6""); }
                       if (d.A3[1] == ""b"") { ret.Add(""X3""); }
                       if (d.A4[0] == 4) { ret.Add(""X9""); }
                    } catch (Exception ex) {}
                    return ret;
                 }
                 func(DynObj)"
            };

            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            IEnumerable<Script<HashSet<string>>> compiledExpressions = new Script<HashSet<string>>[] { };

            try {
                compiledExpressions = ComileExpressions<HashSet<string>>(expressions);
            } catch (CompilationErrorException ex)
            {
                Console.WriteLine(string.Join(Environment.NewLine, ex.Diagnostics));
            }

            stopWatch.Stop();
            Console.WriteLine("Compile Time (Milliseconds): " + stopWatch.ElapsedMilliseconds);
            stopWatch.Reset();

            stopWatch.Start();
            var ccNameBag = new ConcurrentBag<string>();

            compiledExpressions.AsParallel().ForAll(exp => {
                try
                {
                    var script = exp;
                    var ret = script.RunAsync(new Globals { DynObj = userSelections }).Result;
                    var res = ret.ReturnValue;
                    if (res.Any()) { Array.ForEach(res.ToArray(), cc => ccNameBag.Add(cc)); }
                } catch (CompilationErrorException ex)
                {
                    Console.WriteLine(string.Join(Environment.NewLine, ex.Diagnostics));
                }
                catch (AggregateException e)
                {
                    Console.WriteLine(e.ToString());
                }
            });

            stopWatch.Stop();
            Console.WriteLine("Evaluation Time (Milliseconds)" + stopWatch.ElapsedMilliseconds);
            stopWatch.Reset();

            Array.ForEach(ccNameBag.ToArray(), x => Console.WriteLine(x));
            Console.WriteLine("Done");

            Console.ReadLine();
        }

        public static IEnumerable<Script<T>> ComileExpressions<T>(IEnumerable<string> expressions)
        {
            if (expressions != null && expressions.Any())
            {
                var scripts = expressions.AsParallel().Select(exp => {
                    var script = CSharpScript.Create<T>(exp,
                        ScriptOptions.Default.AddReferences(new Assembly[] {
                                typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly,
                                typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly,
                                typeof(Newtonsoft.Json.JsonWriter).Assembly,
                                typeof(JValue).Assembly,
                                typeof(IEnumerable<>).Assembly,
                                typeof(Enumerable).Assembly,
                                typeof(Exception).Assembly
                        })
                        .AddImports(new string[] {
                                "Newtonsoft.Json",
                                "Newtonsoft.Json.Linq",
                                "System.Linq",
                                "System.Collections.Generic",
                                "System"
                        }),
                        globalsType: typeof(Globals)
                        );
                    return script;
                }).ToList();

                return scripts;
            }

            return new List<Script<T>>();

        }

    }
}