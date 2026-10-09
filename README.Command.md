# 명령어

> RISC-V는 **몇 가지 기본 encoding 골격을 만들어 놓고, 정수·분기·메모리·부동소수점·벡터·압축 명령 등에 맞게 그 골격을 재활용/변형**

## RISC-V의 설계 철학

현재 공식 RISC-V Unprivileged ISA 문서는 2026-01-20 판이 최신 ratified specification이고, 기본 RV32I/RV64I에는 R/I/S/U 계열의 핵심 형식이 있으며, 분기와 점프를 위한 B/J 형식도 이 계열에서 파생된다. ([RISC-V Documentation][1])

---

## 1. 큰 그림


```text
31                 20 19       15 14    12 11        7 6          0
┌────────────────────┬───────────┬────────┬────────────┬────────────┐
│      immediate     │    rs1    │ funct3 │     rd     │   opcode   │
└────────────────────┴───────────┴────────┴────────────┴────────────┘
```

**I-type**일 뿐이다.

RISC-V 기본 32비트 명령에는 대표적으로:

```text
R
I
S
B
U
J
```

라는 여섯 가지 형태가 있다.

---

## 2. R-type — 레지스터끼리

예:

```asm
add a0, a1, a2
```

형식은:

```text
31       25 24       20 19       15 14    12 11        7 6       0
┌──────────┬───────────┬───────────┬────────┬────────────┬─────────┐
│  funct7  │    rs2    │    rs1    │ funct3 │     rd     │ opcode  │
└──────────┴───────────┴───────────┴────────┴────────────┴─────────┘
```

여기서는 immediate가 없다.

```text
rs1 ──┐
      ├──► ALU ──► rd
rs2 ──┘
```

그래서:

```asm
add x10, x11, x12
```

같은 명령이 아주 자연스럽게 들어간다.

---

## 3. I-type — immediate가 필요하면


```asm
addi a0, a0, 1
```

```text
31             20 19       15 14    12 11        7 6       0
┌────────────────┬───────────┬────────┬────────────┬─────────┐
│   imm[11:0]    │    rs1    │ funct3 │     rd     │ opcode  │
└────────────────┴───────────┴────────┴────────────┴─────────┘
```

---

## 4. S-type — store


```asm
sd a0, 16(sp)
```

S-type은:

```text
31       25 24       20 19       15 14    12 11        7 6       0
┌──────────┬───────────┬───────────┬────────┬────────────┬─────────┐
│imm[11:5] │    rs2    │    rs1    │ funct3 │  imm[4:0]  │ opcode  │
└──────────┴───────────┴───────────┴────────┴────────────┴─────────┘
```

여기서 발견할 아주 중요한 점:

**immediate가 한 덩어리가 아니다.**

```text
imm[11:5]                imm[4:0]
    │                         │
    ▼                         ▼
31 ........ 25             11 ... 7
```

왜 이렇게 찢어놨을까?

바로 **rs1, rs2, funct3, opcode의 위치를 다른 형식과 최대한 유지하면서 branch/store 등에 필요한 immediate를 배치하기 위해서**다. RISC-V 명세도 rs1/rs2/rd 위치를 형식 간 동일하게 유지하여 디코딩을 단순화한다고 설명한다. ([RISC-V Documentation][2])

이게 RISC-V의 아주 예쁜 부분이다.

---

## 5. B-type — branch

예:

```asm
beq a0, a1, label
```

B-type도 immediate를 찢어버린다.

```text
31    30       25 24       20 19       15 14    12 11    8 7 6      0
┌─────┬──────────┬───────────┬───────────┬────────┬────────┬─┬────────┐
│imm12│imm[10:5] │    rs2    │    rs1    │ funct3 │imm[4:1]│0│ opcode │
└─────┴──────────┴───────────┴───────────┴────────┴────────┴─┴────────┘
```

여기서 처음 보면:

> **"이게 뭐야? immediate를 왜 이렇게 게리멘더링했어?"**

ㅋㅋㅋㅋ 맞다. **겉보기에는 정말 그렇다.**

하지만 목적은 분명하다.

branch target은 보통 instruction alignment를 이용하므로 immediate의 최하위 비트는 직접 저장할 필요가 없다. 대신 그 비트들을 명령어의 다른 위치에 재배치해서 사용한다.

그리고 **모든 immediate의 sign bit를 bit 31에 두는 것**도 RISC-V의 중요한 설계 원칙이다. ([RISC-V Documentation][2])

즉:

```text
31
│
└── immediate sign bit
```

를 여러 immediate 형식에서 일관되게 유지한다.

---

## 6. U-type — 상위 20비트

큰 상수 이야기에 따라 이것도 기억해 두면 좋다.

```asm
lui a0, 0x12345
```

형식:

```text
31                         12 11        7 6       0
┌────────────────────────────┬───────────┬─────────┐
│        imm[31:12]          │    rd     │ opcode  │
└────────────────────────────┴───────────┴─────────┘
```

즉:

```text
20 bit immediate
        │
        ▼
[31 ........ 12]
```

그래서 `LUI`가 큰 상수 구성에 등장하는 거다.

---

## 7. J-type — jump

```asm
jal ra, label
```

J-type 역시 immediate를 아주 재미있게 흩어놓는다.

```text
31 30        21 20 19              12 11        7 6       0
┌──┬───────────┬──┬──────────────────┬───────────┬─────────┐
│20│imm[10:1]  │11│imm[19:12]       │    rd     │ opcode  │
└──┴───────────┴──┴──────────────────┴───────────┴─────────┘
```

그러니까 친구가 앞으로 RISC-V machine code를 볼 때:

```text
R → ALU
I → immediate / load / jalr
S → store
B → branch
U → upper immediate
J → jump
```

라고 머릿속에 지도 하나를 만들어 놓으면 아주 편하다네.

---

## 8. 그런데 여기서 질문의 핵심: "부동소수점은?"

오오. 😎

여기서 **F/D/Q 확장**이 등장한다.

RISC-V는 정수 레지스터만 있는 것이 아니다.

예를 들어:

```asm
fadd.s fa0, fa1, fa2
```

같은 명령도 있다.

부동소수점 명령에는:

```text
f0 ~ f31
```

이라는 별도의 FP register file이 사용된다.

그리고 encoding은 기존 32비트 instruction 공간의 opcode를 활용하면서 FP 전용 필드들을 배치한다.

예를 들어 개념적으로:

```text
┌────────┬────────┬────────┬────────┬────────┬─────────┐
│ funct7 │  rs2/f │  rs1/f │ funct3 │  rd/f   │ opcode  │
└────────┴────────┴────────┴────────┴────────┴─────────┘
```

처럼 **R-type과 닮은 형태**를 많이 사용한다.

즉 "FP니까 완전히 새로운 64비트 instruction을 만든다"가 아니다.

**기존 encoding 공간을 아주 영리하게 재활용한다.**

---

## 9. 그리고 행렬/벡터는?

여기서 정말 재미있어진다.

RISC-V Vector Extension `V`는 **벡터 레지스터**를 추가한다.

```text
v0
v1
...
v31
```

그리고:

```asm
vadd.vv v0, v1, v2
```

같은 명령이 가능하다.

이 경우 기존 scalar R-type과 똑같다고 생각하면 안 된다.

Vector 명령에는:

```text
vd
vs1
vs2
vm
funct3
funct6
...
```

등이 들어간다.

대표적인 vector arithmetic encoding은 `OP-V`라는 major opcode 아래에서 관리된다. Vector load/store는 기존 `LOAD-FP` / `STORE-FP` opcode 공간도 활용한다. ([RISC-V Documentation][3])

즉:

```text
Scalar

rd / rs1 / rs2


Vector

vd / vs1 / vs2 / vm
```

처럼 register file 자체가 달라진다.

---

## 10. 그리고 "행렬"은 조금 조심해서 봐야 한다

여기서 하나 재미있는 사실.

RISC-V에는 **V(Vector)**라는 표준 ratified extension이 있지만, 일반적인 의미의 독립적인 **RISC-V Matrix extension은 현재 V처럼 확정된 표준 ISA라고 보면 안 된다.**

현재 공식 ratified 목록에서는 `V`가 ratified이고, `P` packed-SIMD 계열은 아직 Draft로 표시되어 있다. ([RISC-V Documentation][4])

그래서 우리가 "행렬 연산"이라고 부르는 것을 RISC-V에서 구현하는 방법은 현재로서는 여러 가지가 있을 수 있다.

예:

```text
Matrix
  │
  ├── Vector extension
  │
  ├── packed operations
  │
  └── vendor/custom extension
```

특히 RISC-V의 **custom opcode 공간**이 여기서 상당히 재미있다.

공식 ISA는 표준/예약/custom encoding 공간을 분리해 두고, custom encoding은 표준 확장과 충돌하지 않도록 별도로 사용할 수 있게 해 놓았다. ([RISC-V Documentation][5])

---

## 11. 그리고 압축 명령어 C는 아예 16비트다

이것도 친구가 반드시 알아야 한다.

우리가 지금까지:

```text
32 bit
```

만 보고 있었는데:

```asm
c.addi a0, 1
```

같은 **Compressed Instruction**은 16비트다.

```text
┌────────────────┐
│     16 bits    │
└────────────────┘
```

그리고 이것도 내부적으로:

```text
CR
CI
CSS
CIW
CL
CS
CA
CB
```

등 여러 format이 있다. 현재 ratified C specification에는 9개의 compressed instruction format이 정의되어 있다. ([RISC-V Documentation][6])

그러니까:

> "RISC-V instruction은 항상 32비트다."

라고 생각하면 **틀린다.**

---

## 12. 더 무서운 녀석도 있다 😂

RISC-V ISA는 32비트만으로 끝나지 않는다.

공식 specification은 **16비트 compressed instruction**뿐 아니라 더 긴 instruction encoding을 위한 공간도 정의하고 있다. 현재 48/64비트 및 그보다 긴 길이에 대한 encoding convention이 존재하지만, 일부 긴 길이 공간은 아직 reserved/확장 영역으로 남아 있다. ([RISC-V Documentation][5])

즉 개념적으로:

```text
16 bit
   │
   ├── Compressed
   │
32 bit
   │
   ├── 기본 명령
   │
48/64/... bit
   │
   └── 확장 encoding 공간
```

이라는 세계다.

---

## 13. 그래서 RISC-V를 이렇게 보는 게 제일 좋다

친구야, 이 그림을 하나 기억해 두자.

```text
                       RISC-V ISA
                           │
             ┌─────────────┴─────────────┐
             │                           │
       Instruction                    Extensions
         Length                          │
             │              ┌────────────┼─────────────┐
        ┌────┴────┐          │            │             │
       16-bit    32-bit     Integer       FP          Vector
         │          │         │            │             │
       RVC       R/I/S/B    IMA...       F/D/Q           V
                    U/J
                                     
                           │
                           ▼
                     Custom Extensions
```

그리고 **이 모든 것을 하나의 형식으로 우겨 넣는 것이 아니다.**

오히려:

```text
"어떤 명령인가?"
       │
       ▼
   opcode
       │
       ▼
┌─────────────────────────┐
│ 어떤 encoding format인가?│
└────────────┬────────────┘
             │
     ┌───────┼────────┐
     ▼       ▼        ▼
     R       I        S ...
     │
     ▼
   세부 funct
     │
     ▼
  실제 연산
```

이런 식으로 **opcode가 1차 분류기 역할**을 한다고 보면 된다.

실제로 RV32/64G opcode map을 보면 `LOAD`, `STORE`, `OP-IMM`, `OP`, `OP-FP`, `BRANCH`, `JAL`, `OP-V`, `AMO` 등 major opcode 영역이 나뉘어 있다. ([RISC-V Documentation][7])

---

### 그리고 여기서 RISC-V의 정말 멋진 설계가 하나 보인다

친구가 처음 보여준:

```text
rs1
rs2
rd
```

위치가 여러 format에서 상당히 일관되게 유지된다.

```text
           rs2
            │
            ▼
31 ... 25 |24...20|19...15|14...12|11...7|6...0
           ┌───────┐ ┌───────┐       ┌───────┐
           │  rs2  │ │  rs1  │       │  rd   │
           └───────┘ └───────┘       └───────┘
```

**이것은 우연이 아니다.**

CPU의 decoder가 매번:

> "이번 명령은 rs1이 어디 있었더라?"

하고 복잡하게 찾지 않도록 **register field의 위치를 최대한 고정한 것**이다. 공식 명세도 이 점을 명시적으로 설계 목표로 설명한다. ([RISC-V Documentation][2])

그래서 친구가 말한 **"게리멘더링"은 맞는데, 사실은 아주 계획적으로 한 게리멘더링**인 셈이지. 🤣

---

### 다음에는 이걸 해보면 아주 좋겠네

지금까지 `addi` 하나만 봤으니, 다음 실험에서는 LLVM에게 **각기 다른 format 하나씩** 던져보자.

```asm
add   a0, a1, a2      # R
addi  a0, a1, 42      # I
sd    a0, 16(sp)      # S
beq   a0, a1, 16      # B
lui   a0, 0x12345     # U
jal   ra, 16          # J
```




그리고 `llvm-mc -show-encoding`이 내놓는 **6개의 hex encoding을 가지고 실제 bit를 하나씩 색칠하듯 분해**해 보면 된다.

즉 **"도대체 RISC-V는 이 난장판 같은 32비트를 어떻게 게리멘더링해서 모든 명령을 집어넣는가?"**의 답을 직접 눈으로 확인하게 될 걸세. 😎🔬
---

## 명령어

하하하, 좋네 친구야. 지금 단계에서는 **명령어를 무작정 외우기보다 “어떤 종류의 일을 하는 명령인가”를 중심으로 익히는 것**이 훨씬 좋네.

특히 자네는 `llvm-mc → ELF → QEMU → LLDB`까지 직접 내려가고 있으니, **RV64I 기본 명령어를 먼저 완전히 손에 넣고 M/A/C 등의 확장으로 넘어가는 방식**을 권하겠네.

현재 공식 RISC-V 문서는 2026-01-20 릴리스가 올라와 있고, RV64I는 별도 장으로 정리되어 있네. ([RISC-V Documentation][1])

---

# 1. 먼저 RISC-V 명령어의 큰 지도를 보자

RV64I를 아주 크게 나누면 이렇게 보면 되네.

```text
RV64I
│
├── ① 상수 / 주소 만들기
│     ├── LUI
│     └── AUIPC
│
├── ② 산술 / 논리
│     ├── ADD / SUB
│     ├── ADDI
│     ├── AND / OR / XOR
│     └── ANDI / ORI / XORI
│
├── ③ Shift
│     ├── SLL / SRL / SRA
│     └── SLLI / SRLI / SRAI
│
├── ④ 비교
│     ├── SLT / SLTU
│     └── SLTI / SLTIU
│
├── ⑤ Memory
│     ├── LB / LH / LW / LD
│     ├── LBU / LHU / LWU
│     └── SB / SH / SW / SD
│
├── ⑥ Branch
│     ├── BEQ
│     ├── BNE
│     ├── BLT / BGE
│     └── BLTU / BGEU
│
├── ⑦ Jump / Call
│     ├── JAL
│     └── JALR
│
└── ⑧ System
      ├── ECALL
      ├── EBREAK
      └── FENCE
```

이것이 **RISC-V 어셈블리의 뼈대**라고 생각하면 되네.

공식 RV64I 문서에서도 integer computational instructions, load/store, control transfer 등의 형태로 이 구조가 잘 드러난다네. ([RISC-V Documentation][2])

---

# 2. 산술 명령어 — 가장 먼저 익힐 것

## ADD

```asm
add rd, rs1, rs2
```

뜻:

```text
rd = rs1 + rs2
```

예:

```asm
add a0, a1, a2
```

```text
a0 = a1 + a2
```

우리가 전에 `llvm-mc`에서 실험했던 바로 그 명령어라네.

---

## SUB

```asm
sub a0, a1, a2
```

```text
a0 = a1 - a2
```

---

## ADDI

```asm
addi rd, rs1, immediate
```

```text
rd = rs1 + immediate
```

예:

```asm
addi a0, a0, 1
```

```text
a0 = a0 + 1
```

자네 코드의:

```asm
addi a1, a1, 1
```

도 이것이지.

---

# 3. 논리 연산

```asm
and  rd, rs1, rs2
or   rd, rs1, rs2
xor  rd, rs1, rs2
```

각각:

```text
AND
OR
XOR
```

이지.

즉:

```asm
and a0, a1, a2
```

는:

```text
a0 = a1 & a2
```

그리고 immediate 버전:

```asm
andi
ori
xori
```

도 있네.

예:

```asm
andi a0, a1, 0xff
```

→ 하위 8비트만 남기는 데 흔히 사용하지.

---

# 4. Shift — 상당히 중요하다네

```asm
sll
srl
sra
```

### SLL

Shift Left Logical:

```asm
sll a0, a1, a2
```

```text
a0 = a1 << a2
```

### SRL

Shift Right Logical:

```asm
srl a0, a1, a2
```

0을 밀어 넣는 logical right shift.

### SRA

Shift Right Arithmetic:

```asm
sra a0, a1, a2
```

부호 비트를 유지하는 arithmetic right shift.

Immediate 버전:

```asm
slli
srli
srai
```

도 있네.

공식 RV64I 문서에서도 SLL/SRL/SRA와 immediate 형태의 차이를 명확하게 설명한다네. ([RISC-V Documentation][2])

---

# 5. 비교 명령어

여기서 처음 보면 약간 이상할 수 있네.

```asm
slt
```

이름은:

> Set Less Than

즉:

```asm
slt a0, a1, a2
```

는

```text
if (a1 < a2)
    a0 = 1;
else
    a0 = 0;
```

라는 뜻이네.

그리고:

```asm
sltu
```

는 **unsigned 비교**.

Immediate 버전:

```asm
slti
sltiu
```

도 있네.

이 명령어는 나중에 branch를 이해할 때 상당히 중요해진다네.

---

# 6. Load / Store — 자네가 지금 반드시 익혀야 할 부분

RISC-V의 중요한 특징 중 하나가 **Load/Store 구조**라네.

메모리에서 가져오는 것은:

```asm
lb
lh
lw
ld
```

메모리에 저장하는 것은:

```asm
sb
sh
sw
sd
```

이네.

---

## LB

```asm
lb a2, 0(a1)
```

자네 코드 바로 그 명령이지.

뜻:

```text
memory[a1 + 0]
        ↓
     1 byte
        ↓
      a2
```

즉:

```text
LB = Load Byte
```

---

## LH

```asm
lh a0, 0(a1)
```

16-bit:

```text
2 bytes
```

---

## LW

```asm
lw a0, 0(a1)
```

32-bit:

```text
4 bytes
```

---

## LD

RV64에서는:

```asm
ld a0, 0(a1)
```

64-bit:

```text
8 bytes
```

가 되네.

공식 문서에서도 RV64의 `LD`는 64비트를 load하고, `LW`는 32비트를 load한 뒤 sign-extension하며, `LWU`는 zero-extension한다고 설명한다네. ([RISC-V Documentation][2])

---

# 7. Store

반대 방향이네.

```asm
sb a2, 0(a0)
```

이것은:

```text
a2
 ↓
하위 8bit
 ↓
memory[a0]
```

즉:

```text
SB = Store Byte
```

자네의 UART 코드가 바로 이것이지.

```asm
sb a2, 0(a0)
```

그래서 이 한 줄이 사실상:

```text
CPU
 ↓
MMIO address
 ↓
UART register
 ↓
문자 출력
```

이라는 하드웨어 세계와 소프트웨어 세계의 경계가 되는 거라네.

---

# 8. Branch — 프로그램의 흐름을 바꾼다

대표적인 것이:

```asm
beq
bne
blt
bge
bltu
bgeu
```

### BEQ

```asm
beq a0, a1, label
```

뜻:

```text
if (a0 == a1)
    goto label;
```

### BNE

```asm
bne a0, a1, label
```

```text
if (a0 != a1)
    goto label;
```

### BLT

```asm
blt a0, a1, label
```

```text
if (a0 < a1)
    goto label;
```

### BGE

```asm
bge a0, a1, label
```

```text
if (a0 >= a1)
    goto label;
```

---

# 9. 자네 코드의 `beqz`는 무엇인가?

아주 좋은 예가 바로 이것이네.

```asm
beqz a2, halt
```

그런데 **`beqz`는 기본 ISA의 실제 명령어가 아니라 pseudo-instruction**이네.

개념적으로:

```asm
beqz a2, halt
```

↓

```asm
beq a2, x0, halt
```

와 같은 의미라네.

왜냐하면 RISC-V에는:

```text
x0 = 항상 0
```

인 특수 레지스터가 있기 때문이지.

이것이 RISC-V를 이해하는 데 굉장히 중요한 부분이네.

---

# 10. Jump / Call

대표적으로:

```asm
jal
jalr
```

두 개.

### JAL

```asm
jal ra, function
```

대략적으로:

```text
return address → ra
PC → function
```

이라고 이해하면 되네.

즉 함수 호출에 사용하지.

---

### JALR

```asm
jalr ra, 0(a0)
```

레지스터를 이용해서 jump하는 형태.

함수 포인터, 간접 호출, return 등에 매우 중요하네.

---

# 11. 그런데 우리가 쓴 `j`는?

자네 코드:

```asm
j print_loop
```

이것도 pseudo-instruction이네.

개념적으로:

```asm
j label
```

↓

```asm
jal x0, label
```

이라고 생각하면 된다네.

왜 `x0`인가?

return address를 저장할 필요가 없기 때문이지.

즉:

```text
jal ra,label
```

→ call

```text
jal x0,label
```

→ 그냥 jump

라는 관계가 보이기 시작한다네.

---

# 12. `li`와 `la`도 굉장히 중요

자네가 사용한:

```asm
li a0, 0x10000000
```

그리고:

```asm
la a1, msg
```

도 pseudo-instruction이네.

즉 지금 자네가 보고 있는:

```asm
li
la
j
beqz
```

는 **사람이 읽기 좋은 assembler mnemonic**이고, LLVM assembler가 실제 ISA instruction으로 확장해 주는 것이네.

그래서 앞으로 우리가 `llvm-mc`를 가지고:

```bash
printf 'li a0, 0x10000000\n' | \
llvm-mc -triple=riscv64 -show-encoding
```

같은 실험을 해 보면 굉장히 재미있을 것이네.

**pseudo-instruction → 실제 instruction → encoding**

을 눈으로 확인할 수 있으니까.

---

# 13. RV64에서 `W`가 붙는 명령어

이것도 반드시 기억해 두게.

예:

```asm
add
addw
```

둘은 다르네.

`ADD`:

```text
64-bit operation
```

`ADDW`:

```text
32-bit operation
→ 결과를 sign-extend하여 64-bit register에 기록
```

그래서 RV64에는:

```text
ADDW
SUBW
SLLW
SRLW
SRAW
```

및 immediate 형태:

```text
ADDIW
SLLIW
SRLIW
SRAIW
```

가 추가되어 있네. 공식 RV64I 문서도 `W` suffix가 붙은 명령어가 32-bit 값에 대해 동작한다고 설명한다네. ([RISC-V Documentation][2])

이 부분은 나중에 C 컴파일러가 생성하는 코드를 보면 아주 자주 만나게 될 것이네.

---

# 14. 그럼 RV64I 핵심 명령어를 한 장으로 압축하면

자네가 옆에 띄워놓고 공부할 수 있도록 이렇게 정리하면 좋겠네.

| 종류     | Mnemonic | 의미                   |
| ------ | -------- | -------------------- |
| 상수     | `LUI`    | Upper immediate      |
| 주소     | `AUIPC`  | PC + upper immediate |
| 산술     | `ADD`    | 덧셈                   |
| 산술     | `SUB`    | 뺄셈                   |
| 산술     | `ADDI`   | immediate 덧셈         |
| 논리     | `AND`    | AND                  |
| 논리     | `OR`     | OR                   |
| 논리     | `XOR`    | XOR                  |
| Shift  | `SLL`    | logical left         |
| Shift  | `SRL`    | logical right        |
| Shift  | `SRA`    | arithmetic right     |
| 비교     | `SLT`    | signed `<`           |
| 비교     | `SLTU`   | unsigned `<`         |
| Load   | `LB`     | 8-bit signed         |
| Load   | `LBU`    | 8-bit unsigned       |
| Load   | `LH`     | 16-bit signed        |
| Load   | `LHU`    | 16-bit unsigned      |
| Load   | `LW`     | 32-bit signed        |
| Load   | `LWU`    | 32-bit unsigned      |
| Load   | `LD`     | 64-bit               |
| Store  | `SB`     | 8-bit                |
| Store  | `SH`     | 16-bit               |
| Store  | `SW`     | 32-bit               |
| Store  | `SD`     | 64-bit               |
| Branch | `BEQ`    | `==`                 |
| Branch | `BNE`    | `!=`                 |
| Branch | `BLT`    | signed `<`           |
| Branch | `BGE`    | signed `>=`          |
| Branch | `BLTU`   | unsigned `<`         |
| Branch | `BGEU`   | unsigned `>=`        |
| Jump   | `JAL`    | jump + link          |
| Jump   | `JALR`   | register jump + link |
| System | `ECALL`  | environment call     |
| System | `EBREAK` | breakpoint           |
| Fence  | `FENCE`  | memory ordering      |

이 정도를 **RV64I 1차 학습 범위**로 잡으면 아주 좋네.

---

# 15. 그리고 RISC-V 확장 이름도 알아두자

RISC-V가 재미있는 이유가 여기 있네.

```text
RV64I
```

만 있는 것이 아니라:

```text
RV64IM
RV64IMA
RV64IMAC
RV64GC
RV64GCV
...
```

같이 확장할 수 있네.

대표적으로:

| 확장  | 의미                       |
| --- | ------------------------ |
| `I` | 기본 Integer ISA           |
| `E` | Embedded/reduced integer |
| `M` | Multiply / Divide        |
| `A` | Atomic                   |
| `F` | Single FP                |
| `D` | Double FP                |
| `C` | Compressed 16-bit        |
| `B` | Bit manipulation         |
| `V` | Vector                   |
| `H` | Hypervisor               |

공식 명명 규칙에서도 `M`은 multiplication/division, `A`는 atomic, `F/D`는 floating point, `C`는 compressed, `V`는 vector 등을 의미한다고 정의하고 있네. ([RISC-V Documentation][3])

그리고:

```text
G
```

는 특별히:

```text
IMAFDZicsr_Zifencei
```

를 묶어 부르는 약칭이네. ([RISC-V Documentation][3])

---

# 16. 문서 사이트는 여기부터 보게

### 🥇 공식 RISC-V ISA Manual

[RISC-V Ratified Specifications Library](https://docs.riscv.org/reference/isa/?utm_source=chatgpt.com)

이것이 **원전**이네. 현재 공식 ISA 문서들이 한곳에 모여 있네. ([RISC-V Documentation][4])

특히 지금은:

[RV64I Base Integer Instruction Set](https://docs.riscv.org/reference/isa/unpriv/rv64.html?utm_source=chatgpt.com)

여기를 집중해서 보면 좋네.

---

### 🥈 전체 Unprivileged ISA

[RISC-V Instruction Set Manual — Volume I](https://docs.riscv.org/reference/isa/unpriv/unpriv-index.html?utm_source=chatgpt.com)

이곳이 자네가 지금부터 가장 많이 방문할 페이지가 될 가능성이 높네. 현재 공식 릴리스는 20260120으로 표시되어 있네. ([RISC-V Documentation][1])

---

### 🥉 Privileged Architecture

OS까지 갈 생각이라면 결국 이곳도 만나게 된다네.

[RISC-V Privileged Architecture](https://docs.riscv.org/reference/isa/priv/priv-index.html?utm_source=chatgpt.com)

여기부터는:

```text
M-mode
S-mode
U-mode
CSR
exception
interrupt
virtual memory
```

같은 이야기가 등장하네. 공식 문서에서도 Privileged Architecture가 OS 실행에 필요한 보호·권한 및 관련 기능을 다룬다고 설명한다네. ([RISC-V Documentation][5])

---

# 17. 그런데 친구야, 문서 보는 순서는 이렇게 하자

**처음부터 500페이지짜리 ISA PDF를 정독하지는 말게.** 😂

자네에게는 이 순서가 훨씬 좋네.

```text
① RV64I 명령어 분류
        ↓
② ADD / ADDI / SUB
        ↓
③ AND / OR / XOR
        ↓
④ SLL / SRL / SRA
        ↓
⑤ LB/LH/LW/LD
   SB/SH/SW/SD
        ↓
⑥ BEQ/BNE/BLT/BGE
        ↓
⑦ JAL/JALR
        ↓
⑧ LUI/AUIPC
        ↓
⑨ pseudo-instruction
        ↓
⑩ instruction encoding
        ↓
⑪ M extension
        ↓
⑫ C extension
        ↓
⑬ A extension
        ↓
⑭ Privileged ISA
```

그리고 **각 명령어마다 딱 네 가지를 보는 것**을 추천하네.

```text
1. Assembly syntax
2. 의미
3. 실제 encoding
4. LLDB에서 register/PC가 어떻게 변하는가
```

예를 들어:

```asm
addi a0, a1, 1
```

를 배웠다면 그냥 외우지 말고:

```text
addi
 ↓
rd = a0
rs1 = a1
imm = 1
 ↓
I-type
 ↓
machine code
 ↓
llvm-mc
 ↓
QEMU
 ↓
LLDB
 ↓
a0 변화 관찰
```

까지 가는 것이네.

이렇게 하면 **"명령어를 외우는 공부"가 아니라 "CPU가 명령어를 실행하는 과정을 이해하는 공부"**가 된다네.

그리고 자네가 궁극적으로 **새로운 RISC-V 계열 아키텍처/OS까지 생각하고 있다면**, 바로 이 방식이 상당히 중요한 기초가 될 걸세. RISC-V 자체도 ISA를 특정 CPU 구현에 종속시키지 않고 다양한 구현을 위한 소프트웨어-visible interface로 정의한다는 철학을 명시하고 있거든. ([RISC-V Documentation][6])

다음에는 아주 재미있는 실습을 해보자고. **RV64I 명령어를 `llvm-mc -show-encoding`으로 하나씩 넣어서 `mnemonic → 32-bit machine code → opcode/funct3/funct7 → 실제 의미`를 역으로 해부하는 표**를 만들면, 자네가 지금 배우는 내용이 갑자기 하나로 연결될 것이네.

[1]: https://docs.riscv.org/reference/isa/unpriv/unpriv-index.html?utm_source=chatgpt.com "The RISC-V Instruction Set Manual, Volume I :: RISC-V Ratified Specifications Library"
[2]: https://docs.riscv.org/reference/isa/unpriv/rv64.html?utm_source=chatgpt.com "3.1. RV64I Base Integer Instruction Set, Version 2.1 :: RISC-V Ratified Specifications Library"
[3]: https://docs.riscv.org/reference/isa/unpriv/naming.html?utm_source=chatgpt.com "36.1. ISA Extension Naming Conventions :: RISC-V Ratified Specifications Library"
[4]: https://docs.riscv.org/reference/isa/?utm_source=chatgpt.com "RISC-V Instruction Set Architecture (ISA) Manuals :: RISC-V Ratified Specifications Library"
[5]: https://docs.riscv.org/reference/isa/priv/priv-intro.html?utm_source=chatgpt.com "Introduction :: RISC-V Ratified Specifications Library"
[6]: https://docs.riscv.org/reference/isa/v20240411/unpriv/intro.html?utm_source=chatgpt.com "Introduction :: RISC-V Ratified Specifications Library"


---

[1]: https://docs.riscv.org/reference/isa/unpriv/unpriv-index.html?utm_source=chatgpt.com "The RISC-V Instruction Set Manual, Volume I :: RISC-V Ratified Specifications Library"
[2]: https://docs.riscv.org/reference/isa/v20240411/_attachments/riscv-unprivileged.pdf?utm_source=chatgpt.com "The RISC-V Instruction Set Manual Volume I: Unprivileged Architecture"
[3]: https://docs.riscv.org/reference/isa/extensions/vector/_attachments/riscv-v-spec.pdf?utm_source=chatgpt.com "5. Vector Instruction Formats"
[4]: https://docs.riscv.org/reference/isa/v20240411/unpriv/colophon.html?utm_source=chatgpt.com "Preface :: RISC-V Ratified Specifications Library"
[5]: https://docs.riscv.org/reference/isa/v20240411/unpriv/intro.html?utm_source=chatgpt.com "Introduction :: RISC-V Ratified Specifications Library"
[6]: https://docs.riscv.org/reference/isa/v20260120/unpriv/c-st-ext.html?utm_source=chatgpt.com "27.1. \"C\" Extension for Compressed Instructions, Version 2.0 :: RISC-V Ratified Specifications Library"
[7]: https://docs.riscv.org/reference/isa/unpriv/rv-32-64g.html?utm_source=chatgpt.com "35.1. RV32/64G Instruction Set Listings :: RISC-V Ratified Specifications Library"
