// Busca instrucciones cuyo texto contenga todos los fragmentos dados. args: fragmento...
// @category Analysis
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;

public class InsnFind extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] a = getScriptArgs();
        InstructionIterator it = currentProgram.getListing().getInstructions(true);
        int n = 0;
        while (it.hasNext() && n < 200) {
            Instruction ins = it.next();
            String s = ins.toString();
            boolean all = true;
            for (String f : a) if (!s.contains(f)) { all = false; break; }
            if (all) {
                Function fn = getFunctionContaining(ins.getAddress());
                println(ins.getAddress() + " " + (fn == null ? "?" : fn.getName()) + " : " + s);
                n++;
            }
        }
    }
}
