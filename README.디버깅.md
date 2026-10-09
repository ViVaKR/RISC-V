# 디버깅

```bash
(lldb) target create good.elf
(lldb) gdb-remote 127.0.0.1:1234
(lldb) breakpoint set --name kernel_main
(lldb) breakpoint set --name print_string
(lldb) breakpoint set --name halt
(lldb) continue        # kernel_main 진입 — sp 확인
(lldb) register read sp
(lldb) continue        # print_string 진입 — 스택 프레임 확인
(lldb) register read sp ra
(lldb) continue        # halt 진입 — 종료 직전 상태 확인
(lldb) continue        # 여기서 QEMU 종료됨, 세션 끊김은 정상
```
