#!/usr/bin/env -S dotnet --
// =========================================================================
// [Hun-RISCV] .NET file-based 오케스트레이터 (hun-build.cs)
//   RISC-V 베어메탈(QEMU virt) 용: 어셈블 -> 링크 -> QEMU 실행
//   사용법:  dotnet ./hun-build.cs [--no-run] [--gdb] [--clean]
// =========================================================================
#:property TargetFramework=net10.0

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

const int Xlen = 64;
const string ProjectName = "Bootcamp";

string march = Xlen == 64 ? "rv64gc" : "rv32imac_zicsr";
string mabi = Xlen == 64 ? "lp64d" : "ilp32";
string ldEmulation = Xlen == 64 ? "elf64lriscv" : "elf32lriscv";
string rustTarget = Xlen == 64 ? "riscv64gc-unknown-none-elf" : "riscv32imac-unknown-none-elf";
string qemuName = $"qemu-system-riscv{Xlen}";

bool noRun = args.Contains("--no-run");
bool gdb = args.Contains("--gdb");
bool clean = args.Contains("--clean");

string binDir = "bin";
string objDir = Path.Combine(binDir, "obj");
string elf = Path.Combine(binDir, ProjectName.ToLowerInvariant() + ".elf");

Console.WriteLine($"[Hun-RISCV] {ProjectName} — RV{Xlen} 베어메탈 오케스트레이터");

if (!Directory.Exists("src") || !File.Exists("link.ld"))
{
    Console.Error.WriteLine("프로젝트 루트(src/ 와 link.ld 가 있는 폴더)에서 실행해줘.");
    return 1;
}

if (clean)
{
    if (Directory.Exists(binDir)) Directory.Delete(binDir, true);
    Console.WriteLine("bin/ 삭제 완료");
    return 0;
}

// --- 0단계: Rust(no_std) 라이브러리 (있으면 자동으로 함께 빌드) ---
string? rustLib = null;
string rustManifest = Path.Combine("app", "RustLibs", "rust_core", "Cargo.toml");
if (File.Exists(rustManifest))
{
    Console.WriteLine("\n▶ 0단계: Rust(no_std) 빌드");
    if (Which("cargo") is null)
    {
        Console.Error.WriteLine("cargo 를 찾지 못했어. https://rustup.rs 로 Rust 를 먼저 설치해줘.");
        return 1;
    }
    int rc = Exec("cargo", ["build", "--release", "--target", rustTarget, "--manifest-path", rustManifest]);
    if (rc != 0)
    {
        Console.Error.WriteLine($"cargo 빌드 실패. 타깃이 없다면:  rustup target add {rustTarget}");
        return rc;
    }
    rustLib = Path.Combine("app", "RustLibs", "rust_core", "target", rustTarget, "release", "librust_core.a");
    if (!File.Exists(rustLib))
    {
        Console.Error.WriteLine($"{rustLib} 를 찾지 못했어. cargo 로그를 확인해줘.");
        return 1;
    }
}

// --- 1단계: 툴체인 탐지 ---
var tc = DetectToolchain();
if (tc is null)
{
    PrintToolchainHelp();
    return 1;
}
Console.WriteLine($"\n▶ 툴체인: {tc.Name}");

// --- 2단계: 어셈블리 파일 수집 ---
var asmFiles = Directory.EnumerateFiles("src", "*", SearchOption.AllDirectories)
    .Where(f => f.EndsWith(".S", StringComparison.Ordinal) || f.EndsWith(".s", StringComparison.Ordinal))
    .Select(f => f.Replace('\\', '/'))
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();

if (asmFiles.Count == 0)
{
    Console.Error.WriteLine("src/ 아래에 어셈블리 파일(.S/.s)이 한 개도 없네!");
    return 1;
}

Directory.CreateDirectory(binDir);
if (Directory.Exists(objDir)) Directory.Delete(objDir, true);
Directory.CreateDirectory(objDir);

// --- 3단계: 어셈블 ---
Console.WriteLine("\n▶ 1단계: 어셈블");
var sw = Stopwatch.StartNew();
var objFiles = new List<string>();
foreach (var src in asmFiles)
{
    string obj = Path.Combine(objDir, src.Replace('/', '_') + ".o");
    var asmArgs = new List<string>(tc.AsmArgs);
    if (tc.UsesDriver)
    {
        // .S 는 전처리기(cpp)를 통과시키고, .s 는 그대로 어셈블한다
        asmArgs.Add("-x");
        asmArgs.Add(src.EndsWith(".S", StringComparison.Ordinal) ? "assembler-with-cpp" : "assembler");
    }
    asmArgs.AddRange(["-I", ".", "-o", obj, src]);
    if (Exec(tc.AsmExe, asmArgs) != 0) return 1;
    objFiles.Add(obj);
}

// --- 4단계: 링크 ---
Console.WriteLine("\n▶ 2단계: 링크 (link.ld)");
var ldArgs = new List<string>(tc.LdArgs) { "-T", "link.ld", "-nostdlib", "-o", elf };
ldArgs.AddRange(objFiles);
if (rustLib is not null) ldArgs.Add(rustLib);
if (Exec(tc.LdExe, ldArgs) != 0) return 1;
sw.Stop();

try { Directory.Delete(objDir, true); } catch { }
Console.WriteLine($"✔ 빌드 성공 ({sw.ElapsedMilliseconds}ms) -> ./{elf}");

if (noRun) return 0;

// --- 5단계: QEMU 실행 ---
string? qemu = Which(qemuName);
if (qemu is null)
{
    Console.Error.WriteLine($"{qemuName} 를 찾지 못했어.");
    Console.Error.WriteLine(OperatingSystem.IsMacOS()
        ? "  brew install qemu"
        : "  sudo apt install qemu-system-misc");
    return 1;
}

var qemuArgs = new List<string> { "-machine", "virt", "-m", "128M", "-smp", "1", "-nographic", "-bios", "none" };
if (gdb)
{
    qemuArgs.AddRange(["-S", "-s"]);
    Console.WriteLine("\n[gdb 모드] QEMU 가 첫 명령 앞에서 멈춰서 기다려. 다른 터미널에서:");
    Console.WriteLine($"  gdb-multiarch {elf}      (macOS: riscv64-elf-gdb)");
    Console.WriteLine($"  (gdb) set architecture riscv:rv{Xlen}");
    Console.WriteLine("  (gdb) target remote :1234");
}
qemuArgs.AddRange(["-kernel", elf]);

Console.WriteLine("\n▶ QEMU 실행 (강제 종료: Ctrl-A 누른 뒤 X)\n------------------------------------------------");
int exit = Exec(qemu, qemuArgs);
Console.WriteLine("------------------------------------------------\n종료 코드: " + exit);
return exit;


// =========================================================================
// 함수들
// =========================================================================
string? Which(string name)
{
    var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Concat(["/opt/homebrew/bin", "/opt/homebrew/opt/llvm/bin", "/opt/homebrew/opt/lld/bin",
                 "/usr/local/bin", "/usr/local/opt/llvm/bin", "/usr/local/opt/lld/bin"]);
    foreach (var dir in dirs)
    {
        var candidate = Path.Combine(dir, name);
        if (File.Exists(candidate)) return candidate;
    }
    return null;
}

string Capture(string exe, params string[] a)
{
    try
    {
        var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var x in a) psi.ArgumentList.Add(x);
        using var p = Process.Start(psi)!;
        string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return o;
    }
    catch { return ""; }
}

int Exec(string exe, IEnumerable<string> a)
{
    var list = a.ToList();
    Console.WriteLine($"  $ {Path.GetFileName(exe)} {string.Join(' ', list.Select(x => x.Contains(' ') ? $"\"{x}\"" : x))}");
    var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
    foreach (var x in list) psi.ArgumentList.Add(x);
    using var p = Process.Start(psi)!;
    p.WaitForExit();
    return p.ExitCode;
}

Toolchain? DetectToolchain()
{
    // 1순위: GNU 툴체인 (Ubuntu: riscv64-unknown-elf- / Homebrew: riscv64-elf- / Ubuntu: riscv64-linux-gnu-)
    foreach (var prefix in new[] { "riscv64-unknown-elf-", "riscv64-elf-", "riscv64-linux-gnu-" })
    {
        var ld = Which(prefix + "ld");
        if (ld is null) continue;

        var gcc = Which(prefix + "gcc");   // 있으면 gcc 드라이버로 .S 전처리까지 처리
        if (gcc is not null)
            return new Toolchain($"GNU {prefix}gcc", gcc, ["-march=" + march, "-mabi=" + mabi, "-g", "-c"], true, ld, ["-m", ldEmulation]);

        var asx = Which(prefix + "as");    // gcc 가 없으면 as 로 직접 (전처리 없음)
        if (asx is not null)
            return new Toolchain($"GNU {prefix}as (전처리 없음)", asx, ["-march=" + march, "-mabi=" + mabi, "-g"], false, ld, ["-m", ldEmulation]);
    }

    // 2순위: LLVM (clang 에 RISC-V 타깃이 있고 ld.lld 가 있을 때)
    var clang = Which("clang");
    var lld = Which("ld.lld");
    if (clang is not null && lld is not null && Capture(clang, "--print-targets").Contains($"riscv{Xlen}"))
        return new Toolchain("LLVM clang + ld.lld", clang, [$"--target=riscv{Xlen}-unknown-elf", "-march=" + march, "-mabi=" + mabi, "-g", "-c"], true, lld, ["-m", ldEmulation]);

    return null;
}

void PrintToolchainHelp()
{
    Console.Error.WriteLine("\nRISC-V 툴체인을 찾지 못했어. 아래 중 하나를 설치해줘.");
    if (OperatingSystem.IsMacOS())
    {
        Console.Error.WriteLine("  brew install riscv64-elf-binutils riscv64-elf-gcc qemu");
        Console.Error.WriteLine("  (또는 brew install llvm lld — 단 PATH 에 /opt/homebrew/opt/llvm/bin 필요)");
    }
    else
    {
        Console.Error.WriteLine("  sudo apt install gcc-riscv64-unknown-elf qemu-system-misc");
    }
    Console.Error.WriteLine("  (설치 상태 점검: riscvcli doctor)");
}

record Toolchain(string Name, string AsmExe, string[] AsmArgs, bool UsesDriver, string LdExe, string[] LdArgs);
