const std = @import("std");

// Bootcamp — Zig 오케스트레이터 (RISC-V 베어메탈, QEMU virt)
// Zig 가 내장 clang/lld 로 어셈블 + 링크하므로 riscv 툴체인을 따로 설치하지 않아도 된다.
// QEMU(qemu-system-riscv64) 만 있으면 된다.
// (riscvcli init 으로 생성됨 — zig 0.16 기준, 다른 버전에서는 API가 다를 수 있으니 확인해줘)

// 어셈블리 소스 목록 — 새 .S 파일을 추가하면 여기에도 한 줄 추가해줘.
const asm_sources = [_][]const u8{
    "src/Boot.S",
    "src/Main.S",
    "src/libs/platform.S",
};

pub fn build(b: *std.Build) void {
    const rv = std.Target.riscv;
    const target = b.resolveTargetQuery(.{
        .cpu_arch = .riscv64,
        .os_tag = .freestanding,
        .cpu_features_add = rv.featureSet(&.{ .m, .a, .f, .d, .c, .zicsr }),
    });
    const optimize = b.standardOptimizeOption(.{});

    const exe = b.addExecutable(.{
        .name = "bootcamp.elf",
        .root_module = b.createModule(.{
            .target = target,
            .optimize = optimize,
            // 0x80000000 주소 대역은 medlow 로 표현할 수 없어서 medany(=medium) 필수
            .code_model = .medium,
            .single_threaded = true,
        }),
    });

    for (asm_sources) |src| {
        exe.root_module.addCSourceFile(.{ .file = b.path(src), .flags = &.{} });
    }
    exe.root_module.addIncludePath(b.path("."));
    exe.setLinkerScript(b.path("link.ld"));

    b.installArtifact(exe);

    // --- zig build run : QEMU 로 실행 (종료: 프로그램이 스스로 끝남, 강제 종료는 Ctrl-A X) ---
    const qemu_base = [_][]const u8{
        "qemu-system-riscv64", "-machine", "virt", "-m", "128M", "-smp", "1", "-nographic", "-bios", "none",
    };

    const run_cmd = b.addSystemCommand(&qemu_base);
    run_cmd.addArg("-kernel");
    run_cmd.addArtifactArg(exe);
    run_cmd.step.dependOn(b.getInstallStep());
    const run_step = b.step("run", "Bootcamp 를 QEMU 에서 실행");
    run_step.dependOn(&run_cmd.step);

    // --- zig build debug : QEMU 를 멈춘 채로 시작하고 gdb 를 기다린다 (tcp::1234) ---
    const debug_cmd = b.addSystemCommand(&qemu_base);
    debug_cmd.addArgs(&.{ "-S", "-s", "-kernel" });
    debug_cmd.addArtifactArg(exe);
    debug_cmd.step.dependOn(b.getInstallStep());
    const debug_step = b.step("debug", "QEMU 를 gdb 대기 상태로 시작 (gdb: target remote :1234)");
    debug_step.dependOn(&debug_cmd.step);
}
