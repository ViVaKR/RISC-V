# Setcion


RISC-V bare-metal에서는 **ELF section + linker script + 실제 메모리 주소 + QEMU machine memory map**까지 한꺼번에 연결해서 설명함.

먼저 ARM64의

```asm
.section __TEXT,__text,regular,pure_instructions
.align 2
```

와 RISC-V의 section을 **동일한 개념으로 1:1 대응시키면 안 된다**는 점부터 잡아두자.

---

## 1. 가장 먼저: `.section`은 CPU 명령어가 아니다

```asm
.section .text
```

는 CPU가 실행하는 instruction이 아니네.

즉:

```text
.section
.align
.global
.type
.size
.asciz
```

같은 것은 **assembler directive / assembler pseudo-op**이지 CPU instruction이 아님.

CPU가 실제로 실행하는 것은:

```asm
addi
ld
sd
jal
beq
...
```

같은 것들임.

따라서 소스 파일을 볼 때:

```text
┌─────────────────────────────┐
│ Assembly source             │
│                             │
│ .section  ← assembler       │  <- assembler 에게 
│ .global                     │  <- 알려주는 지시어
│ .align                      │
│                             │
│ addi      ← CPU instruction │
│ ld                          │
│ jal                         │
└─────────────────────────────┘
```

이렇게 층을 나누어 보는 것이 좋음.

---

## 2. 그런데 RISC-V bare-metal에서는 왜 `.text`가 중요한가?

프로그램을 다시 보세.

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

여기에는 사실 두 종류의 내용이 있네.

```text
.text
 └── CPU가 실행할 instruction

.rodata
 └── CPU가 읽을 read-only data
```

즉:

```text
              hello.s
                 │
        ┌────────┴────────┐
        ▼                 ▼
      .text             .rodata
        │                 │
        ▼                 ▼
   instructions         string
        │                 │
        └────────┬────────┘
                 ▼
              linker
                 │
                 ▼
             hello.elf
```

이렇게 되는 것.

---

### 3. ARM64 macOS와 RISC-V bare-metal의 가장 큰 차이

Apple Silicon M 시리즈에서 작성하는 ARM64 코드는 대개:

```asm
.section __TEXT,__text,regular,pure_instructions
```

처럼 **Mach-O object format**의 section naming을 사용하지.

반면 우리가 QEMU에서 사용하는 RISC-V 프로그램은 보통:

```text
ELF
```

를 사용함.

따라서:

### Apple Silicon / macOS

```text
Assembly
   ↓
Mach-O
   ↓
macOS loader
```

### RISC-V bare-metal / QEMU

```text
Assembly
   ↓
ELF
   ↓
linker script
   ↓
physical address
   ↓
QEMU machine
```

가 되는 것이네.

이 차이가 상당히 중요하다네.

---

# 4. RISC-V에서 가장 기본적인 section

일반적인 GNU/LLVM ELF 환경에서 자주 만나는 section은 다음과 같네.

```text
.text
.rodata
.data
.bss
```

그리고 필요에 따라:

```text
.init
.fini
.sdata
.sbss
.tdata
.tbss
```

등도 등장할 수 있네.

하지만 **처음 bare-metal OS를 만드는 단계에서는**

```text
.text
.rodata
.data
.bss
```

네 개를 먼저 완전히 이해하면 됨.

---

### 5. `.text`

가장 기본적인 코드 section.

```asm
.section .text
```

뜻은:

> 이후에 나오는 내용을 `.text` section에 넣어라.

예:

```asm
.section .text

.global _start

_start:
    addi a0, zero, 10
    addi a1, zero, 20
    add  a0, a0, a1

halt:
    j halt
```

링커가 최종 ELF를 만들면:

```text
.text
┌──────────────────────────┐
│ instruction              │
│ instruction              │
│ instruction              │
│ instruction              │
└──────────────────────────┘
```

이런 영역이 생김.

---

## 6. `.rodata`

Read Only Data.

```asm
.section .rodata
```

예를 들어 문자열:

```asm
.section .rodata

msg:
    .asciz "Hello, RISC-V!\n"
```

이것은 instruction이 아님.

메모리에는:

```text
48 65 6c 6c 6f 2c 20 ...
```

같은 byte sequence가 들어가게 되지.

그리고:

```asm
la a1, msg
```

를 통해 그 주소를 얻어서:

```asm
lb a2, 0(a1)
```

로 읽는 것 뿐임.

---

## 7. `.data`

이번에는 **초기값을 가지고 있는 writable data**일세.

예:

```asm
.section .data

counter:
    .quad 123

message:
    .asciz "Hello"
```

링커 결과는 대략:

```text
.data
┌─────────────────────────┐
│ counter = 123           │
│ message = "Hello"       │
└─────────────────────────┘
```

이 영역은 프로그램이 실행된 뒤 변경할 수 있음.

예:

```asm
la t0, counter
ld t1, 0(t0)
addi t1, t1, 1
sd t1, 0(t0)
```

---

## 8. `.bss`

이것이 OS를 만들 때 상당히 중요해지네.

`.bss`는:

> 초기값이 0인 writable data 또는 실행 전에 0으로 초기화되어야 하는 공간

이라고 이해하면 좋네.

예:

```asm
.section .bss

.align 3
buffer:
    .skip 1024
```

그러면:

```text
.bss
┌────────────────────────────┐
│                            │
│       1024 byte            │
│       buffer               │
│                            │
└────────────────────────────┘
```

같은 영역이 생기네.

여기서 중요한 특징:

**`.bss`는 ELF 파일 안에 1024개의 0을 반드시 저장할 필요가 없다.**

ELF에서는:

```text
파일에서 차지하는 공간
        ≠
실행할 때 필요한 메모리 공간
```

으로 표현할 수 있기 때문이지.

이것이 나중에 linker script와 `NOBITS`를 이해할 때 연결된다네.

---

## 9. section을 한꺼번에 보면

```text
             ELF
              │
     ┌────────┼────────┐
     │        │        │
     ▼        ▼        ▼
   .text    .rodata   .data
     │        │        │
  code      constant  writable
                      initialized
     
                  .bss
                    │
                 writable
                 zero-init
```

이것이 가장 기본적인 메모리 구성이라네.

---

## 10. 그런데 bare-metal에서는 "section을 만들었다"만으로 끝나지 않는다

이 부분이 아주 중요하네.

예를 들어:

```asm
.section .text
```

했다고 해서 자동으로:

```text
0x80000000
```

에 들어가는 것은 아니네.

**링커가 최종 주소를 결정한다.**

즉:

```text
Assembly
   │
   │ section
   ▼
hello.o
   │
   │ linker script
   ▼
hello.elf
   │
   │ address assignment
   ▼
.text     = 0x80000000
.rodata   = 0x80000100
.data     = 0x80000200
.bss      = 0x80000300
```

같은 식으로 결정되는 것이네.

---

## 11. 그래서 linker script가 등장한다

RISC-V bare-metal에서는 이것을 반드시 이해해야 하네.

예를 들어:

```ld
ENTRY(_start)

SECTIONS
{
    . = 0x80000000;

    .text : {
        *(.text)
    }

    .rodata : {
        *(.rodata)
    }

    .data : {
        *(.data)
    }

    .bss : {
        *(.bss)
    }
}
```

이것은 **RISC-V instruction이 아니다.**

이것은:

> 링커에게 최종 ELF의 메모리 배치를 지시하는 linker script

라네.

---

## 12. 이 한 줄이 굉장히 중요하다

```ld
. = 0x80000000;
```

이것은:

> 현재 location counter를 `0x80000000`으로 설정한다.

는 의미네.

그래서:

```ld
.text : {
    *(.text)
}
```

가 이어지면 `.text`가 그 주소 근처에 배치되는 것임.

즉:

```text
0x80000000
        │
        ▼
┌─────────────────────┐
│ .text               │
│                     │
│ _start              │
│ print_loop          │
│ halt                │
└─────────────────────┘
        │
        ▼
┌─────────────────────┐
│ .rodata             │
│                     │
│ "Hello, RISC-V..."  │
└─────────────────────┘
        │
        ▼
┌─────────────────────┐
│ .data               │
└─────────────────────┘
        │
        ▼
┌─────────────────────┐
│ .bss                │
└─────────────────────┘
```

같은 배치가 되는 것이지.

---

## 13. QEMU `virt`에서 특히 조심할 것

여기서 UART:

```asm
li a0, 0x10000000
```

가 등장함.

이것은:

```text
0x10000000
```

이라는 **MMIO 주소**네.

그리고 프로그램 코드의 주소:

```text
0x80000000
```

와 전혀 다른 영역임.

즉:

```text
RISC-V address space

0x00000000
      │
      │
      │
0x10000000 ───── UART MMIO
      │
      │
      │
      │
0x80000000 ───── RAM / kernel image
      │
      ├──────── .text
      ├──────── .rodata
      ├──────── .data
      └──────── .bss
```

처럼 생각하면 된다네.

**section과 MMIO는 서로 다른 개념**이네.

`.text`가 UART가 아니고 `.data`가 RAM이라는 식으로 단순하게 생각하면 안 된다네.

section은 **ELF 내부의 논리적인 분류**이고, linker script와 machine memory map을 통해 실제 주소 공간에 배치되는 것이지.

---

## 14. `.align`도 알아두자

ARM64에서:

```asm
.align 2
```

를 봤다고 했지.

RISC-V에서도:

```asm
.align 2
```

를 사용할 수 있음

다만 assembler 문법/의미가 플랫폼과 assembler에 따라 다를 수 있으므로 

**LLVM/GNU assembler에서 어떤 단위로 해석되는지 확인하면서 사용하는 것이 좋다.**

LLVM의 RISC-V assembler에서는 흔히:

```asm
.p2align 2
```

같은 표현도 볼 수 있네.

개념적으로:

```text
alignment = 2^N
```

이라는 방식으로 이해하면 좋음.

예:

```asm
.p2align 2
```

→ 4-byte alignment.

---

# 15. RISC-V instruction alignment와도 연결된다

기본 RV32I/RV64I instruction은:

```text
32 bit = 4 byte
```

이네.

따라서:

```asm
.p2align 2
```

가 자연스럽게 등장할 수 있지.

하지만 `C` extension이 있으면:

```text
16-bit compressed instruction
```

도 존재하기 때문에 instruction alignment에 관한 이야기가 조금 더 복잡해진다네.

이것은 나중에 `RV64GC`로 갈 때 이야기하면 아주 좋겠네.

---

## 16. `.global _start`

이것도 section과 함께 설명해야 하네.

```asm
.global _start
```

뜻은:

> `_start`라는 symbol을 다른 object file에서도 볼 수 있는 global symbol로 만들어라.

즉:

```text
_start
```

라는 label 자체와:

```asm
.global _start
```

는 서로 다른 개념이지.

```asm
_start:
```

→ symbol 정의

```asm
.global _start
```

→ symbol visibility 지정


---

## 17. 왜 `_start`가 필요한가?

링커가 프로그램의 entry point를 알아야 하기 때문이네.

linker script에서:

```ld
ENTRY(_start)
```

라고 하면:

```text
ELF entry point
        ↓
     _start
        ↓
0x80000000
```

같은 관계가 만들어지네.

그리고 QEMU가 이미지를 실행하기 시작하면 결국 CPU가 entry point에서 instruction을 실행하게 되는 것이지.

---

## 18. `.type`도 알아두면 좋네

LLVM/GAS 계열에서 다음을 볼 수 있네.

```asm
.type _start, @function
```

뜻은 `_start`를 function symbol로 표시하는 것.

예:

```asm
.global _start
.type _start, @function

_start:
    ...
```

그리고 함수 끝에서:

```asm
.size _start, .-_start
```

같은 것도 볼 수 있네.

이것들은 특히:

```text
ELF symbol table
DWARF
debugger
objdump
```

에서 의미가 생긴다네.

---

# 19. 그래서 디버깅과도 연결된다

자네가 앞에서 물었던 LLDB가 여기서 다시 등장한다네.

```text
_start
   │
   ├── symbol
   │
   ├── address
   │
   ├── section
   │
   └── DWARF
         │
         ▼
       LLDB
```

그래서 LLDB에서:

```text
(lldb) b _start
```

가 가능한 것이네.

그리고:

```text
(lldb) image lookup -n _start
```

같은 명령으로 symbol 정보를 조사할 수도 있네.

---

## 20. 이제 Hello World를 "교과서용"으로 조금 제대로 작성해보자

나는 강좌용이라면 다음 정도까지는 보여주는 것을 추천하네.

```asm
.section .text

.global _start
.type _start, @function

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
    j    halt

.size _start, .-_start


.section .rodata

msg:
    .asciz "Hello, RISC-V bare metal! Start...\n"
```

이 정도면:

```text
.section
.global
.type
label
instruction
.size
.rodata
.asciz
```

를 모두 설명할 수 있네.

---

## 21. 그리고 linker script

강좌에서는 이것도 함께 보는 것이 매우 중요함.

```ld
ENTRY(_start)

SECTIONS
{
    . = 0x80000000;

    .text : ALIGN(4)
    {
        *(.text)
    }

    .rodata : ALIGN(4)
    {
        *(.rodata)
    }

    .data : ALIGN(8)
    {
        *(.data)
    }

    .bss : ALIGN(8)
    {
        *(.bss)
        *(COMMON)
    }
}
```

그리고:

```text
Assembly source
        ↓
Assembler
        ↓
hello.o
        ↓
Linker script
        ↓
hello.elf
```

를 보여주면 상당히 좋네.

---

## 22. 그런데 여기서 아주 중요한 개념이 하나 더 있다

**ELF Section과 ELF Segment는 다르다.**

이것은 나중에 반드시 등장한다네.

```text
ELF
│
├── Sections
│    ├── .text
│    ├── .rodata
│    ├── .data
│    └── .bss
│
└── Segments
     ├── LOAD
     ├── LOAD
     └── ...
```

Section은 주로:

```text
linking
symbol
debugging
organization
```

관점이고,

Segment는 **실행할 때 loader가 어떻게 메모리에 배치할 것인가**라는 관점임.

일반 OS에서는 이 차이가 매우 중요하고, bare-metal에서도 ELF를 어떻게 QEMU에 넘기는지 이해할 때 중요해짐.

---

## 23. `llvm-readelf`로 직접 확인하면 된다

이제 이부분에서 LLVM 도구가 등장 😄

```bash
llvm-readelf -S hello.elf
```

그러면:

```text
Section Headers:

[Nr] Name
     .text
     .rodata
     .data
     .bss
     ...
```

를 볼 수 있네.

그리고:

```bash
llvm-readelf -s hello.elf
```

하면:

```text
_start
msg
```

등의 symbol을 볼 수 있지.

그리고:

```bash
llvm-readelf -l hello.elf
```

하면 **Program Headers / Segments**를 볼 수 있네.

이 세 개를 비교해보면:

```text
llvm-readelf -S
        ↓
Section

llvm-readelf -s
        ↓
Symbol

llvm-readelf -l
        ↓
Segment
```

가 된다네.

이건 꼭 한번 해보기를 권하네.

---

## 24. ARM64와 RISC-V를 이렇게 비교하면 아주 깔끔함.

|                           | Apple Silicon / macOS  | RISC-V bare-metal             |
| ------------------------- | ---------------------- | ----------------------------- |
| CPU                       | AArch64                | RV64                          |
| Object format             | Mach-O                 | ELF                           |
| Code section              | `__TEXT,__text`        | `.text`                       |
| Read-only data            | `__TEXT,__const` 등     | `.rodata`                     |
| Writable initialized data | `__DATA,__data`        | `.data`                       |
| Zero-init data            | `__DATA,__bss`         | `.bss`                        |
| Linker                    | Apple `ld` 계열          | `ld.lld` 등                    |
| Loader                    | macOS                  | 직접/firmware/QEMU              |
| Entry                     | Mach-O entry mechanism | ELF `ENTRY(_start)` 등         |
| Debugger                  | LLDB                   | LLDB + GDB remote/QEMU        |
| OS                        | macOS                  | 없음/bare-metal                 |
| Memory map                | OS가 관리                 | linker + machine/firmware가 결정 |

다만 이것은 **개념적인 비교표**이고, macOS Mach-O section/segment 명칭은 더 세분화되어 있다는 점은 기억하면 좋네.

---

## 25. 그래서 RISC-V bare-metal의 "전체 그림"


```text
                     RISC-V SOURCE
                           │
                           │
             ┌─────────────┴─────────────┐
             │                           │
             ▼                           ▼
        .section .text             .section .rodata
             │                           │
             │ instructions              │ constants
             │                           │
             └─────────────┬─────────────┘
                           │
                           ▼
                        llvm-mc
                           │
                           ▼
                       hello.o
                           │
                           │
                    ┌──────▼──────┐
                    │ linker      │
                    │ ld.lld      │
                    │ + script    │
                    └──────┬──────┘
                           │
                           ▼
                       hello.elf
                           │
             ┌─────────────┼──────────────┐
             │             │              │
             ▼             ▼              ▼
           .text        .rodata         .data
             │             │              │
             └─────────────┼──────────────┘
                           │
                          .bss
                           │
                           ▼
                     Memory Layout
                           │
                           ▼
                   QEMU RISC-V virt
                           │
             ┌─────────────┴─────────────┐
             │                           │
        RAM 0x80000000             UART 0x10000000
             │                           │
             │                           │
             ▼                           ▼
       RISC-V CPU                    MMIO output
             │
             ▼
           LLDB
```

이 그림을 이해하면 **section → linker → address → ELF → CPU → MMIO → debugger**가 한 덩어리로 연결된다네.

---

## 26. 마지막으로 하나만 바로잡아 두자

ARM64:

```asm
.section __TEXT, __text, regular, pure_instructions
.align 2
```

에서 `__TEXT`, `__text`는 **macOS Mach-O의 segment/section 명명 체계**이고,

RISC-V ELF에서는 보통:

```asm
.section .text
```

라고 쓰네.

따라서 RISC-V에서는 ARM64의 것을 그대로 흉내 내서:

```asm
.section __TEXT, __text
```

같이 만들 필요가 없네.

**RISC-V ISA가 `.text`를 요구하는 것도 아니고**, ELF toolchain의 관례와 linker script가 그렇게 구성되어 있는 것이네.

이 구분이 아주 중요하다네:

```text
RISC-V ISA
    ≠
ELF
    ≠
Assembler syntax
    ≠
Linker script
    ≠
QEMU machine
    ≠
UART memory map
```

각각 다른 층이다.

---

## 🌱 그리고 지금 자네에게 가장 좋은 다음 실습

이제 바로 다음 단계로 가면 정말 좋겠네.

**하나의 `hello.s`를 가지고:**

```bash
llvm-mc
   ↓
hello.o
   ↓
llvm-readelf -S
   ↓
ld.lld + linker.ld
   ↓
hello.elf
   ↓
llvm-readelf -S
llvm-readelf -s
llvm-readelf -l
llvm-objdump -d
   ↓
QEMU
   ↓
LLDB
```

를 **실제로 한 단계씩 실행하면서 각 출력물을 비교**해보는 것이네.

특히 `hello.o`와 `hello.elf`에서 **`.text`, `.rodata`, `_start`, `msg`의 주소가 어떻게 변하는지** 보면 linker의 존재 이유가 눈앞에 나타난다네.

그 다음에는 `.data`와 `.bss`를 추가하고, 마지막에는 **`stack`까지 linker script로 배치해서 `sp`를 초기화**해보세. 그러면 우리가 앞에서 이야기한 `callee-saved`, `ra`, `sp`, stack frame까지 정확히 이어지네.

이것까지 연결되면 자네의 **"RISC-V CPU → Assembly → ELF → Linker → Memory → QEMU → LLDB → OS"**라는 큰 그림이 거의 한 줄로 관통하기 시작할 걸세. 하하하.
