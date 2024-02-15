# Publicizer <a href="https://www.nuget.org/packages/Krafs.Publicizer"><img alt="Version" src="https://img.shields.io/nuget/vpre/Krafs.Publicizer?label=Latest"></a>  <a href="https://www.nuget.org/packages/Krafs.Publicizer"><img alt="Downloads" src="https://img.shields.io/nuget/dt/Krafs.Publicizer?label=Downloads"></a>

Publicizer is an MSBuild plugin that allows direct access to private members in .NET-assemblies.

## Installation
Use your IDE's package manager and add [Krafs.Publicizer](https://www.nuget.org/packages/Krafs.Publicizer) from nuget.org.

Or add via the dotnet CLI:
```bash
dotnet add package Krafs.Publicizer
```

## Usage
Publicizer needs to be told what private members you want access to. You do this by defining _Publicize_-items in your project file.

```xml
<ItemGroup>
    <!-- All members in an assembly -->
    <Publicize Include="MyAssemblyFileName" />

    <!-- Field -->
    <Publicize Include="MyAssemblyFileName:MyNamespace.MyType.myField" />

    <!-- Property -->
    <Publicize Include="MyAssemblyFileName:MyNamespace.MyType.MyProperty" />

    <!-- Method -->
    <Publicize Include="MyAssemblyFileName:MyNamespace.MyType.MyMethod" />

    <!-- Field in nested type -->
    <Publicize Include="MyAssemblyFileName:MyNamespace.MyType+MyNestedType.myField" />

    <!-- Constructor -->
    <Publicize Include="MyAssemblyFileName:MyNamespace.MyType..ctor" />
</ItemGroup>
```

Notes:
- Assemblies are referenced by their file name, excluding file extension.
So, given an assembly called `MyAssemblyFileName.dll`, you reference it as `MyAssemblyFileName`.
- There is no way to publicize a specific method overload. Specifying a method will publicize all its overloads.

### Publicize assemblies from a PackageReference
PackageReferences, like other kinds of References, point towards one or more underlying assemblies. Publicizing these assemblies is just a matter of finding out the file names of the underlying assemblies, and then specify them as explained above.

### Publicize All
You can use this shorthand property to publicize **all** assemblies referenced by your project:
```xml
<PropertyGroup>
    <PublicizeAll>true</PublicizeAll>
</PropertyGroup>
```

Save the project file and the changes should take effect shortly. If not, try performing a _Restore_.

### Diagnostics
Publicizer logs to MSBuild. However, for convenience it is also possible to log to a custom log file by setting:
```xml
<PropertyGroup>
    <PublicizerLogFilePath>path/to/logfile</PublicizerLogFilePath>
</PropertyGroup>
```
If the file does not exist it will be created.

The file is overwritten on every execution.

### Clean
You can instruct Publicizer to clear its cache everytime the project is cleaned:
```xml
<PropertyGroup>
    <PublicizerClearCacheOnClean>true</PublicizerClearCacheOnClean>
</PropertyGroup>
```
This is mostly useful when troubleshooting Publicizer and you want logs to publicize on every rebuild instead of using the cached assemblies.

## How Publicizer works
There are two obstacles with accessing private members - the compiler and the runtime. 
The compiler won't compile code that attempts to access private members, and even if it would - the runtime would throw a [MemberAccessException](https://docs.microsoft.com/en-us/dotnet/api/system.memberaccessexception/) during execution.

Publicizer addresses the compiler issue by copying the assemblies, rewriting the access modifiers to public, and feeding those edited assemblies to the compiler instead of the real ones. This makes the compilation succeed.

The runtime issue is solved by instructing the runtime to not throw MemberAccessExceptions when accessing private members. 
This is done differently depending on the runtime. Publicizer implements two strategies: Unsafe and IgnoresAccessChecksTo.

Unsafe means that the assembly will be compiled with the [unsafe](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/unsafe-code/) flag.

IgnoresAccessChecksTo emits an [IgnoresAccessChecksToAttribute](https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/) to your source code, which then becomes part of your assembly.

Unsafe works for most versions of [Mono](https://www.mono-project.com/). IgnoresAccessChecksTo should work for most other runtimes, like CoreClr. That said - there could be exceptions.

These strategies can be toggled on or off by editing the PublicizerRuntimeStrategies-property in your project file.

Both strategies are enabled by default:
```xml
<PropertyGroup>
    <PublicizerRuntimeStrategies>Unsafe;IgnoresAccessChecksTo</PublicizerRuntimeStrategies>
</PropertyGroup>
```
However, if you e.g. know that your code runs fine with just the Unsafe strategy, you can avoid including the IgnoresAccessChecksToAttribute by telling Publicizer to only use Unsafe:
```xml
<PropertyGroup>
    <PublicizerRuntimeStrategies>Unsafe</PublicizerRuntimeStrategies>
</PropertyGroup>
```

## Quirks
Publicizer works by hacking the compiler and runtime, and there are a couple of quirks to be aware of.

### Overriding publicized members
Overriding a publicized member will throw an error at runtime. For example, say the following class exists in a referenced assembly ExampleAssembly:
```cs
namespace Example;
public abstract class Person
{
    protected abstract string Name { get; }
}
```
If you publicize this assembly, then Person.Name will be changed to public. If you then create a subclass Student, it might look like this:
```cs
public class Student : Person
{
    public override string Name => "Foobar";
}
```
This compiles just fine. However, during execution the runtime is presumably loading the original assembly where Person.Name is protected. 
So you have a Student class with a public Name-property overriding a protected Name-property on the Person class. 
This will cause an access check mismatch at runtime and throw an error.

You can avoid this by instructing Publicizer to not publicize Person.Name. You can use the _DoNotPublicize_-item for this:
```xml
<ItemGroup>
    <Publicize Include="ExampleAssembly" />
    <DoNotPublicize Include="ExampleAssembly:Example.Person.Name" />
</ItemGroup>
```

However, if there are a lot of protected members you have to override, doing this for all of them can be cumbersome.
For this scenario, you can instruct Publicizer to ignore all virtual members in the assembly:
```xml
<ItemGroup>
    <Publicize Include="ExampleAssembly" IncludeVirtualMembers="false" />
</ItemGroup>
```

### Compiler-generated member name conflicts
Sometimes assemblies contain members generated automatically by the compiler, like backing-fields for events. 
These generated members sometimes have names that conflict with other member names when they become public.

You can solve this in the same ways as above - either by using individual _DoNotPublicize_-items, or by telling Publicizer to ignore all compiler-generated members in the assembly:
```xml
<ItemGroup>
    <Publicize Include="ExampleAssembly" IncludeCompilerGeneratedMembers="false" />
</ItemGroup>
```

If you opt to ignore all virtual and/or compiler-generated members, you can still publicize specific ignored members by specifying them explicitly:
```xml
<ItemGroup>
    <Publicize Include="ExampleAssembly" IncludeCompilerGeneratedMembers="false" IncludeVirtualMembers="false" />
    <Publicize Include="ExampleAssembly:Example.Person.SpecificMember" />
</ItemGroup>
```

## Acknowledgements
This project builds upon rwmt's [Publicise](https://github.com/rwmt/Publicise), simplyWiri's [TaskPubliciser](https://github.com/simplyWiri/TaskPubliciser), and [this gist](https://gist.github.com/Zetrith/d86b1d84e993c8117983c09f1a5dcdcd) by Zetrith.

## License
[MIT](https://choosealicense.com/licenses/mit/)
