.text
.align 2
.thumb
.thumb_func
.global newmovesetstyle2
main:
 ldrb r1, [r0, #0x2]
 mov r2, #0xFF
 cmp r1, r2
 beq exit2
 mov r9, r2
 mov r3, #0x0
loop: lsl r0, r3, #0x1
 add r0, r0, r3
 ldr r1, movesettable
 add r1, r1, r6
 ldr r1, [r1, #0x0]
 add r7, r0, r1
 ldrb r0, [r7, #0x2]
 mov r4, r10
 cmp r0, r4
 bgt exit2
 ldrb r1, [r7, #0x1]
 ldrb r0, [r7, #0x0]
 lsl r1, r1, #0x8
 orr r1, r0
 mov r0, r8
 str r3, [sp, #0x0]
 bl branchone
 mov r5, r9
 ldr r3, [sp, #0x0]
 cmp r0, r9
 bne exit
 mov r0, r8
 add r1, r4, #0x0
 bl branchtwo
 ldr r3, [sp, #0x0]
exit: add r3, #0x1
 lsl r1, r3, #0x1
 add r1, r1, r3
 add r0, r7, r1
 ldrb r0, [r0, #0x2]
 cmp r0, r5
 bne loop
exit2: add sp, #0x4
 pop {r3-r5}
 mov r8, r3
 mov r9, r4
 mov r10, r5
 pop {r4-r7}
 pop {r0}
 bx r0
branchone: push {r4-r7,lr}
 add sp, #-0x4
 ldr r7, gothere
 bx r7
branchtwo: push {r4-r7}
 ldr r7, gothere2
 bx r7
.align
gothere: .word 0x0803E8B5
gothere2: .word 0x0803EC43
movesettable: .word 0x08A0B7E4