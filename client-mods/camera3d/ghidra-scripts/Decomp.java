// Decompila las funciones pasadas como argumentos (direcciones hex) y las escribe a un archivo.
// @category Analysis
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import java.io.*;

public class Decomp extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        String outPath = args[0];
        DecompInterface d = new DecompInterface();
        d.openProgram(currentProgram);
        try (PrintWriter w = new PrintWriter(new FileWriter(outPath))) {
            for (int i = 1; i < args.length; i++) {
                Function f = getFunctionAt(toAddr(args[i]));
                if (f == null) { w.println("// no function at " + args[i]); continue; }
                DecompileResults r = d.decompileFunction(f, 120, monitor);
                w.println("// ===== " + f.getName() + " @ " + f.getEntryPoint());
                w.println(r.getDecompiledFunction() == null ? "// failed" : r.getDecompiledFunction().getC());
            }
        }
    }
}
