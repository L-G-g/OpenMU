// Decompila todas las funciones en un rango de direcciones. args: salida desde hasta
// @category Analysis
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import java.io.*;

public class DecompRange extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] a = getScriptArgs();
        DecompInterface d = new DecompInterface();
        d.openProgram(currentProgram);
        try (PrintWriter w = new PrintWriter(new FileWriter(a[0]))) {
            FunctionIterator it = currentProgram.getFunctionManager().getFunctions(toAddr(a[1]), true);
            while (it.hasNext()) {
                Function f = it.next();
                if (f.getEntryPoint().compareTo(toAddr(a[2])) > 0) break;
                DecompileResults r = d.decompileFunction(f, 60, monitor);
                w.println("// ===== " + f.getName() + " @ " + f.getEntryPoint());
                w.println(r.getDecompiledFunction() == null ? "// failed" : r.getDecompiledFunction().getC());
            }
        }
    }
}
