import idc
import ida_auto
import ida_lines
import ida_pro
import idautils


def parse_address(index, default):
    if len(idc.ARGV) <= index:
        return default
    return int(idc.ARGV[index], 0)


ida_auto.auto_wait()
start = parse_address(1, 0)
end = parse_address(2, start + 0x100)

for address in idautils.Heads(start, end):
    line = ida_lines.generate_disasm_line(address, 0) or ""
    print(f"{address:08X}: {ida_lines.tag_remove(line)}")

ida_pro.qexit(0)
