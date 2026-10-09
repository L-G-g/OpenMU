// Busca las vtables de clases por su nombre RTTI (MSVC) y lista sus funciones virtuales.
// args: nombres de clase (ej. CNewUIMyInventory)
// @category Analysis
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.listing.Function;

public class Vtables extends GhidraScript {
    @Override
    protected void run() throws Exception {
        Memory mem = currentProgram.getMemory();
        for (String cls : getScriptArgs()) {
            String mangled = ".?AV" + cls + "@";
            Address str = find(null, mangled.getBytes());
            if (str == null) { println("== " + cls + ": RTTI no encontrado"); continue; }
            Address typeDesc = str.subtract(8); // TypeDescriptor: vftable ptr, spare, name
            // Complete Object Locator: sig(0), offset, cdOffset, pTypeDescriptor, pClassHierarchy
            byte[] td = intBytes((int) typeDesc.getOffset());
            Address a = mem.getMinAddress();
            while ((a = mem.findBytes(a, td, null, true, monitor)) != null) {
                Address col = a.subtract(12);
                int sig = mem.getInt(col);
                int offset = mem.getInt(col.add(4));
                if (sig == 0 && offset == 0) {
                    byte[] colBytes = intBytes((int) col.getOffset());
                    Address ref = mem.findBytes(mem.getMinAddress(), colBytes, null, true, monitor);
                    if (ref != null) {
                        Address vt = ref.add(4);
                        println("== " + cls + " vtable @ " + vt);
                        for (int i = 0; i < 24; i++) {
                            Address fa = toAddr(mem.getInt(vt.add(i * 4)) & 0xffffffffL);
                            Function f = getFunctionAt(fa);
                            if (f == null && getFunctionContaining(fa) == null && !mem.contains(fa)) break;
                            println("   [" + i + "] " + fa + (f != null ? " " + f.getName() : ""));
                        }
                    }
                }
                a = a.add(1);
            }
        }
    }

    private static byte[] intBytes(int v) {
        return new byte[] { (byte) v, (byte) (v >> 8), (byte) (v >> 16), (byte) (v >> 24) };
    }
}
