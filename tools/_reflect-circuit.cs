using System;
using System.Linq;
using System.Reflection;
var t = Type.GetType("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, Microsoft.AspNetCore.Components.Server");
if (t == null) {
  foreach (var path in Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\.nuget\packages", "Microsoft.AspNetCore.Components.Server.dll", SearchOption.AllDirectories).OrderByDescending(f => f).Take(3)) {
    Console.WriteLine("asm " + path);
    var asm = Assembly.LoadFrom(path);
    t = asm.GetType("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler");
    if (t != null) {
      foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
        Console.WriteLine(m.ToString());
      break;
    }
  }
} else {
  foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
    Console.WriteLine(m.ToString());
}
