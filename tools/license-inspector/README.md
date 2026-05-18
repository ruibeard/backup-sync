# License Inspector

Small local utility that asks the installed XD code to parse `xd.lic`.

Build:

```powershell
cd tools/license-inspector && dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -o ..\..
```

Usage:

| Flag | Description |
|------|-------------|
| *(none)* | Print all licence fields |
| `--json` | JSON output |
| `--remote-folder` | Machine-readable folder name only |
| `--license <path>` | Licence file (default `C:\XDSoftware\cfg\xd.lic`) |
| `--xd-dir <path>` | XD binaries (default `C:\XDSoftware\bin\xd`) |

How it works:

The `xd.lic` file is encrypted. Rather than reverse the format, the inspector loads the installed XD DLL (`XDPeople.NET.dll`) and calls its built-in decryptor via reflection:

```csharp
var asm = Assembly.LoadFrom(@"C:\XDSoftware\bin\xd\XDPeople.NET.dll");
var type = asm.GetType("XDPeople.Utils.XDLicence");
var method = type.GetMethod("LoadToPreview", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
var licenceData = method.Invoke(null, new object?[] { @"C:\XDSoftware\cfg\xd.lic" });
```

It then reflects over every public property on the returned `LicenceData` object and prints them. `XdLoadContext` resolves XD's transitive dependencies from `C:\XDSoftware\bin\xd\` while preferring the host's .NET runtime for framework assemblies.
