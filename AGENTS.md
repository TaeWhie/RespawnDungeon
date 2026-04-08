# AGENTS.md

## Cursor Cloud specific instructions

### Project Overview

This is a monorepo with three products:
1. **Unity Game** (2D dungeon exploration RPG) — requires Unity Editor 6000.3.10f1, cannot run headless in Cloud VMs
2. **GuildDialogue Backend** — .NET 8 C# console app / ASP.NET Core Minimal API (`MiniProjects/GuildDialogue/`)
3. **Hub Web UI** — React 18 + Vite 5 SPA (`MiniProjects/Hub/`)

Cloud agents can build/run/test **GuildDialogue** and **Hub** but **not** the Unity game (no Unity Editor).

### Critical: Regenerating the .csproj

The `GuildDialogue.csproj` is gitignored. On fresh clones it must be regenerated before `dotnet build` or `dotnet run`. Create `MiniProjects/GuildDialogue/GuildDialogue.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>GuildDialogue</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Text.Encoding.CodePages" Version="8.0.0" />
  </ItemGroup>
  <ItemGroup>
    <Content Update="Config\**\*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

### Running Services

Per `MiniProjects/Hub/README.md`:

- **Terminal A — Hub API**: `cd MiniProjects/GuildDialogue && dotnet run -- --hub-api` (port 5050)
- **Terminal B — Vite dev**: `cd MiniProjects/Hub && npm run dev` (port 5173, proxies `/api` → 5050)

### Gotchas

- **Ollama required for LLM features**: Dialogue (menu 1/2) and character creation (menu 5) require a local Ollama server at `localhost:11434` with models `exaone3.5:7.8b` and `nomic-embed-text`. Without Ollama, party management (menu 3) and expedition simulation (menu 4) still work.
- **No lint or automated test suite**: The codebase has no ESLint config, no test frameworks, and no CI. Validation is done by `dotnet build` and `npm run build`.
- **Config files are runtime data**: `MiniProjects/GuildDialogue/Config/` contains JSON data files that are read/written at runtime. Changes here are game state, not code.
