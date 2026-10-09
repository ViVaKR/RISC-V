# Register

지금까지 우리가 **“명령어가 무엇을 하는가”**를 봤다면, 이제는 **“그 명령어가 사용하는 레지스터를 누가 보존해야 하는가”**를 이해해야 해야 함.

특히 `callee-saved`, `caller-saved`, `ra`, `sp`를 이해하면 **RISC-V 어셈블리 → 함수 호출 → C 컴파일러 → OS 커널**이 한꺼번에 연결되기 시작함. 

중요한 점 하나:

> **RISC-V ISA가 정하는 레지스터의 역할과, 함수 호출 때 지켜야 하는 ABI 규칙은 서로 다른 층이다.**

---

# 1. RV64 레지스터 전체 지도

RV64에서는 기본적으로 **32개의 정수 레지스터**, 각각 64비트가 있네.

```text
x0 ~ x31
```

그리고 사람이 읽기 좋은 ABI 이름이 따로 있네.

|  번호 | ABI 이름      | 역할                    | 보존 규칙        |
| --: | ----------- | --------------------- | ------------ |
|  x0 | `zero`      | 항상 0                  | —            |
|  x1 | `ra`        | Return Address        | caller-saved |
|  x2 | `sp`        | Stack Pointer         | callee-saved |
|  x3 | `gp`        | Global Pointer        | fixed        |
|  x4 | `tp`        | Thread Pointer        | fixed        |
|  x5 | `t0`        | Temporary             | caller-saved |
|  x6 | `t1`        | Temporary             | caller-saved |
|  x7 | `t2`        | Temporary             | caller-saved |
|  x8 | `s0` / `fp` | Saved / Frame Pointer | callee-saved |
|  x9 | `s1`        | Saved                 | callee-saved |
| x10 | `a0`        | Argument / Return     | caller-saved |
| x11 | `a1`        | Argument / Return     | caller-saved |
| x12 | `a2`        | Argument              | caller-saved |
| x13 | `a3`        | Argument              | caller-saved |
| x14 | `a4`        | Argument              | caller-saved |
| x15 | `a5`        | Argument              | caller-saved |
| x16 | `a6`        | Argument              | caller-saved |
| x17 | `a7`        | Argument              | caller-saved |
| x18 | `s2`        | Saved                 | callee-saved |
| x19 | `s3`        | Saved                 | callee-saved |
| x20 | `s4`        | Saved                 | callee-saved |
| x21 | `s5`        | Saved                 | callee-saved |
| x22 | `s6`        | Saved                 | callee-saved |
| x23 | `s7`        | Saved                 | callee-saved |
| x24 | `s8`        | Saved                 | callee-saved |
| x25 | `s9`        | Saved                 | callee-saved |
| x26 | `s10`       | Saved                 | callee-saved |
| x27 | `s11`       | Saved                 | callee-saved |
| x28 | `t3`        | Temporary             | caller-saved |
| x29 | `t4`        | Temporary             | caller-saved |
| x30 | `t5`        | Temporary             | caller-saved |
| x31 | `t6`        | Temporary             | caller-saved |

RISC-V의 공식 ABI 표에서도 이 매핑과 보존 규칙을 정의하고 있네.

---

# 2. 이것을 세 그룹으로 기억하면 아주 쉽네

32개를 전부 외우려 하지 말고 우선 이렇게 나누게.

```text
                RV64 Registers
                     │
       ┌─────────────┼─────────────┐
       │             │             │
       ▼             ▼             ▼
    Temporary     Argument       Saved
       │             │             │
   t0 ~ t6        a0 ~ a7       s0 ~ s11
       │             │             │
       └─────────────┴─────────────┘
                caller-saved
                              
                s0 ~ s11
                callee-saved
```

그리고 별도의 특별한 레지스터:

```text
zero
ra
sp
gp
tp
```

가 있다고 생각하면 된다네.

---

# 3. 가장 중요한 `caller-saved` vs `callee-saved`

여기서부터 함수 호출의 핵심일세.

### Caller-saved

```text
t0~t6
a0~a7
ra
```

를 대표적으로 생각하면 되네.

뜻은:

> **함수를 호출하는 쪽(caller)이 필요하면 알아서 보존해라.**

예를 들어:

```asm
caller:
    ...
    addi a0, zero, 123

    call foo

    # 여기서 a0가 123이라고 기대하면 안 됨
```

왜냐하면 `foo()`가:

```asm
foo:
    addi a0, zero, 999
    ret
```

할 수도 있기 때문이지.

따라서 caller가 `a0=123`을 계속 필요로 한다면:

```text
caller
  │
  ├── 123
  │
  ├── save somewhere
  │
  ├── call foo
  │
  └── restore
```

해야 하네.

---

# 4. 반대로 `callee-saved`

`s0 ~ s11`은 이야기가 반대네.

> **함수를 호출받은 쪽(callee)이 원래 값을 보존해야 한다.**

예:

```asm
foo:
    addi sp, sp, -16

    sd s0, 8(sp)

    ...
    
    ld s0, 8(sp)
    addi sp, sp, 16

    ret
```

왜 저장하는가?

`foo`가 들어올 때 `s0`에:

```text
0x12345678
```

이 들어 있었다면, `foo`가 `s0`를 마음대로 사용한 뒤 **반드시 원래 값으로 돌려놓고 반환해야 하기 때문**이지.

---

# 5. 이것이 바로 "callee backup"이라네

자네가 말한 바로 그것이네.

예를 들어:

```asm
foo:
    addi sp, sp, -32

    sd ra, 24(sp)
    sd s0, 16(sp)
    sd s1, 8(sp)

    ...
```

이것이 전형적인 **function prologue**의 일부네.

그리고 끝에서는:

```asm
    ld s1, 8(sp)
    ld s0, 16(sp)
    ld ra, 24(sp)

    addi sp, sp, 32
    ret
```

이것이 **epilogue**가 되지.

---

# 6. 그런데 `ra`가 정말 중요하다네

여기가 RISC-V 함수 호출에서 아주 재미있는 부분일세.

```asm
jal ra, foo
```

하면:

```text
현재 PC + 4
     ↓
    ra
```

그리고:

```text
PC
 ↓
foo
```

로 이동하지.

즉:

```text
caller

    jal ra, foo
       │
       ├──────→ ra = return address
       │
       └──────→ foo
```

그래서 `foo`가 또 다른 함수를 호출하면?

```asm
foo:
    jal ra, bar
```

이 순간 `ra`가 **bar로 돌아올 주소로 덮어써지네.**

따라서 `foo`가 원래 caller로 돌아가려면 자신의 `ra`를 먼저 저장해야 할 수 있네.

```text
caller
  │
  │ jal ra, foo
  ▼
 foo
  │
  │ save ra
  │
  │ jal ra, bar
  ▼
 bar
```

이것이 함수가 중첩될 때 stack frame이 필요한 주요 이유 중 하나라네.

---

# 7. 그래서 이런 함수가 등장한다네

```asm
foo:
    addi sp, sp, -16

    sd ra, 8(sp)

    jal ra, bar

    ld ra, 8(sp)

    addi sp, sp, 16
    ret
```

그림으로 보면:

```text
높은 주소
┌────────────────────┐
│                    │
├────────────────────┤
│ saved ra           │ ← 8(sp)
├────────────────────┤
│                    │
└────────────────────┘
낮은 주소
        ↑
        sp
```

이것이 아주 기본적인 stack frame이지.

---

# 8. `sp`는 조금 특별하다

`sp`:

```text
x2
```

는 Stack Pointer.

스택은 일반적으로:

```text
높은 주소
      ↓
┌──────────────┐
│              │
│    stack     │
│              │
└──────────────┘
      ↑
     sp
      ↓
낮은 주소
```

방향으로 성장한다고 생각하면 된다네.

예:

```asm
addi sp, sp, -32
```

→ 32바이트 stack frame 확보.

그리고:

```asm
addi sp, sp, 32
```

→ 다시 원상복구.

RV64 ABI에서는 stack pointer와 stack alignment에도 규칙이 있으며, 표준 ABI에서는 함수 호출 시 stack pointer가 128-bit(16-byte) 경계에 정렬되도록 요구한다네.

---

# 9. `a0 ~ a7`는 함수 인자

예를 들어 C에서:

```c
foo(10, 20, 30);
```

을 호출한다고 생각해 보세.

개념적으로:

```text
a0 = 10
a1 = 20
a2 = 30

call foo
```

가 되네.

그래서:

```asm
foo:
    # a0 = first argument
    # a1 = second argument
    # a2 = third argument
```

그리고 return 값도:

```text
a0
```

를 사용하네.

64비트 정수 하나를 반환한다면:

```text
a0 = return value
```

라고 생각하면 된다네.

두 개의 레지스터가 필요한 경우에는 `a0`, `a1`을 사용하게 되고.

---

# 10. `a0 ~ a7`를 "입출력 레지스터"라고 생각하면 편하다네

```text
                 function
                    │
        ┌───────────┴───────────┐
        │                       │
      입력                     출력
        │                       │
     a0 ~ a7                  a0/a1
```

예:

```asm
caller:

    li a0, 10
    li a1, 20

    jal ra, add_two

    # a0 = 30
```

callee:

```asm
add_two:
    add a0, a0, a1
    ret
```

이렇게 아주 간단하게 만들 수 있네.

---

# 11. `t0 ~ t6`는 "마음껏 쓰는 임시 레지스터"

예:

```asm
foo:
    add t0, a0, a1
    slli t1, t0, 2
    add a0, t1, a2
    ret
```

`foo`는 `t0`, `t1`을 사용하고 마음대로 버려도 되네.

caller 입장에서는:

> "함수 호출하고 나면 t0/t1 값은 사라졌을 수도 있다."

라고 생각해야 한다네.

---

# 12. `s0 ~ s11`는 "내가 쓰되 원상복구해야 하는 레지스터"

이름부터:

```text
s = saved
```

라고 기억하면 상당히 쉽네.

예:

```asm
foo:
    addi sp, sp, -16
    sd s0, 8(sp)

    # s0 자유롭게 사용

    ...

    ld s0, 8(sp)
    addi sp, sp, 16
    ret
```

즉:

```text
사용
 ↓
보관
 ↓
마음껏 사용
 ↓
복구
 ↓
return
```

이네.

---

# 13. `s0`는 `fp`라는 이름도 가지고 있다

이것도 중요하네.

```text
x8 = s0 = fp
```

`fp`:

```text
Frame Pointer
```

이지.

그래서 어떤 컴파일러의 디버그 어셈블리를 보면:

```asm
mv s0, sp
```

같은 것이 등장할 수 있네.

그러면:

```text
sp
 ↓
현재 stack 위치

s0/fp
 ↓
현재 함수 frame을 기준으로 삼는 주소
```

라는 관계가 생기지.

다만 최적화가 켜져 있으면 frame pointer를 생략할 수도 있네.

---

# 14. `zero`는 정말 독특하다네

```text
x0 = zero
```

이 레지스터는 **항상 0**이네.

쓰기:

```asm
add x0, a0, a1
```

를 해도 `x0`은 여전히:

```text
0
```

이다.

그래서 RISC-V에서는 이 하나 때문에 pseudo-instruction을 상당히 우아하게 만들 수 있네.

예:

```asm
mv a0, a1
```

실제로는:

```asm
addi a0, a1, 0
```

이고,

```asm
nop
```

은:

```asm
addi x0, x0, 0
```

처럼 표현할 수 있네.

그리고 아까 이야기했던:

```asm
beqz a0, label
```

도:

```asm
beq a0, x0, label
```

이라는 관계가 생기지.

---

# 15. `gp`와 `tp`는 일단 특별 취급

```text
x3 = gp
x4 = tp
```

### `gp`

Global Pointer.

전역/정적 데이터에 대한 효율적인 접근 등에 ABI 차원에서 사용될 수 있네.

### `tp`

Thread Pointer.

thread-local storage와 관련된다네.

지금 자네가 만드는 **bare-metal Hello World 단계에서는 깊게 들어갈 필요가 없네.**

오히려:

```text
zero
ra
sp
a0~a7
t0~t6
s0~s11
```

을 먼저 완전히 익히는 게 중요하네.

---

# 16. 그러면 자네의 Hello World를 함수로 분리해보면

지금 프로그램을 이런 식으로 발전시킬 수 있네.

```asm
_start:

    li   a0, 0x10000000
    la   a1, msg

    jal  ra, print

halt:
    j halt


print:

print_loop:
    lb   a2, 0(a1)
    beqz a2, print_done

    sb   a2, 0(a0)
    addi a1, a1, 1

    j print_loop

print_done:
    ret
```

여기서는 `print`가 다른 함수를 호출하지 않기 때문에 `ra`를 stack에 저장하지 않아도 되는 간단한 형태네.

하지만 `print()` 내부에서:

```asm
jal ra, uart_putc
```

같은 것을 호출한다면 상황이 달라지네.

그때는 `print`가 자신의 `ra`를 보존해야 할 가능성이 생긴다네.

---

# 17. 이걸 그림으로 보면 진짜 아름답다네

```text
_start
  │
  │ jal ra, print
  ▼
┌──────────────────────┐
│ print                │
│                      │
│ ra = return address  │
│                      │
│ jal ra, uart_putc    │
│       │              │
│       ▼              │
│   uart_putc          │
│       │              │
│       └──── ret ─────┤
│                      │
│       ret            │
└─────────┬────────────┘
          │
          ▼
        _start
```

그런데 `print`가 `uart_putc`를 호출하는 순간:

```text
print의 ra
```

가 덮어써지네.

따라서:

```asm
print:
    addi sp, sp, -16
    sd ra, 8(sp)

    ...
    jal ra, uart_putc
    ...

    ld ra, 8(sp)
    addi sp, sp, 16
    ret
```

이렇게 되는 것이지.

**이것이 바로 callee backup을 이해하는 핵심 장면**이라네.

---

# 18. 한 가지 더 중요한 사실

친구야, 여기서 용어를 아주 정확하게 잡아두면 앞으로 편하네.

`callee-saved`라는 것은:

> **callee가 반드시 무조건 stack에 저장한다**

라는 뜻은 아니네.

정확히는:

> **callee가 그 레지스터를 변경했다면, 호출 당시의 값을 반환 전에 복원해야 한다.**

예를 들어 `s0`를 전혀 건드리지 않는 함수라면:

```asm
foo:
    add a0, a0, a1
    ret
```

굳이:

```asm
sd s0, ...
```

할 필요가 없지.

이것은 컴파일러가 최적화할 때 굉장히 중요하게 작동한다네.

---

# 19. 그리고 `caller-saved`도 마찬가지

`caller-saved`라는 것은:

> caller가 **반드시** 저장한다

가 아니네.

정확히는:

> **caller가 그 값을 call 이후에도 필요로 한다면 caller가 보존해야 한다.**

필요 없다면 그냥 버리면 되네.

그래서 ABI는 일종의 **계약(contract)**이라고 생각하면 아주 좋다네.

```text
Caller                         Callee
  │                              │
  │ "a0~a7은 깨져도 괜찮아."      │
  │ "t0~t6도 깨져도 괜찮아."      │
  │                              │
  │ "하지만 s0~s11은 건드리면     │
  │  원래대로 돌려줘."            │
  │                              │
  └──────── call ────────────────►
```

이것이 **RISC-V calling convention**이네.

---

# 20. 그리고 이것이 C와 연결된다

예를 들어:

```c
long add(long a, long b)
{
    return a + b;
}
```

컴파일러는 대략:

```asm
add:
    add a0, a0, a1
    ret
```

처럼 만들 수 있네.

그러면:

```text
C
 ↓
Clang
 ↓
RISC-V ABI
 ↓
a0 = first argument
a1 = second argument
a0 = return
 ↓
add a0,a0,a1
 ↓
ret
```

가 된다네.

즉 **ABI가 없으면 서로 다른 함수가 어떻게 대화해야 할지 약속할 방법이 없는 것**이지.

---

# 21. 자네에게는 이 레지스터 그림을 책상 옆에 붙여두길 권하네

```text
RV64 INTEGER REGISTERS
══════════════════════════════════════

x0   zero       항상 0
x1   ra         return address
x2   sp         stack pointer
x3   gp         global pointer
x4   tp         thread pointer

x5   t0         temporary
x6   t1         temporary
x7   t2         temporary

x8   s0/fp      saved / frame pointer
x9   s1         saved

x10  a0         argument / return
x11  a1         argument / return
x12  a2         argument
x13  a3         argument
x14  a4         argument
x15  a5         argument
x16  a6         argument
x17  a7         argument

x18  s2         saved
x19  s3         saved
x20  s4         saved
x21  s5         saved
x22  s6         saved
x23  s7         saved
x24  s8         saved
x25  s9         saved
x26  s10        saved
x27  s11        saved

x28  t3         temporary
x29  t4         temporary
x30  t5         temporary
x31  t6         temporary


CALLER-SAVED
────────────────────
ra
a0-a7
t0-t6

CALLEE-SAVED
────────────────────
sp
s0-s11

SPECIAL
────────────────────
zero
gp
tp
```

다만 엄밀히 말하면 `sp`는 단순히 일반적인 "callee-saved register"라고만 이해하기보다 **ABI가 항상 보존해야 하는 stack pointer**로 보는 것이 더 정확하네.

---

## 그리고 다음 실습은 이것이 딱 좋겠네

지금 단계에서는 **명령어를 더 추가하기보다 `call → stack → ra → callee-saved → return`을 직접 LLDB로 관찰**해보는 게 아주 좋네.

예를 들어 우리가 방금 만든:

```text
_start
   ↓
print
   ↓
uart_putc
   ↓
return
```

3단계 함수를 만들고,

```text
LLDB
 ├── register read ra
 ├── register read sp
 ├── register read a0
 ├── register read a1
 ├── memory read $sp
 └── stepi
```

를 한 명령씩 실행하면서 **`ra`가 어떻게 덮어써지고, `sp`가 내려가고, stack에 저장된 `ra`가 어떻게 복구되는지** 관찰해보면 된다네.

그 순간 `callee-saved`가 책 속의 추상적인 ABI 규칙이 아니라 **실제로 CPU 안에서 벌어지는 사건**으로 바뀔 걸세. 하하하.

그리고 이걸 이해하고 나면 자연스럽게 **RISC-V의 `call`/`ret` pseudo-instruction이 실제 `JAL`/`JALR`로 어떻게 바뀌는지**까지 연결할 수 있네. 그 부분은 지금까지 자네가 해온 `llvm-mc` 실험과 정확히 맞물린다네.
