# Publicizer
Publicizer is an MSBuild library for allowing direct access to non-public members in .NET assemblies.

## Installation
Use the Visual Studio package manager and reference [Krafs.Publicizer](https://www.nuget.org/packages/Krafs.Publicizer).

Or add via the dotnet CLI:
```bash
dotnet add package Krafs.Publicizer
```

## Usage
Define _Publicize_-items in your project file to instruct Publicizer what to make public.

### Publicize an entire assembly:
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne" />
</ItemGroup>
```
Doing this will publicize all the assembly's containing members.

### Publicize a specific member:
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne:MyNamespace.MyType._privateField" />
</ItemGroup>
```
Nested types are specified with '+':
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne:MyNamespace.MyParentType+MyNestedType._privateField" />
</ItemGroup>
```
### Exclude members:
You can use _DoNotPublicize_-items to exclude members from being made public. 
These items can be used in conjunction with _Publicize_-items to publicize an entire assembly except a few members.
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne" />
    <DoNotPublicize Include="AssemblyOne:MyNamespace.MyType._privateField" />
</ItemGroup>
```

### Multiple includes
As with most Items, you can define multiple at once with semi-colons:
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne;AssemblyTwo;AssemblyThree" />
</ItemGroup>
```

### Publicize All
You can use this shorthand property to publicize all assemblies referenced by the project:
```xml
<PropertyGroup>
    <PublicizeAll>true</PublicizeAll>
</PropertyGroup>
```

Save the project file and the changes should take effect shortly.

## How Publicizer works
Member access is enforced by both the runtime and the compiler. 

The runtime does this by performing an access check when you attempt to use a member. If you don't have access, it throws a [MemberAccessException](https://docs.microsoft.com/en-us/dotnet/api/system.memberaccessexception/), which terminates the application.
This should never happen, because the compiler will not allow us to compile if trying to access a private member.

Publicizer suppresses the runtime exceptions by compiling the assembly as [unsafe](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/unsafe-code/).
Among other things, this tells the runtime to not enforce access checks.

However, the compiler cannot be suppressed like that. Instead, we have to trick it that all the members actually **are** public.
Publicizer does this by copying the referenced assemblies into memory, rewriting the access modifiers to public, and feeding these assemblies to the compiler instead of the real ones.
The compiler then only detects public members, and will let us compile.

By default, Publicizer additionally creates the new assemblies as [reference assemblies](https://docs.microsoft.com/en-us/dotnet/standard/assembly/reference-assemblies/). 
This reduces build times and memory usage. However, if you use your IDE's decompilation feature to inspect code, you may want to turn that off, or you will just see empty methods.
Do that by specifying this property:
```xml
<PropertyGroup>
    <PublicizeAsReferenceAssemblies>false</PublicizeAsReferenceAssemblies>
</PropertyGroup>
```

## License
[MIT](https://choosealicense.com/licenses/mit/)