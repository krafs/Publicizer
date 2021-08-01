
# Publicizer

Publicizer is an MSBuild library for getting compile-time public access to any member in referenced assemblies.

## Installation
Use the Visual Studio package manager and reference [Krafs.Publicizer](https://www.nuget.org/packages/Krafs.Publicizer).

Or add via the dotnet CLI:
```bash
dotnet add package Krafs.Publicizer
```
Or add directly to your project file:
```xml
<ItemGroup>
    <PackageReference Include="Krafs.Publicizer" Version="1.0.0" />
</ItemGroup>
```
## Usage
Define _Publicize_ items in your project file with the names of the assemblies whose members you want public access to. 
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne;AssemblyTwo" />
</ItemGroup>
```
You can also use this shorthand property to publicize all assemblies referenced by the project:
```xml
<PropertyGroup>
    <PublicizeAll>true</PublicizeAll>
</PropertyGroup>
```
Save the project file and the changes should take effect shortly.

## Advanced usage
Publicizer supports targetting specific members for publicization, while leaving others untouched. This can be especially useful in cases where publicizing an entire assembly causes two normally private, separate members, to collide when publicized.

Members are targetted with the following pattern:
```xml
<Assembly>:<Namespace>.<Type>.<Member>
```
If targetting nested types:
```xml
<Assembly>:<Namespace>.<ParentType>+<NestedType>.<Member>
```

#### Example: Access specific private field.
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne:MyNamespace.MyType._privateField" />
</ItemGroup>
```
### Exclude
There is also support for excluding specific members from being publicized. This is done by defining a _DoNotPublicize_-item.
_DoNotPublicize_-items always override _Publicize_-items. This is useful if one wants an entire assembly publicized except a few members.

#### Example: Access everything in assembly expect specific private field.
```xml
<ItemGroup>
    <Publicize Include="AssemblyOne" />
    <DoNotPublicize Include="AssemblyOne:MyNamespace.MyType._privateField" />
</ItemGroup>
```

## License
[MIT](https://choosealicense.com/licenses/mit/)
