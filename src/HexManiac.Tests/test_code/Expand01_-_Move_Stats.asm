.text
.align 2
.thumb
.thumb_func
.global newmovesetstyle
main:
 mov r1, r9
 lsl r1, r1, #0x2
 ldr r0, table
 add r0, r0, r1
 ldr r0, [r0, #0x0]
 ldr r6, there
 add r6, #0x6
 ldrb r7, [r6, #0x0]
loop: lsl r1, r7, #0x1
 add r1, r1, r7
 add r3, r0, r1
 ldrb r1, [r3, #0x2]
 mov r4, r10
 cmp r4, r1
 beq learn
 cmp r1, #0xFF
 beq exit
 add r7, #0x1
 b loop
learn: ldr r2, there
 add r7, #0x1
 strb r7, [r6, #0x0]
 ldrb r1, [r3, #0x1]
 lsl r1, r1, #0x8
 ldrb r0, [r3, #0x0]
 orr r0, r1
 strh r0, [r2, #0x0]
 ldr r1, return
 bx r1
exit: ldr r0, return2
 bx r0
.align
return:  .word 0x0803EB65
return2: .word 0x0803EB73
table:  .word 0x08900000
there:  .word 0x02024022
