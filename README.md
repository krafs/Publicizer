# Publicizer
Publicizer is an MSBuild library for allowing direct access to non-public members in referenced assemblies.

## Installation
Use your IDE's package manager and reference [Krafs.Publicizer](https://www.nuget.org/packages/Krafs.Publicizer) from nuget.org.

Or add via the dotnet CLI:
```bash
dotnet add package Krafs.Publicizer
```
Or add directly to your project file:
```xml
<ItemGroup>
    <PackageReference Include="Krafs.Publicizer" Version="1.0.2" />
</ItemGroup>
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

Make sure you type the assembly name without the file extension, i.e. **without** '.dll'.

### Publicize a specific member:
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne:MyNamespace.MyType._privateField" />
</ItemGroup>
```

### Exclude members:
You can use _DoNotPublicize_-items to exclude members from being made public. 
These items can be used in conjunction with _Publicize_-items to publicize an entire assembly except a few members.
This can be useful when two members collide when publicized.
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne" />
    <DoNotPublicize Include="AssemblyOne:MyNamespace.MyType._privateField" />
</ItemGroup>
```

### Multiple includes
As with most Items, you can include multiple patterns with semi-colons:
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne;AssemblyTwo;AssemblyThree" />
</ItemGroup>
```
### Publicize assemblies from a PackageReference
PackageReferences, like other kinds of References, point towards one or more underlying assemblies. Publicizing these assemblies is just a matter of finding out what the underlying assemblies are called, and then specify them the same way as above.

Below is an example of publicizing two assemblies from the package [Krafs.Rimworld.Ref](https://www.nuget.org/packages/Krafs.Rimworld.Ref/):
```xml
<ItemGroup>
    <PackageReference Include="Krafs.Publicizer" Version="1.0.2" />
    <PackageReference Include="Krafs.Rimworld.Ref" Version="1.3.3200" />
</ItemGroup>

<ItemGroup>
    <Publicize Include="Assembly-CSharp;UnityEngine.CoreModule" />
</ItemGroup>
```

### Publicize All
You can use this shorthand property to publicize all assemblies referenced by the project:
```xml
<PropertyGroup>
    <PublicizeAll>true</PublicizeAll>
</PropertyGroup>
```

Save the project file and the changes should take effect shortly. If not, try performing a _Restore_.

## How Publicizer works
Publicizer works by copying the referenced assemblies into memory, rewriting the access modifiers to public, and feeding those assemblies to the compiler instead of the real ones.
Publicized assemblies are cached in _obj_ for future builds.

Additionally, the project's assembly is compiled as [unsafe](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/unsafe-code/). Among other things, this tells the runtime to not enforce access checks. Without this, accessing an unpermitted member at runtime throws a [MemberAccessException](https://docs.microsoft.com/en-us/dotnet/api/system.memberaccessexception/).

This means that you do **NOT** have to specify _AllowUnsafeBlocks=true_ in your project file. Publicizer does this for you under the hood.

By default, Publicizer additionally creates the new assemblies as [reference assemblies](https://docs.microsoft.com/en-us/dotnet/standard/assembly/reference-assemblies/). 
This reduces build times and memory usage. However, if you use your IDE's decompilation feature to inspect code, you may want to turn that off, or you will just see empty methods.
Do that by specifying this property:
```xml
<PropertyGroup>
    <PublicizeAsReferenceAssemblies>false</PublicizeAsReferenceAssemblies>
</PropertyGroup>
```
## Acknowledgements
This project builds upon rwmt's [Publicise](https://github.com/rwmt/Publicise), simplyWiri's [TaskPubliciser](https://github.com/simplyWiri/TaskPubliciser), and [this gist](https://gist.github.com/Zetrith/d86b1d84e993c8117983c09f1a5dcdcd) by Zetrith.


## License
[MIT](https://choosealicense.com/licenses/mit/)
