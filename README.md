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
