#!/usr/bin/env zsh
set -e

if [ $# -eq 0 ]; then
  echo "사용법: $0 [출력이름]   (boot.rv + kernel.rv 를 함께 빌드)"
  exit 1
fi

OUT=$1
MODE=${2:-run} # 기본값은 'run', debug를 주면 -S -s 켜짐

rm -f boot.o kernel.o ${OUT}.elf   # -f로 없어도 에러 안 나게

llvm-mc -triple=riscv64 -filetype=obj -g boot.rv   -o boot.o
llvm-mc -triple=riscv64 -filetype=obj -g kernel.rv -o kernel.o
llvm-mc -triple=riscv64 -filetype=obj -g data.rv -o data.o

# -Ttext 대신 -T link.ld 로 링커스크립트 자체를 적용 (섹션 배치, __bss_start 등)
ld.lld -m elf64lriscv -T link.ld boot.o kernel.o data.o -o ${OUT}.elf

if [ "$MODE" = "debug" ]; then
  echo "디버그 모드 — 다른 터미널에서:"
  echo "  lldb ${OUT}.elf"
  echo "  (lldb) gdb-remote 127.0.0.1:1234"
  qemu-system-riscv64 -machine virt -nographic -bios none -kernel ${OUT}.elf -S -s
else
  qemu-system-riscv64 -machine virt -nographic -bios none -kernel ${OUT}.elf
fi

# qemu-system-riscv64 -machine virt -nographic -bios none -kernel ${OUT}.elf
