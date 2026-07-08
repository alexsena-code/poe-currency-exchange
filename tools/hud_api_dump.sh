#!/usr/bin/env bash
# ============================================================================
# hud_api_dump.sh — "changelog gerado por nós" da API do HUD.
#
# Despeja a superfície PÚBLICA de ExileCore.dll/GameOffsets.dll (tipos + membros,
# com marcação [Obsolete]) em docs/hud_api/*.api.txt, ordenado e determinístico.
# Commite o resultado; a cada update do HUD (hud_check_update.sh --apply) rode de
# novo e faça `git diff docs/hud_api` — as linhas +/- SÃO o changelog da API.
#
# Lê METADADOS (System.Reflection.Metadata) — não carrega os tipos, então não
# quebra com os tipos de memória do jogo. Precisa do dotnet SDK (net10).
#
# Uso:
#   ./hud_api_dump.sh                 # HUD ./ExileApi-Compiled-master -> docs/hud_api/
#   ./hud_api_dump.sh <hud_dir> <out_dir>
# ============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HUD_DIR="${1:-$SCRIPT_DIR/../../ExileApi-Compiled}"
OUT_DIR="${2:-$SCRIPT_DIR/../docs/hud_api}"
DLLS=("ExileCore.dll" "GameOffsets.dll")   # o que o plugin referencia (csproj HintPath)

[ -d "$HUD_DIR" ] || { echo "ERRO: HUD não encontrado em $HUD_DIR" >&2; exit 1; }
mkdir -p "$OUT_DIR"

TOOL="$(mktemp -d)"
trap 'rm -rf "$TOOL"' EXIT

cat > "$TOOL/dump.csproj" <<'CSPROJ'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <AssemblyName>hudapidump</AssemblyName>
  </PropertyGroup>
</Project>
CSPROJ

cat > "$TOOL/Program.cs" <<'CSHARP'
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

// Provider que decodifica assinaturas -> nomes legíveis (sem carregar tipos).
class Prov : ISignatureTypeProvider<string, object> {
  public string GetArrayType(string e, ArrayShape s)=>e+"[]";
  public string GetSZArrayType(string e)=>e+"[]";
  public string GetByReferenceType(string e)=>"ref "+e;
  public string GetPointerType(string e)=>e+"*";
  public string GetPinnedType(string e)=>e;
  public string GetGenericInstantiation(string g, ImmutableArray<string> a)=>g+"<"+string.Join(",",a)+">";
  public string GetGenericMethodParameter(object c,int i)=>"!!"+i;
  public string GetGenericTypeParameter(object c,int i)=>"!"+i;
  public string GetModifiedType(string m,string u,bool req)=>u;
  public string GetFunctionPointerType(MethodSignature<string> s)=>"fnptr";
  public string GetPrimitiveType(PrimitiveTypeCode c)=>c switch {
    PrimitiveTypeCode.Boolean=>"bool", PrimitiveTypeCode.Int32=>"int", PrimitiveTypeCode.Int64=>"long",
    PrimitiveTypeCode.Single=>"float", PrimitiveTypeCode.Double=>"double", PrimitiveTypeCode.String=>"string",
    PrimitiveTypeCode.Object=>"object", PrimitiveTypeCode.Void=>"void", PrimitiveTypeCode.Byte=>"byte",
    PrimitiveTypeCode.Char=>"char", _=>c.ToString() };
  public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte b)=>r.GetString(r.GetTypeDefinition(h).Name);
  public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte b)=>r.GetString(r.GetTypeReference(h).Name);
  public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte b)=>r.GetTypeSpecification(h).DecodeSignature(this,c);
}

class P {
  static MetadataReader r;
  static Prov prov = new Prov();

  static string FullName(TypeDefinitionHandle h) {
    var td = r.GetTypeDefinition(h);
    var name = r.GetString(td.Name);
    var decl = td.GetDeclaringType();
    if (!decl.IsNil) return FullName(decl) + "+" + name;
    var ns = r.GetString(td.Namespace);
    return ns.Length > 0 ? ns + "." + name : name;
  }

  static bool TypePublic(TypeDefinition td) {
    var v = td.Attributes & TypeAttributes.VisibilityMask;
    return v==TypeAttributes.Public || v==TypeAttributes.NestedPublic || v==TypeAttributes.NestedFamily || v==TypeAttributes.NestedFamORAssem;
  }
  static bool MethodApi(MethodAttributes a){ var m=a&MethodAttributes.MemberAccessMask; return m==MethodAttributes.Public||m==MethodAttributes.Family||m==MethodAttributes.FamORAssem; }
  static bool FieldApi(FieldAttributes a){ var m=a&FieldAttributes.FieldAccessMask; return m==FieldAttributes.Public||m==FieldAttributes.Family||m==FieldAttributes.FamORAssem; }

  static string AttrName(CustomAttributeHandle h) {
    var ca = r.GetCustomAttribute(h);
    try {
      if (ca.Constructor.Kind == HandleKind.MemberReference) {
        var mr = r.GetMemberReference((MemberReferenceHandle)ca.Constructor);
        if (mr.Parent.Kind == HandleKind.TypeReference) return r.GetString(r.GetTypeReference((TypeReferenceHandle)mr.Parent).Name);
        if (mr.Parent.Kind == HandleKind.TypeDefinition) return r.GetString(r.GetTypeDefinition((TypeDefinitionHandle)mr.Parent).Name);
      } else if (ca.Constructor.Kind == HandleKind.MethodDefinition) {
        var md = r.GetMethodDefinition((MethodDefinitionHandle)ca.Constructor);
        return r.GetString(r.GetTypeDefinition(md.GetDeclaringType()).Name);
      }
    } catch {}
    return "";
  }
  // "[Obsolete: msg] " se o membro tiver ObsoleteAttribute; senão "".
  static string ObsTag(CustomAttributeHandleCollection attrs) {
    foreach (var h in attrs) {
      if (AttrName(h) != "ObsoleteAttribute") continue;
      string msg = "";
      try { var br = r.GetBlobReader(r.GetCustomAttribute(h).Value); if (br.ReadUInt16()==1) msg = br.ReadSerializedString() ?? ""; } catch {}
      return msg.Length>0 ? "[Obsolete: "+msg+"] " : "[Obsolete] ";
    }
    return "";
  }

  static bool Junk(string n) => n.Length==0 || n[0]=='<' || n.Contains("k__BackingField") || n.Contains("<>");

  static void DumpDll(string dllPath, string outPath) {
    using var fs = File.OpenRead(dllPath);
    using var pe = new PEReader(fs);
    r = pe.GetMetadataReader();
    var types = new List<(string full, List<string> members)>();
    int obs = 0;

    foreach (var th in r.TypeDefinitions) {
      var td = r.GetTypeDefinition(th);
      if (!TypePublic(td)) continue;
      var full = FullName(th);
      if (Junk(r.GetString(td.Name))) continue;
      var mem = new List<string>();

      foreach (var ph in td.GetProperties()) {
        var pd = r.GetPropertyDefinition(ph);
        var nm = r.GetString(pd.Name); if (Junk(nm)) continue;
        var acc = pd.GetAccessors();
        var getter = !acc.Getter.IsNil ? r.GetMethodDefinition(acc.Getter) : (MethodDefinition?)null;
        var setter = !acc.Setter.IsNil ? r.GetMethodDefinition(acc.Setter) : (MethodDefinition?)null;
        bool api = (getter!=null && MethodApi(getter.Value.Attributes)) || (setter!=null && MethodApi(setter.Value.Attributes));
        if (!api) continue;
        string ty; try { ty = pd.DecodeSignature(prov, null).ReturnType; } catch { ty="?"; }
        var tag = ObsTag(pd.GetCustomAttributes()); if (tag.Length>0) obs++;
        mem.Add($"  {tag}prop {ty} {nm}");
      }
      foreach (var fh in td.GetFields()) {
        var fd = r.GetFieldDefinition(fh);
        if (!FieldApi(fd.Attributes)) continue;
        var nm = r.GetString(fd.Name); if (Junk(nm)) continue;
        string ty; try { ty = fd.DecodeSignature(prov, null); } catch { ty="?"; }
        var tag = ObsTag(fd.GetCustomAttributes()); if (tag.Length>0) obs++;
        mem.Add($"  {tag}field {ty} {nm}");
      }
      foreach (var mh in td.GetMethods()) {
        var md = r.GetMethodDefinition(mh);
        if (!MethodApi(md.Attributes)) continue;
        var nm = r.GetString(md.Name); if (Junk(nm)) continue;
        if (nm.StartsWith("get_")||nm.StartsWith("set_")||nm.StartsWith("add_")||nm.StartsWith("remove_")) continue;
        string sig; try { var s = md.DecodeSignature(prov, null); sig = $"{s.ReturnType} {nm}({string.Join(", ", s.ParameterTypes)})"; } catch { sig = $"? {nm}(?)"; }
        var tag = ObsTag(md.GetCustomAttributes()); if (tag.Length>0) obs++;
        mem.Add($"  {tag}method {sig}");
      }
      if (mem.Count==0) continue;
      mem.Sort(StringComparer.Ordinal);
      types.Add((full, mem));
    }

    types.Sort((a,b)=>string.CompareOrdinal(a.full,b.full));
    var sb = new StringBuilder();
    sb.AppendLine("# API pública de " + Path.GetFileName(dllPath) + " (gerado por hud_api_dump.sh)");
    sb.AppendLine("# tipos: " + types.Count + " | membros [Obsolete]: " + obs);
    sb.AppendLine();
    foreach (var t in types) { sb.AppendLine("=== " + t.full + " ==="); foreach (var m in t.members) sb.AppendLine(m); sb.AppendLine(); }
    File.WriteAllText(outPath, sb.ToString().Replace("\r\n","\n"));
    Console.WriteLine($"  {Path.GetFileName(dllPath)}: {types.Count} tipos, {obs} obsoletos -> {outPath}");
  }

  static void Main(string[] a) {
    string hud=a[0], outDir=a[1];
    for (int i=2;i<a.Length;i++) {
      var dll = Path.Combine(hud, a[i]);
      if (!File.Exists(dll)) { Console.WriteLine("  (pulei, não existe: "+a[i]+")"); continue; }
      DumpDll(dll, Path.Combine(outDir, Path.GetFileNameWithoutExtension(a[i]) + ".api.txt"));
    }
  }
}
CSHARP

echo "gerando dump da API do HUD ($HUD_DIR) -> $OUT_DIR ..."
( cd "$TOOL" && dotnet run -v q -- "$HUD_DIR" "$OUT_DIR" "${DLLS[@]}" )
echo "OK. Compare com o baseline commitado:  git diff docs/hud_api"
