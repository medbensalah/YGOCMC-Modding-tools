# CMC Studio

Native Windows modding tool for Yu-Gi-Oh! Capsule Monster Coliseum.

## Build and run

Requires Windows and the .NET 10 SDK.

```powershell
dotnet restore CMCStudio/CMCStudio.csproj
dotnet publish CMCStudio/CMCStudio.csproj -c Release -o CMCStudio/dist
.\"Launch CMC Studio.cmd"
```

Application and engine source: `CMCStudio/`. Command-line tools: `CMCStudio.Cli/`.
See their READMEs for details. Game ISOs, personal projects and generated builds are not included.

This revision replaces the previous tool with the current standalone CMC Studio source.
