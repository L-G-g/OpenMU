// Lista referencias a direcciones y desensambla el inicio de funciones.
// @category Analysis
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.Reference;

public class Refs extends GhidraScript {
    @Override
    protected void run() throws Exception {
        for (String s : getScriptArgs()) {
            Address a = toAddr(s);
            println("== refs to " + a);
            for (Reference r : getReferencesTo(a)) {
                Function fn = getFunctionContaining(r.getFromAddress());
                Instruction ins = getInstructionAt(r.getFromAddress());
                println("  " + r.getFromAddress() + " " + r.getReferenceType() + " in " + (fn == null ? "?" : fn.getName() + "@" + fn.getEntryPoint()) + " : " + (ins == null ? "" : ins.toString()));
            }
            Instruction ins = getInstructionAt(a);
            for (int i = 0; i < 8 && ins != null; i++) {
                println("  asm " + ins.getAddress() + " len=" + ins.getLength() + " : " + ins);
                ins = ins.getNext();
            }
        }
    }
}
