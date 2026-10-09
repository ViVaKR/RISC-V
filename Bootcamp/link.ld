/* =========================================================================
 * link.ld — QEMU virt 머신용 베어메탈 링커 스크립트
 *   - QEMU virt 의 RAM 은 0x80000000 부터 시작한다 (기본 크기 128MB)
 *   - -bios none 으로 실행하면 이 주소의 첫 명령부터 실행되므로
 *     _start(.text.init)를 반드시 맨 앞에 배치한다.
 * ========================================================================= */
OUTPUT_ARCH(riscv)
ENTRY(_start)

MEMORY
{
    RAM (rwx) : ORIGIN = 0x80000000, LENGTH = 128M
}

/* 세그먼트를 권한별로 분리한다 (코드 R+X / 상수 R / 데이터 RW).
 * 하나로 합치면 RWX 세그먼트가 생겨서 최신 ld 가 경고를 출력한다. */
PHDRS
{
    text   PT_LOAD FLAGS(5);   /* R + X */
    rodata PT_LOAD FLAGS(4);   /* R     */
    data   PT_LOAD FLAGS(6);   /* R + W */
}

SECTIONS
{
    .text : ALIGN(4)
    {
        *(.text.init)
        *(.text .text.*)
    } > RAM :text

    .rodata : ALIGN(8)
    {
        *(.rodata .rodata.* .srodata .srodata.*)
    } > RAM :rodata

    .data : ALIGN(8)
    {
        __data_start = .;
        *(.data .data.*)
        . = ALIGN(8);
        __global_pointer$ = . + 0x800;   /* gp 는 .sdata 중간을 가리킨다 (relaxation 용) */
        *(.sdata .sdata.*)
        __data_end = .;
    } > RAM :data

    .bss (NOLOAD) : ALIGN(8)
    {
        __bss_start = .;
        *(.sbss .sbss.* .bss .bss.* COMMON)
        . = ALIGN(8);
        __bss_end = .;
    } > RAM :data

    /* 스택은 RAM 맨 끝에서 아래로 자란다 */
    __stack_top = ORIGIN(RAM) + LENGTH(RAM);
}
