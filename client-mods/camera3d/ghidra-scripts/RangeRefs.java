// Lista las direcciones dentro de un rango que el codigo referencia, con la funcion que las usa.
// args: desde hasta
// @category Analysis
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.*;

public class RangeRefs extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] a = getScriptArgs();
        AddressSet set = new AddressSet(toAddr(a[0]), toAddr(a[1]));
        ReferenceManager rm = currentProgram.getReferenceManager();
        AddressIterator it = rm.getReferenceDestinationIterator(set, true);
        while (it.hasNext()) {
            Address to = it.next();
            int n = 0;
            StringBuilder sb = new StringBuilder();
            for (Reference r : rm.getReferencesTo(to)) {
                if (n++ < 6) {
                    Function f = getFunctionContaining(r.getFromAddress());
                    sb.append(" ").append(f == null ? r.getFromAddress().toString() : f.getName());
                }
            }
            println(to + " (" + n + "):" + sb);
        }
    }
}
