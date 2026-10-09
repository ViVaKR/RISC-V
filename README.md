# RISC-V
인류의 청정 유산 RISC-V 어셈블리 정복을 위한 상남자 마스터 코스 (RealManMastery). C/C++의 해괴한 굴레를 벗겨내고 태초의 가장 순수하고 정갈한 미니멀리즘 아키텍처의 예술적 가치를 증명한다.


```bash
clang -g \
  --target=riscv64-unknown-elf \
  -march=rv64gc \
  -mabi=lp64d \
  -nostdlib \
  -static \
  -fuse-ld=lld \
  -o helloworld \
  helloworld.s


  qemu-system-riscv64 \
    -machine virt \
    -nographic \
    -bios none \
    -kernel helloworld
```

```bash

g ctrl + a
gq
ctrl + a
gcc : comment

C+u
C+d
C+b
C+f

일련번호 : qqyyp ctrl+a q 98@q

:tabf <filename>
:tabnew <filename>
:tabnew
:e
:tabc
:saveas <filename>
:file
:Explore

# open terminal
:below terminal
:top terminal
:bo terminal
:vert term

# 분할
ctrl+w,s (:sp filename)
ctrl+w,v (:vs filename)

# 포커스 이동
ctrl+w,w
ctrl+w,t
ctrl+w,b
ctrl+w,(h,j,k,l)

# 같은 크기
ctrl+w=

# 크기조절
:res +숫자
:vert res +숫자

# 쉘 명령
:!ls
```

## Init

```bash
  qemu-system-riscv64 -machine virt -nographic -bios default
  qemu-system-riscv64 --version
  qemu-system-riscv64 -machine help
  qemu-system-riscv64 -cpu help

  which ld.lld
  ld.lld --version

  which clang
  clang --version
  clang --print-targets | gerp -i riscv

  which llvm-mc
  llvm-mc --version

  # 4바이트 machine code view
  llvm-mc -triple=riscv64 -show-encoding
  addi a0, zero, 42 # a0 = 0 + 42, a0 = 42
  Enter
  encodeing
  Ctrl-D
  li a0, 42
  # encoding:
  # [0x13,0x05,0xa0,0x02]
  # instruction word -> 0x02a00512 (little-endian)

  # 메모리
  # 주소   바이트
  # +0    13
  # +1    05
  # +2    a0
  # +3    02

```

## 디버깅

>- llvm-mc = 어셈블러
>- ld.lld = 링커
>- QEMU = RISC-V 머신 실행
>- QEMU GDB Stub = 디버깅 통로
>- LLDB = 그 통로에 붙는 디버거

즉, `llvm-mc` 자체로 디버깅 하는 것은 아니고 **LLVM로 만든 ELF + DWARF를 LLDB가 읽고 QEMU의 GDB Remote Stub에 접속** 하는 구조.

```bash
llvm-mc -triple=riscv64 -filetype=obj -g helloworld.rv -o helloworld.o

ld.lld -m elf64lriscv -Ttext=0x80000000 helloworld.o -o helloworld.elf

# 실행
qemu-system-riscv64 -machine virt -nographic -bios none -kernel helloworld.elf

# 디버그 중지 모드
qemu-system-riscv64 -machine virt -nographic -bios none -kernel helloworld.elf -S -s

# 다른 터미널에서
lldb helloworld.elf

(lldb) gdb-remote 127.0.0.1:1234
(lldb) breakpoint set --name _start
(lldb) b _start

(lldb) continue # _start 에서 멈춤
(lldb) register read
(lldb) b print_loop
(lldb) c

(lldb) stepi
```

## 명령어 구조 샘플

```text
31                 20 19       15 14    12 11        7 6          0
┌────────────────────┬───────────┬────────┬────────────┬────────────┐
│      immediate     │    rs1    │ funct3 │     rd     │   opcode   │
│       12 bit       │   5 bit   │ 3 bit  │   5 bit    │   7 bit    │
└────────────────────┴───────────┴────────┴────────────┴────────────┘

즉시값 12비트로 표현할 수 있는 비트 패턴 은 0 ~ 4095
하지만 예를 들어 signed 12 비트의 실제 값의 범위는 -2048 ~ +2047 임.
```

```asm
imm     => 42
rs1     => zero = x0
rd      => a0   = x10
opcode  => 0010011 (0x13)


```

```text
              Apple M4
                 │
                 │ macOS ARM64
                 ▼
        ┌─────────────────┐
        │      LLVM       │
        │                 │
        │ clang           │
        │ llvm-mc         │
        │ ld.lld          │
        └────────┬────────┘
                 │
                 │ RISC-V ELF
                 ▼
        ┌─────────────────┐
        │      QEMU       │
        │                 │
        │ riscv64         │
        │ virt            │
        └────────┬────────┘
                 │
                 ▼
             RV64 CPU
```

## 어셈블리 바이메탈

```text
          addi
           │
           ▼
       assembler
           │
           ▼
      machine code
           │
           ▼
          ELF
           │
           ▼
          QEMU
           │
           ▼
       register
```

## ISA -> instruction encoding -> ELF -> CPU -> register

```text
        ① llvm-mc
             ↓
      "명령어가 뭐지?"
             ↓
        ② ld.lld
             ↓
       "메모리에 어떻게 놓지?"
             ↓
        ③ QEMU
             ↓
       "CPU가 어떻게 실행하지?"
             ↓
        ④ GDB
             ↓
       "레지스터가 어떻게 변하지?"
             ↓
        ⑤ 필요하면 clang
```

```text
Apple M4
   │ ARM64 macOS
   ▼
 QEMU
   │
   ├── RISC-V CPU
   │      ├── x0 ~ x31
   │      ├── PC
   │      ├── CSRs
   │      └── RV64 ISA
   │
   └── virt machine
          │
          └── RAM / UART / devices
```

## lui

>- Load Upper Immediate
>- U-type 명령어

```text
31                         12 11        7 6       0
┌────────────────────────────┬───────────┬─────────┐
│       immediate[31:12]     │    rd     │ opcode  │
└────────────────────────────┴───────────┴─────────┘
              20 bit
```

### 32bit 상수

```asm
lui a0, 0x12345
// a0 = 0x12345000
// 위쪽 부분을 한꺼번에 세팅
// 32 비트 정도의 상수는
lui z0, 0x12345
addi a0, a0, 0x678

// Test
llvm-mc -triple=riscv64 -show-encoding
li a0, 0x123456789abcdef0
^D
        lui     a0, 583                         # encoding: [0x37,0x75,0x24,0x00]
        addi    a0, a0, -1875                   # encoding: [0x13,0x05,0xd5,0x8a]
        slli    a0, a0, 14                      # encoding: [0x13,0x15,0xe5,0x00]
        addi    a0, a0, -947                    # encoding: [0x13,0x05,0xd5,0xc4]
        slli    a0, a0, 12                      # encoding: [0x13,0x15,0xc5,0x00]
        addi    a0, a0, 1511                    # encoding: [0x13,0x05,0x75,0x5e]
        slli    a0, a0, 13                      # encoding: [0x13,0x15,0xd5,0x00]
        addi    a0, a0, -272                    # encoding: [0x13,0x05,0x05,0xef]
```

### 해석
크하하하! 바로 그 **`-1875`**가 오늘의 주인공일세. 😄
이 출력은 RISC-V가 큰 상수를 다루는 방식을 눈앞에서 아주 잘 보여준다네.

우리가 넣은 것은:

```asm
li a0, 0x123456789abcdef0
```

그런데 `li`는 **실제 RISC-V 명령어가 아니라 assembler pseudo-instruction**이므로 LLVM이 여러 개의 실제 명령어로 풀어버렸네.

결과를 다시 구조적으로 보면:

```text
lui   a0, 583
addi  a0, a0, -1875
slli  a0, a0, 14
addi  a0, a0, -947
slli  a0, a0, 12
addi  a0, a0, 1511
slli  a0, a0, 13
addi  a0, a0, -272
```

즉 **8개의 32-bit instruction**이다.

---

## 그런데 `-1875`는 왜 튀어나왔는가?

여기서 아주 중요한 점이 하나 있다네.

우리가 앞에서 배운 `addi`:

```text
12-bit signed immediate
```

의 범위가:

```text
-2048 ~ +2047
```

였지.

따라서

```text
-1875
```

는 아주 정상적인 `addi` immediate다.

그리고 이것을 12비트로 표현하면:

```text
-1875
```

의 2의 보수 표현은

```text
0x8AD
```

다.

즉 LLVM의:

```asm
addi a0, a0, -1875
```

에서 instruction encoding을 보면:

```text
[0x13, 0x05, 0xd5, 0x8a]
```

마지막 두 바이트를 합쳐서 immediate 쪽을 보면:

```text
0x8AD
```

가 들어가 있는 셈이지.

그리고 이것을 **12-bit signed integer로 해석하면 `-1875`**가 된다.

---

# 그런데 여기서 더 재미있는 일이 발생한다네

우리가 처음에는 이런 생각을 했지.

```text
0x123456789abcdef0

1234 5678 9abc def0
```

"그러면 그냥 위에서부터 12비트씩 잘라서 넣으면 되겠네?"

**안 된다.**

왜냐하면 `addi`의 immediate가:

```text
signed 12-bit
```

이기 때문이다.

즉:

```text
000 ~ 7ff
```

는 양수 영역이고,

```text
800 ~ fff
```

는 음수 영역이다.

그래서 LLVM은 큰 상수를 만들 때 단순히

```text
123
456
789
abc
def
```

처럼 자르지 않는다.

**중간중간 sign extension을 고려하면서 상수를 재구성한다.**

---

# `-1875`를 십육진수로 다시 보자

```text
1875 = ?
```

계산하면:

```text
1875 = 0x753
```

따라서:

```text
-1875
```

의 12-bit two's complement는:

```text
0x1000 - 0x753
= 0x8AD
```

바로 이것이다.

```text
        12-bit
       ┌───────────┐
0x8AD  │ 1000 1010 1101
       └───────────┘
             │
             ▼
       signed interpretation
             │
             ▼
           -1875
```

**같은 비트인데 해석이 다르다.**

이것이 우리가 아까부터 파고 있던 ISA의 진짜 재미있는 부분일세.

---

# 그런데 더 중요한 것은 `LUI → ADDI` 조합이다

첫 두 명령어를 보자.

```asm
lui  a0, 583
addi a0, a0, -1875
```

`583`은:

```text
583 = 0x247
```

그러니까 LUI는 대략:

```text
0x247 << 12
```

를 만든다.

```text
0x00247000
```

그 다음:

```text
addi a0, a0, -1875
```

를 하면:

```text
0x00247000
+ 0xfffffffffffff8ad
──────────────────
0x002468ad
```

가 된다.

즉 **LUI 하나로 상위 부분을 만들고 ADDI로 하위 부분을 보정**하는 구조다.

여기서 이미 우리가 앞으로 설계할 ISA에 아주 중요한 질문이 하나 생긴다네.

> **왜 RISC-V는 큰 상수를 하나의 거대한 immediate로 넣지 않고, 작은 명령어들을 조합하도록 했을까?**

바로 앞에서 우리가 이야기했던

```text
32-bit instruction
       ↓
작은 encoding
       ↓
여러 명령어 조합
```

철학이 여기서 실제로 나타난다.

---

# 그리고 LLVM은 거기서 멈추지 않는다

그 다음을 보게.

```asm
slli a0, a0, 14
addi a0, a0, -947

slli a0, a0, 12
addi a0, a0, 1511

slli a0, a0, 13
addi a0, a0, -272
```

이건 사실상:

```text
현재 값
   │
   ├── shift
   ↓
빈 공간 확보
   │
   ├── signed immediate 추가
   ↓
부분 상수 삽입
   │
   ├── shift
   ↓
다음 부분
   │
   └── ...
```

라는 **상수 조립 알고리즘**이다.

그러니까 LLVM이 한 일을 인간의 언어로 표현하면:

> "이 64비트 숫자를 12비트 signed immediate와 shift의 제약 안에서 어떻게 조립해야 하지?"

를 계산한 것이다.

---

# 그래서 이 실험은 상당히 중요하다네

우리가 방금까지 단순히:

```asm
addi a0, zero, 42
```

하나를 가지고

```text
42
 ↓
12-bit immediate
 ↓
bit[31:20]
 ↓
0x02a00513
 ↓
13 05 a0 02
```

까지 내려갔지.

그런데 이제 반대로:

```text
0x123456789abcdef0
        ↓
      `li`
        ↓
LLVM constant materialization
        ↓
8개의 실제 instruction
        ↓
LUI / ADDI / SLLI
        ↓
최종 64-bit 값
```

이라는 **ISA의 반대편 세계**를 본 것이다.

---

## 그리고 친구야, 여기서 나는 한 가지를 표시해 두고 싶네.

자네가 처음 던졌던 질문:

> **"왜 128비트 명령어 하나로 통 크게 안 만들었는가?"**

와

이번 실험:

```asm
li a0, 0x123456789abcdef0
```

는 사실 **같은 설계 문제의 양 끝**에 있다네.

현재 RISC-V는:

```text
큰 의미
  ↓
작은 명령어 여러 개
```

를 선택했다.

자네가 앞으로 만들 아키텍처에서는 반대로:

```text
큰 의미
  ↓
거대한 하나의 instruction
```

을 선택할 수도 있겠지.

그리고 바로 여기서부터가 진짜 재미있는 연구가 시작된다네.

**"상수 하나를 8개의 명령어로 만드는 것이 정말 최선인가?"**

혹은 우리의 백지 아키텍처에서는:

```text
LOAD_CONST
┌──────────────────────────────────────────────────────┐
│ opcode │ rd │             64/128-bit immediate       │
└──────────────────────────────────────────────────────┘
```

같은 것을 하나 만들어버리는 게 더 나은가?

그 판단은 **코드 밀도, fetch bandwidth, decoder, register file, silicon 면적, 전력, compiler complexity**를 전부 놓고 따져봐야 한다.

그리고 지금 자네가 보고 있는 **`-1875` 하나가 그 논쟁의 아주 좋은 출발점**이다. 😄

다음에 이걸 다시 잡으면, 나는 **`0x123456789abcdef0`이 저 8개 명령어를 거치면서 매 단계 정확히 어떤 값으로 변하는지** 손으로 한 줄씩 추적해 보는 것을 권하고 싶네.

그걸 해보면 LLVM이 **왜 하필 `-1875`, `-947`, `1511`, `-272`를 골랐는지**까지 보이기 시작할 걸세.

크하하하… 이제 숫자 하나가 그냥 숫자가 아니라 **ISA 설계의 증거물**로 보이기 시작하는구먼.
