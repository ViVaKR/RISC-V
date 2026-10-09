# =================================================
#  제목: 공용 매크로 모음 (hun.macros.inc) — RISC-V RV64
#  목적: 섹션 선언, 함수 프롤로그/에필로그, UART 출력을 짧게 쓰기 위한 매크로
#        GNU as 와 LLVM(clang) 양쪽에서 동작하도록 .macro / .endm 만 사용
# =================================================
    .ifndef HUN_MACROS_INC
    .set    HUN_MACROS_INC, 1

# --- 플랫폼 상수 (QEMU -machine virt) ---
    .equ    XLEN,           64
    .equ    SZREG,          8
    .equ    UART0_BASE,     0x10000000      # NS16550A UART
    .equ    UART_LSR,       5               # Line Status Register 오프셋
    .equ    UART_LSR_THRE,  0x20            # 송신 버퍼 비어 있음 비트
    .equ    SYSCON_BASE,    0x100000        # sifive_test (종료 장치)
    .equ    SYSCON_PASS,    0x5555
    .equ    SYSCON_FAIL,    0x3333

# ------------------------------------------------------
# [섹션 선언] 링커 스크립트(link.ld)의 섹션 이름과 맞춰져 있다.
#   .text   : 실행 코드 (명령어는 4바이트 정렬)
#   .rodata : 읽기 전용 데이터 (문자열, 상수 테이블)
#   .data   : 초기값이 있는 변수
#   .bss    : 0 으로 초기화되는 변수 (파일 용량을 차지하지 않음)
# ------------------------------------------------------
    .macro CODE_SECTION
        .section .text
        .balign 4
    .endm
    # 사용 예)
    #   CODE_SECTION
    #   add_one:
    #       addi    a0, a0, 1       # a0(인자) + 1 을 a0(반환값)에 저장
    #       ret

    .macro RODATA_SECTION
        .section .rodata
        .balign 8
    .endm
    # 사용 예)
    #   RODATA_SECTION
    #   msg:        .asciz "안녕, RISC-V!\n"
    #   fib_table:  .dword 0, 1, 1, 2, 3, 5, 8, 13

    .macro DATA_SECTION
        .section .data
        .balign 8
    .endm
    # 사용 예)
    #   DATA_SECTION
    #   counter:    .dword 0        # 실행 중 값이 바뀌는 전역 변수
    #   hp:         .word 100

    .macro BSS_SECTION
        .section .bss
        .balign 8
    .endm
    # 사용 예)
    #   BSS_SECTION
    #   input_buffer: .skip 256     # 256바이트 버퍼, 0 으로 초기화됨

# ------------------------------------------------------
# [UART 출력] 임시 레지스터 t0, t1 을 사용한다 (호출 규약상 마음대로 써도 되는 레지스터)
# ------------------------------------------------------
    # UART 송신 준비가 될 때까지 대기. base 에는 UART 베이스 주소가 든 레지스터를 준다.
    .macro UART_WAIT base, tmp
.Luart_wait_\@:
        lbu     \tmp, UART_LSR(\base)
        andi    \tmp, \tmp, UART_LSR_THRE
        beqz    \tmp, .Luart_wait_\@
    .endm

    # 문자 하나 출력 (t0, t1 사용). 사용 예) PUTC 0x41  -> A
    .macro PUTC ch
        li      t0, UART0_BASE
        UART_WAIT t0, t1
        li      t1, \ch
        sb      t1, 0(t0)
    .endm

    # NUL 종단 문자열 출력 (a0 사용, uart_puts 를 call 하므로 ra 가 덮어써진다)
    # 호출하는 함수가 프롤로그에서 ra 를 저장해 둔 상태여야 한다.
    .macro PRINT label
        la      a0, \label
        call    uart_puts
    .endm

    # 종료 코드를 지정하고 QEMU 종료 (돌아오지 않음)
    .macro EXIT code
        li      a0, \code
        call    qemu_exit
    .endm

# --- [함수 프롤로그 / 에필로그] --- #

# ========================================================================
# [기본형] ra, s0(프레임 포인터) 만 저장하는 16바이트 프레임
#   사용 예)  FUNC_START  my_func
#             ...
#             FUNC_END
# ========================================================================
    .macro FUNC_START name
        .global \name
        .balign 4
\name:
        addi    sp, sp, -16
        sd      ra, 8(sp)
        sd      s0, 0(sp)
        addi    s0, sp, 16
    .endm

    .macro FUNC_END
        ld      ra, 8(sp)
        ld      s0, 0(sp)
        addi    sp, sp, 16
        ret
    .endm

# ========================================================================
# [풀 스펙] ra, s0~s11 전원 백업 (callee-saved 레지스터 12개 + ra)
# RISC-V 규칙상 sp 는 16바이트 정렬이어야 하므로 stack_size 는 16의 배수,
# 최소 112 바이트 (= 레지스터 13개 x 8바이트, 16바이트 올림).
#   사용 예)  FUNC_START_FULL big_func, 112
#             ...
#             FUNC_EXIT_FULL 112
# ========================================================================
    .macro FUNC_START_FULL name, stack_size
        .if     \stack_size < 112
            .error "FUNC_START_FULL: stack_size 는 최소 112 이상이어야 합니다 (ra + s0~s11 저장)"
        .endif
        .if     (\stack_size & 15) != 0
            .error "FUNC_START_FULL: stack_size 는 16의 배수여야 합니다 (RISC-V sp 정렬 규칙)"
        .endif
        .global \name
        .balign 4
\name:
        addi    sp, sp, -\stack_size
        sd      ra , 0(sp)
        sd      s0 , 8(sp)
        sd      s1 , 16(sp)
        sd      s2 , 24(sp)
        sd      s3 , 32(sp)
        sd      s4 , 40(sp)
        sd      s5 , 48(sp)
        sd      s6 , 56(sp)
        sd      s7 , 64(sp)
        sd      s8 , 72(sp)
        sd      s9 , 80(sp)
        sd      s10, 88(sp)
        sd      s11, 96(sp)
        addi    s0, sp, \stack_size
    .endm

    .macro FUNC_EXIT_FULL stack_size
        ld      ra , 0(sp)
        ld      s0 , 8(sp)
        ld      s1 , 16(sp)
        ld      s2 , 24(sp)
        ld      s3 , 32(sp)
        ld      s4 , 40(sp)
        ld      s5 , 48(sp)
        ld      s6 , 56(sp)
        ld      s7 , 64(sp)
        ld      s8 , 72(sp)
        ld      s9 , 80(sp)
        ld      s10, 88(sp)
        ld      s11, 96(sp)
        addi    sp, sp, \stack_size
        ret
    .endm

    .endif
