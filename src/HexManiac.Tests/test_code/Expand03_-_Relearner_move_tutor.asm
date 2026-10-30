.text
.align 2
.thumb
.thumb_func
.global newmovesetstyle
main:
 lsl r2, r5, #0x1
 add r2, r2, r5
 ldr r1, [sp, #0x10]
 add r0, r2, r1
 ldrb r0, [r0, #0x2]
 ldr r1, [sp, #0xC]
 add r7, r2, #0x0
 add r5, #0x1
 mov r12, r5
 cmp r0, r1
 bgt later
 mov r4, #0x0
 cmp r1, r0
 beq later2
 mov r4, #0x1
 neg r4, r4
 ldr r0, [sp, #0x14]
 ldr r1, table
 add r6, r0, r1
 mov r3, sp
 sub r3, #0x2
 add r5, r7, #0x0
there: add r3, #0x2
 add r4, #0x1
 cmp r4, #0x3
 bgt later2
 ldr r0, [r6, #0x0]
 add r0, r5, r0
 ldrb r2, [r0, #0x0]
 ldrb r0, [r0, #0x1]
 lsl r0, r0, #0x8
 orr r0, r2
 ldrh r2, [r3, #0x0]
 cmp r0, r2
 bne there
later2: cmp r4, #0x4
 bne later
 mov r4, #0x0
 cmp r4, r10
 bge later3
 mov r1, r9
 ldr r0, [r1, #0x0]
 add r0, r7, r0
 ldrb r2, [r0, #0x0]
 ldrb r1, [r0, #0x1]
 lsl r1, r1, #0x8
 orr r1, r2
 ldr r0, [sp, #0x8]
 ldrh r2, [r0, #0x0]
 cmp r1, r2
 beq later3
 ldr r1, [sp, #0x14]
 ldr r2, table
 add r6, r1, r2
 ldr r3, [sp, #0x8]
 add r5, r7, #0x0
there2: add r3, #0x2
 add r4, #0x1
 cmp r4, r10
 bge later3
 ldr r0, [r6, #0x0]
 add r0, r5, r0
 ldrb r2, [r0, #0x0]
 ldrb r0, [r0, #0x1]
 lsl r0, r0, #0x8
 orr r0, r2
 ldrh r2, [r3, #0x0]
 cmp r0, r2
 bne there2
later3: cmp r4, r10
 bne later
 mov r0, r10
 add r0, #0x1
 mov r10, r0
 lsl r2, r4, #0x1
 ldr r1, [sp, #0x8]
 add r2, r2, r1
 mov r4, r9
 ldr r0, [r4, #0x0]
 add r0, r7, r0
 ldrb r1, [r0, #0x0]
 ldrb r0, [r0, #0x1]
 lsl r0, r0, #0x8
 orr r0, r1
 strh r0, [r2, #0x0]
later: mov r5, r12
 mov r1, r9
 ldr r0, [r1, #0x0]
 lsl r1, r5, #0x1
 add r1, r1, r5
 add r1, r1, r0
 ldrb r0, [r1, #0x2]
 cmp r0, #0xFF
 bne main
 mov r0, r10
 add sp, #0x18
 pop {r3-r5}
 mov r8, r3
 mov r9, r4
 mov r10, r5
 pop {r4-r7}
 pop {r1}
 bx r1
.align
table: .word 0x0890F000