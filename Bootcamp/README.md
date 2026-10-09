# Bootcamp

`riscvcli init` 으로 생성된 RISC-V 베어메탈 학습용 프로젝트 (RV64, `rv64gc` / `lp64d`).
OS 없이 **QEMU virt 머신**에서 직접 실행되며, Ubuntu 와 Apple Silicon Mac 에서 똑같이 동작한다.

## 동작 원리

1. QEMU 가 `-bios none -kernel x.elf` 로 ELF 를 RAM `0x80000000` 에 올리고 첫 명령부터 실행
2. `src/Boot.S` 의 `_start` 가 스택/BSS 를 준비한 뒤 `main` 호출
3. `src/Main.S` 의 `main` 이 UART(`0x10000000`)로 문자 출력
4. `main` 이 돌아오면 `qemu_exit` 가 종료 장치(`0x100000`)에 값을 써서 QEMU 종료

## 구성

- (언어 라이브러리 없음 — 순수 어셈블리 프로젝트. `--rust` 로 Rust 라이브러리를 추가할 수 있어요)

## 디렉토리

```
Bootcamp/
├── link.ld                # 링커 스크립트 (RAM 0x80000000, 스택/BSS 심볼)
├── build.zig              # 오케스트레이터 (Zig)
├── hun-build.cs           # 오케스트레이터 (.NET file-based app)
└── src/
    ├── Boot.S             # _start — 리셋 직후 코드
    ├── Main.S             # main — 여기서부터 작성
    ├── libs/platform.S    # uart_putc/puts/put_hex/put_dec, qemu_exit
    ├── includes/hun.macros.inc   # 섹션/함수/UART 매크로
    ├── constants/
    └── data/
```

## 필요한 도구

- **QEMU** — `qemu-system-riscv64`
  - Ubuntu: `sudo apt install qemu-system-misc`
  - macOS: `brew install qemu`
- **어셈블러/링커** (아래 중 하나)
  - Ubuntu: `sudo apt install gcc-riscv64-unknown-elf`
  - macOS: `brew install riscv64-elf-binutils riscv64-elf-gcc`
  - clang + ld.lld (RISC-V 타깃이 있는 LLVM, macOS 는 `brew install llvm lld`)
  - Zig 오케스트레이터를 쓰면 위 툴체인 없이 Zig 만으로도 된다
- 오케스트레이터 중 하나: **Zig** / **.NET SDK 10** / **PowerShell 7**

설치 상태는 `riscvcli doctor` 로 한 번에 점검할 수 있다.

## 빌드 & 실행

**Zig 오케스트레이터**:
```bash
zig build run
```

**.NET 오케스트레이터** (`hun-build.cs`, 툴체인 자동 탐지: GNU `riscv64-unknown-elf-` → `riscv64-elf-` → `riscv64-linux-gnu-` → clang/lld):
```bash
dotnet ./hun-build.cs              # 빌드 + 실행
dotnet ./hun-build.cs --no-run     # 빌드만
dotnet ./hun-build.cs --gdb        # QEMU 를 gdb 대기 상태로 시작
dotnet ./hun-build.cs --clean      # bin/ 삭제
```

## gdb 로 한 줄씩 따라가기

터미널 1: `dotnet ./hun-build.cs --gdb` (또는 `zig build debug`)

터미널 2:
```bash
gdb-multiarch bin/bootcamp.elf     # macOS: riscv64-elf-gdb
(gdb) set architecture riscv:rv64
(gdb) target remote :1234
(gdb) break main
(gdb) continue
(gdb) stepi          # 명령어 한 개씩 실행
(gdb) info registers a0 a1 sp ra
```

## 참고

- QEMU 를 강제로 끝내려면 `Ctrl-A` 누른 뒤 `X`
- `src/` 아래 `.S`/`.s` 파일은 `hun-build.cs`/`hun-build.ps1` 이 자동으로 모두 어셈블한다.
  `build.zig` 는 `asm_sources` 목록에 직접 추가해야 한다.
- 새 파일은 `riscvcli new src/Foo -t function -n foo` 로 만들 수 있다.
