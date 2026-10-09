// Lista referencias a simbolos por nombre (ej. GetAsyncKeyState).
// @category Analysis
import ghidra.app.script.GhidraScript;
import ghidra.program.model.symbol.*;
import ghidra.program.model.listing.Function;

public class SymRefs extends GhidraScript {
    @Override
    protected void run() throws Exception {
        for (String name : getScriptArgs()) {
            for (Symbol s : currentProgram.getSymbolTable().getSymbols(name)) {
                println("== " + name + " @ " + s.getAddress());
                for (Reference r : getReferencesTo(s.getAddress())) {
                    Function f = getFunctionContaining(r.getFromAddress());
                    println("   " + r.getFromAddress() + " in " + (f == null ? "?" : f.getName() + "@" + f.getEntryPoint()));
                    for (Reference r2 : getReferencesTo(r.getFromAddress())) {
                        Function f2 = getFunctionContaining(r2.getFromAddress());
                        println("      <- " + r2.getFromAddress() + " in " + (f2 == null ? "?" : f2.getName() + "@" + f2.getEntryPoint()));
                    }
                }
            }
        }
    }
}
