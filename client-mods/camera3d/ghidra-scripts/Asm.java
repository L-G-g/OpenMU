// Desensambla un rango: args = desde hasta
// @category Analysis
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;

public class Asm extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] a = getScriptArgs();
        Instruction ins = getInstructionAt(toAddr(a[0]));
        if (ins == null) ins = getInstructionAfter(toAddr(a[0]));
        while (ins != null && ins.getAddress().compareTo(toAddr(a[1])) <= 0) {
            println("ASM " + ins.getAddress() + " : " + ins);
            ins = ins.getNext();
        }
    }
}
