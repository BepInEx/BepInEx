using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeveloperConsole
{
    static class SceneDumper
    {
        public static void DumpScene()
        {
            var objects = SceneManager.GetActiveScene().GetRootGameObjects();

            var fname = Path.GetTempFileName();
            using (var f = File.OpenWrite(fname))
            using (var sw = new StreamWriter(f, Encoding.UTF8))
            {
                foreach (var obj in objects)
                {
                    PrintRecursive(sw, obj);
                }
            }
            Process.Start("notepad.exe", fname);
        }

        private static void PrintRecursive(StreamWriter sw, GameObject obj)
        {
            PrintRecursive(sw, obj, 0);
        }

        private static void PrintRecursive(StreamWriter sw, GameObject obj, int d)
        {
            //Console.WriteLine(obj.name);
            var pad1 = new string(' ', 3 * d);
            var pad2 = new string(' ', 3 * (d + 1));
            var pad3 = new string(' ', 3 * (d + 2));
            sw.WriteLine(pad1 + obj.name + "--" + obj.GetType().FullName);

            foreach (Component c in obj.GetComponents<Component>())
            {
                sw.WriteLine(pad2 + "::" + c.GetType().Name);

                var ct = c.GetType();
                var props = ct.GetProperties(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var p in props)
                {
                    try
                    {
                        var v = p.GetValue(c, null);
                        sw.WriteLine(pad3 + "@" + p.Name + "<" + p.PropertyType.Name + "> = " + v);
                    }
                    catch (Exception e)
                    {
                        //Console.WriteLine(e.Message);
                    }
                }

            }
            foreach (Transform t in obj.transform)
            {
                PrintRecursive(sw, t.gameObject, d + 1);
            }
        }
    }
}
