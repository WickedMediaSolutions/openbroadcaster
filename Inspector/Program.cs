using System;
using System.Linq;
using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.Compression;

var assembly = typeof(WaveFormat).Assembly;
Console.WriteLine($"Inspecting types from {assembly.FullName}");
var types = assembly.GetTypes()
	.Where(t => t.Name.Contains("Mpeg", StringComparison.OrdinalIgnoreCase)
		|| t.Name.Contains("Mp3", StringComparison.OrdinalIgnoreCase))
	.OrderBy(t => t.FullName)
	.ToList();

foreach (var type in types)
{
	Console.WriteLine(type.FullName ?? type.Name);
	if (type.Name.Equals("Mp3WaveFormat", StringComparison.Ordinal))
	{
		Console.WriteLine("  Constructors:");
		foreach (var ctor in type.GetConstructors())
		{
			var parameters = ctor.GetParameters()
				.Select(p => $"{p.ParameterType.Name} {p.Name}");
			Console.WriteLine($"    ({string.Join(", ", parameters)})");
		}
	}
}

Console.WriteLine();
Console.WriteLine("AcmStream public methods:");
foreach (var method in typeof(AcmStream).GetMethods(BindingFlags.Instance | BindingFlags.Public)
	.Where(m => !m.IsSpecialName))
{
	var parameters = method.GetParameters()
		.Select(p => $"{p.ParameterType.Name} {p.Name}");
	Console.WriteLine($" - {method.ReturnType.Name} {method.Name}({string.Join(", ", parameters)})");
}

Console.WriteLine();
Console.WriteLine("AcmStream constructors:");
foreach (var ctor in typeof(AcmStream).GetConstructors())
{
	var parameters = ctor.GetParameters()
		.Select(p => $"{p.ParameterType.Name} {p.Name}");
	Console.WriteLine($" - ({string.Join(", ", parameters)})");
}

Console.WriteLine();
Console.WriteLine("AcmStream fields/properties:");
foreach (var prop in typeof(AcmStream).GetProperties(BindingFlags.Public | BindingFlags.Instance))
{
	Console.WriteLine($" - {prop.PropertyType.Name} {prop.Name}");
}
