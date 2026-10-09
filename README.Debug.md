# 디버깅

**RISC-V bare-metal 환경 실습 구조**

> **`llvm-mc` = 어셈블러**
> **`ld.lld` = 링커**
> **QEMU = RISC-V 머신 실행**
> **QEMU GDB Stub = 디버깅 통로**
> **LLDB = 그 통로에 붙는 디버거**

즉 `llvm-mc` 자체로 디버깅하는 것은 아니고, **LLVM로 만든 ELF + DWARF를 LLDB가 읽고, QEMU의 GDB Remote Stub에 접속**하는 구조. QEMU 공식 문서도 `-s -S`로 GDB stub을 열어 레지스터·메모리·브레이크포인트 등을 디버깅하는 방식을 지원하고 있고, LLDB 역시 bare-metal의 경우 QEMU 내장 GDB stub에 연결하는 방법을 명시 ([QEMU][1])

---

## 1. LLDB 디버깅에 적합한 소스코드

```asm
.section .text

.global _start

_start:
    li   a0, 0x10000000
    la   a1, msg

print_loop:
    lb   a2, 0(a1)
    beqz a2, halt
    sb   a2, 0(a0)
    addi a1, a1, 1
    j    print_loop

halt:
    j halt

.section .rodata

msg:
    .asciz "Hello, RISC-V bare metal! Start...\n"
```

여기서 재미있는 점이 하나

### `li`, `la`, `beqz`, `j`

이것들은 RISC-V의 **pseudo-instruction**이네.

예를 들어:

```asm
li a0, 0x10000000
```

를 `llvm-mc`가 실제 RISC-V 명령어들로 확장함.

그리고

```asm
j print_loop
```

도 실제 branch/jump instruction으로 변환되네.

따라서 우리가 전에 했던

```bash
printf 'add a0, a1, a2\n' | llvm-mc ...
```

실험과 **완전히 같은 LLVM 파이프라인 위에서**, 이번에는 ELF + DWARF까지 붙이는 것이네.

---

# 2. 내가 추천하는 전체 구조

지금 프로젝트 방향이라면 이렇게 가는 게 아주 깔끔하네.

```text
                ┌──────────────────┐
                │   source.s       │
                │ RISC-V Assembly  │
                └────────┬─────────┘
                         │
                         ▼
                  ┌────────────┐
                  │  llvm-mc   │
                  │ assembler  │
                  └─────┬──────┘
                        │
                        ▼
                  ┌────────────┐
                  │  object.o  │
                  │ ELF/DWARF  │
                  └─────┬──────┘
                        │
                        ▼
                  ┌────────────┐
                  │   ld.lld   │
                  │   linker   │
                  └─────┬──────┘
                        │
                        ▼
                 ┌──────────────┐
                 │ helloworld   │
                 │ ELF + DWARF  │
                 └──────┬───────┘
                        │
                        ▼
              ┌──────────────────┐
              │      QEMU        │
              │ qemu-system-     │
              │      riscv64     │
              └────────┬─────────┘
                       │
                 GDB Remote
                  Protocol
                       │
                       ▼
                 ┌───────────┐
                 │   LLDB    │
                 └───────────┘
```

이게 아주 중요한 구조라네.

**M4는 ARM64지만 디버깅 대상은 RISC-V64**다.

즉:

```text
M4
 └── macOS ARM64
      └── LLDB
           │
           │ GDB Remote Protocol
           ▼
        QEMU
           │
           ▼
      RISC-V64 CPU
```

이런 식이지.

---

# 3. 먼저 ELF를 디버그 정보와 함께 작성

예를 들어:

```bash
llvm-mc \
  -triple=riscv64 \
  -filetype=obj \
  -g helloworld.rv \
  -o helloworld.o
```

그리고 `ld.lld`:

```bash
ld.lld \
  -m elf64lriscv \
  -Ttext=0x80000000 \
  helloworld.o \
  -o helloworld.elf
```

여기서 중요한 것은:

```text
helloworld.elf
```

를 **버리지 않는 것**이네.

QEMU에는 이것을 넣고:

```bash
qemu-system-riscv64 \
    -machine virt \
    -nographic \
    -bios none \
    -kernel helloworld.elf
```

LLDB에도 **동일한 ELF**를 넣는 것이네.

왜냐하면 LLDB는 ELF 안의:

```text
symbol
DWARF
source line
address
section
```

정보를 이용하기 때문이네.

---

# 4. QEMU를 멈춘 상태로 시작

여기가 핵심

```bash
qemu-system-riscv64 \
    -machine virt \
    -nographic \
    -bios none \
    -kernel hello.elf \
    -S \
    -s
```

두 옵션의 의미는:

```text
-S
```

→ CPU를 **시작하지 않고 정지**

그리고

```text
-s
```

→ QEMU가 TCP `1234`에서 GDB Remote connection을 기다림.

QEMU 공식 문서에서도 이 조합을 사용한다네. ([QEMU][1])

따라서 터미널에는 QEMU가 떠 있지만 CPU는 아직 실행하지 않는 상태가 되네.

---

# 5. 이제 LLDB

다른 터미널에서:

```bash
lldb hello.elf
```

그리고:

```text
(lldb) gdb-remote 127.0.0.1:1234
```

그러면 LLDB가 QEMU에 붙네.

LLDB의 remote debugging은 GDB Remote Protocol을 사용하기 때문에 이 구조가 가능하네. ([LLDB][2])

---

# 6. `_start`에 breakpoint

이제:

```text
(lldb) breakpoint set --name _start
```

또는 간단하게:

```text
(lldb) b _start
```

그리고:

```text
(lldb) continue
```

그러면 `_start`에서 멈출 것이네.

그 순간:

```text
(lldb) register read
```

하면 RISC-V 레지스터를 볼 수 있네.

예:

```text
x0
ra
sp
gp
tp
t0
t1
...
a0
a1
a2
...
```

그리고:

```text
(lldb) register read a0
(lldb) register read a1
(lldb) register read a2
```

처럼 볼 수도 있네.

---

# 7. 이 코드에서는 이것을 관찰하면 아주 재미있다네

처음:

```asm
li a0, 0x10000000
```

에서 멈춰서:

```text
(lldb) register read a0
```

그리고 한 명령 실행:

```text
(lldb) stepi
```

다시:

```text
(lldb) register read a0
```

그러면:

```text
a0 = 0x10000000
```

이 들어가는 과정을 직접 볼 수 있네.

그 다음:

```asm
la a1, msg
```

실행하고:

```text
(lldb) register read a1
```

그러면 `msg`의 주소가 들어가는 것을 볼 수 있네.

---

# 8. 더 재미있는 부분: `lb`

여기서:

```asm
lb a2, 0(a1)
```

에 breakpoint를 걸어보게.

```text
(lldb) b print_loop
(lldb) c
```

그리고:

```text
(lldb) stepi
```

후:

```text
(lldb) register read a1 a2
```

그러면 `a2`에 첫 번째 문자:

```text
'H'
```

의 ASCII 값인:

```text
0x48
```

이 들어오는 것을 볼 수 있네.

다시:

```text
stepi
```

하면:

```asm
beqz a2, halt
```

다시:

```text
stepi
```

하면:

```asm
sb a2, 0(a0)
```

여기까지 오지.

즉 우리가 머릿속으로 생각했던:

```text
msg
 ↓
H
 ↓
a2
 ↓
a0 = UART
 ↓
store byte
 ↓
UART 출력
```

이 과정을 **CPU 레지스터 수준에서 직접 관찰**할 수 있네.

이게 바로 bare-metal 디버깅의 맛이라네. 하하하.

---

# 9. 메모리도 LLDB에서 볼 수 있다

예를 들어 `a1`이 `msg` 주소를 가지고 있을 때:

```text
(lldb) memory read --format x --size 1 --count 32 $a1
```

또는:

```text
(lldb) x/32xb $a1
```

형태로 메모리를 볼 수 있네.

문자열로 보고 싶다면:

```text
(lldb) memory read --format c --size 1 --count 40 $a1
```

그러면:

```text
H e l l o , ...
```

를 직접 볼 수 있지.

---

# 10. 그런데 `llvm-mc`는 어디에 쓰는가?

여기서 **두 가지 역할을 분리하면 머리가 아주 깨끗해진다네.**

### `llvm-mc`

```text
Assembly
   ↓
Machine Code
```

즉:

```bash
llvm-mc \
  -triple=riscv64 \
  -show-encoding \
  hello.s
```

로:

```asm
addi a1, a1, 1
```

이 어떤 machine code가 되는지 확인할 수 있네.

그리고:

```bash
llvm-mc \
  -triple=riscv64 \
  -filetype=obj \
  -g \
  hello.s \
  -o hello.o
```

로 실제 object file을 만드는 역할.

---

### LLDB

반면:

```text
ELF
 ↓
DWARF
 ↓
address ↔ source
 ↓
register
 ↓
memory
 ↓
breakpoint
 ↓
single step
```

를 담당하네.

따라서:

> **llvm-mc로 디버깅한다**

라기보다는

> **llvm-mc로 만든 RISC-V ELF를 LLDB로 디버깅한다**

가 정확한 표현이라네.

---

# 11. 그리고 `llvm-objdump`가 중간에서 굉장히 중요하다네

사실 자네가 지금 하려는 공부에는 이것도 반드시 같이 쓰는 것을 추천하네.

```bash
llvm-objdump \
    -d \
    --mattr=+m,+a,+f,+d,+c \
    hello.elf
```

혹은 단순히:

```bash
llvm-objdump -d hello.elf
```

그러면:

```text
80000000 <_start>:
    ...
```

형태로 실제 machine instruction을 볼 수 있네.

그리고:

```bash
llvm-readelf -S hello.elf
```

```bash
llvm-readelf -s hello.elf
```

```bash
llvm-dwarfdump hello.elf
```

까지 연결하면:

```text
Assembly
    ↓
llvm-mc
    ↓
Machine Code
    ↓
ELF
    ↓
Symbol
    ↓
DWARF
    ↓
QEMU
    ↓
LLDB
```

라는 **하나의 완전한 흐름**을 이해하게 된다네.

---

# 12. 한 가지 중요한 함정

여기서 자네가 올린 코드의:

```asm
li a0, 0x10000000
```

부분은 **QEMU `virt` 머신에서 UART 주소가 실제로 어디인지**와 연결해서 생각해야 하네.

즉:

```text
RISC-V instruction
        ↓
a0 = 0x10000000
        ↓
sb a2, 0(a0)
        ↓
QEMU virt machine의 MMIO
        ↓
UART
```

라는 하드웨어 모델이 맞아야 출력이 나오는 것이네.

그리고 이것은 **RISC-V ISA 자체가 정해주는 것이 아니라 machine/platform의 memory map 문제**라네.

이 구분이 앞으로 OS를 만들 때 엄청나게 중요해질 것이네.

```text
RISC-V ISA
 ├── add
 ├── ld
 ├── sd
 ├── jal
 └── ...

QEMU virt platform
 ├── UART
 ├── CLINT/ACLINT
 ├── PLIC
 ├── RAM
 └── 기타 MMIO
```

즉 **ISA와 SoC/platform을 분리해서 생각하는 습관**을 지금부터 들이는 게 좋네.

---

# 13. 그래서 내가 자네라면 이렇게 공부하겠네

현재 목표가 단순히 "Hello World"가 아니라 **나중에 한글 OS → 새로운 아키텍처/파운드리까지 탐구**하는 것이라면, 다음 4개를 한 세트로 돌리겠네.

```text
             ┌──────────────┐
             │   llvm-mc    │
             │ instruction  │
             │   encoding   │
             └──────┬───────┘
                    │
                    ▼
             ┌──────────────┐
             │ llvm-objdump │
             │ disassembly  │
             └──────┬───────┘
                    │
                    ▼
             ┌──────────────┐
             │     QEMU     │
             │ RISC-V CPU   │
             └──────┬───────┘
                    │
                    ▼
             ┌──────────────┐
             │     LLDB     │
             │ PC/register  │
             │ memory/step  │
             └──────────────┘
```

그리고 **GCC/GDB를 굳이 끼워 넣을 필요가 없네.**

현재 자네가 선호하는 LLVM 중심 환경에서:

```text
clang
llvm-mc
ld.lld
llvm-objdump
llvm-readelf
llvm-dwarfdump
lldb
        +
qemu-system-riscv64
```

로 상당히 훌륭한 RISC-V bare-metal 연구 환경을 만들 수 있네.

특히 LLDB 공식 문서도 **bare-metal에서는 QEMU의 내장 GDB stub에 연결하는 것이 유용하다**고 명시하고 있다네. ([LLDB][2])

---

## 그리고 한 단계 더 재미있는 실험이 있다네

자네가 방금 올린 프로그램을 가지고 다음 단계에서는 **`li`, `la`, `beqz`, `j`를 전부 제거하고 순수 RV64 실제 instruction만으로 작성한 버전**을 하나 만들어 보는 걸 권하네.

그러면 우리가 전에 했던:

```text
add a0,a1,a2
        ↓
0x00c58533
```

했던 실험이 그대로 이어지네.

즉:

```text
소스
 ↓
pseudo instruction
 ↓
llvm-mc
 ↓
실제 instruction
 ↓
encoding
 ↓
ELF
 ↓
QEMU
 ↓
LLDB
 ↓
PC / register / memory
```

를 **한 프로그램에서 전부 연결해서 볼 수 있게** 된다네.

이 단계까지 가면 자네가 말한 "안개가 걷히는 느낌"이 한 번 더 올 가능성이 높네. 하하하.

[1]: https://www.qemu.org/docs/master/system/gdb.html?utm_source=chatgpt.com "GDB usage — QEMU documentation"
[2]: https://lldb.llvm.org/resources/debugging.html?utm_source=chatgpt.com "Debugging - 🐛 LLDB"
