// clang exhelper_linux_arm64.s -o exhelper_linux_arm64.so -shared -Wl,--eh-frame-hdr,-z,now,-z,noexecstack,-x 
.arch armv8-a

.set _UA_SEARCH_PHASE, 0
.set _UA_CLEANUP_PHASE, 1
.set _UA_HANDLER_FRAME, 2
.set _UA_FORCE_UNWIND, 3

.set _URC_HANDLER_FOUND, 6
.set _URC_INSTALL_CONTEXT, 7
.set _URC_CONTINUE_UNWIND, 8

.set DW_REG_x0, 0


.data

tlskey:
    .space 8

LSDA_none:
    .word 0

LSDA_mton:
    .word .Lemtn_landingpad - LSDA_mton

ref_personality:
    .xword _personality


.section .init_array,"aw"
.dc.a _eh_init_tlskey


.text
.global eh_get_exception_ptr, eh_has_exception, eh_managed_to_native, eh_native_to_managed

eh_get_exception_ptr:
    .cfi_startproc
    .cfi_lsda 0x1b, LSDA_none
    stp x19, x20, [sp, #-96]!
    stp x7, x8, [sp, #16]
    stp x5, x6, [sp, #32]
    stp x3, x4, [sp, #48]
    stp x1, x2, [sp, #64]
    stp x29, x30, [sp, #80]
    .cfi_def_cfa_offset 96
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    .cfi_offset x2, -24
    .cfi_offset x1, -32
    .cfi_offset x4, -40
    .cfi_offset x3, -48
    .cfi_offset x6, -56
    .cfi_offset x5, -64
    .cfi_offset x8, -72
    .cfi_offset x7, -80
    .cfi_offset x20, -88
    .cfi_offset x19, -96
    add x29, sp, #80
    ldr x0, =tlskey
    ldr x0, [x0]
    mov x20, x0
    bl pthread_getspecific
    cbz x0, .Legep_init
.Legep_ret:
    ldp x29, x30, [sp, #80]
    ldp x1, x2, [sp, #64]
    ldp x3, x4, [sp, #48]
    ldp x5, x6, [sp, #32]
    ldp x7, x8, [sp, #16]
    ldp x19, x20, [sp], #96
    .cfi_remember_state
    .cfi_restore x30
    .cfi_restore x29
    .cfi_restore x2
    .cfi_restore x1
    .cfi_restore x4
    .cfi_restore x3
    .cfi_restore x6
    .cfi_restore x5
    .cfi_restore x8
    .cfi_restore x7
    .cfi_restore x20
    .cfi_restore x19
    .cfi_def_cfa_offset 0
    ret
.Legep_init:
    .cfi_restore_state
    mov x0, #8
    bl malloc
    mov x19, x0
    mov x1, x0
    mov x0, x20
    bl pthread_setspecific
    mov x0, x19
    b .Legep_ret
    .cfi_endproc

_eh_init_tlskey:
    stp x29, x30, [sp, #-16]!
    mov x29, sp
    ldr x0, =tlskey
    ldr x1, =free
    bl pthread_key_create
    cbnz x0, .Leit_error
.Leit_ret:
    ldp x29, x30, [sp], #16
    ret
.Leit_error:
    b .Leit_ret

eh_has_exception:
    .cfi_startproc
    .cfi_lsda 0x1b, LSDA_none
    stp x29, x30, [sp, #-16]!
    .cfi_def_cfa_offset 16
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    mov x29, sp
    bl eh_get_exception_ptr
    ldr x0, [x0]
    cmp x0, #0
    cset x0, ne
    ldp x29, x30, [sp], #16
    .cfi_restore x30
    .cfi_restore x29
    .cfi_def_cfa_offset 0
    ret
    .cfi_endproc

eh_managed_to_native:
    .cfi_startproc
    .cfi_personality 0x9c, ref_personality
    .cfi_lsda 0x1b, LSDA_mton
    stp x29, x30, [sp, #-16]!
    .cfi_def_cfa_offset 16
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    mov x29, sp
    blr x9
    ldp x29, x30, [sp], #16
    .cfi_remember_state
    .cfi_restore x30
    .cfi_restore x29
    .cfi_def_cfa_offset 0
    ret
.Lemtn_landingpad:
    .cfi_restore_state
    mov x1, x0 
    bl eh_get_exception_ptr
    str x1, [x0]
    mov x0, xzr
    ldp x29, x30, [sp], #16
    .cfi_restore x30
    .cfi_restore x29
    .cfi_def_cfa_offset 0
    ret
    .cfi_endproc

eh_native_to_managed:
    .cfi_startproc
    .cfi_personality 0x9c, ref_personality
    .cfi_lsda 0x1b, LSDA_none
    stp x20, x19, [sp, #-32]!
    stp x29, x30, [sp, #16]
    .cfi_def_cfa_offset 32
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    .cfi_offset x19, -24
    .cfi_offset x20, -32
    add x29, sp, #16
    mov x19, x9
    mov x20, x0
    bl eh_get_exception_ptr
    str xzr, [x0]
    mov x0, x20
    blr x19
    mov x20, x0
    bl eh_get_exception_ptr
    ldr x0, [x0]
    cbnz x0, .Lentm_do_rethrow
    mov x0, x20
    ldp x29, x30, [sp, #16]
    ldp x20, x19, [sp], #32
    .cfi_remember_state
    .cfi_restore x30
    .cfi_restore x29
    .cfi_restore x19
    .cfi_restore x20
    .cfi_def_cfa_offset 0
    ret
.Lentm_do_rethrow:
    .cfi_restore_state
    bl _Unwind_RaiseException
    brk #1
    .cfi_endproc

_personality:
    .cfi_startproc
    .cfi_lsda 0x1b, LSDA_none
    stp x22, x21, [sp, #-48]!
    stp x20, x19, [sp, #16]
    stp x29, x30, [sp, #32]
    .cfi_def_cfa_offset 48
    .cfi_offset x30, -8
    .cfi_offset x29, -16
    .cfi_offset x19, -24
    .cfi_offset x20, -32
    .cfi_offset x21, -40
    .cfi_offset x22, -48
    add x29, sp, #32
    mov x19, x1
    mov x20, x2
    mov x21, x3
    mov x22, x4
    tbz x19, _UA_FORCE_UNWIND, .Lp_should_process
    mov x0, _URC_CONTINUE_UNWIND
    b .Lp_ret
.Lp_should_process:
    mov x0, x22
    bl _Unwind_GetLanguageSpecificData
    ldrsw x1, [x0]
    tbz x19, _UA_SEARCH_PHASE, .Lp_handler_phase
    cbz x1, .Lp_no_handler
    mov x0, _URC_HANDLER_FOUND
    b .Lp_ret
.Lp_no_handler:
    mov x0, _URC_CONTINUE_UNWIND
    b .Lp_ret
.Lp_handler_phase:
    tbz x19, _UA_HANDLER_FRAME, .Lp_no_handler
    add x1, x1, x0
    mov x0, x22
    bl _Unwind_SetIP
    mov x0, x22
    mov x1, #DW_REG_x0
    mov x2, x21
    bl _Unwind_SetGR
    mov x0, _URC_INSTALL_CONTEXT
.Lp_ret:
    ldp x29, x30, [sp, #32]
    ldp x20, x19, [sp, #16]
    ldp x22, x21, [sp], #48
    .cfi_restore x30
    .cfi_restore x29
    .cfi_restore x19
    .cfi_restore x20
    .cfi_restore x21
    .cfi_restore x22
    .cfi_def_cfa_offset 0
    ret
    .cfi_endproc
