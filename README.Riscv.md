# RISC-V 빌드앤 런

## 빌드

```bash
clang -g -x assembler --target=riscv64-unknown-elf -march=rv64gc \
  -mabi=lp64d \ 
  -nostdlib -static -fuse-ld=lld -Wl,-Ttext=0x80000000 \
  -o helloworld helloworld.rv
```
## 실행

```bash
qemu-system-riscv64 -machine virt -nographic -bios none -kernel helloworld
```

## 디버깅

```bash
llvm-objdump
llvm-readelf
llvm-dwarfdump
lldb

```