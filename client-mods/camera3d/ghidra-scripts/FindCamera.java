// Busca candidatos de variables de cámara en main.exe 1.04d.
// @category Analysis
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.mem.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.scalar.Scalar;
import java.util.*;

public class FindCamera extends GhidraScript {
    @Override
    protected void run() throws Exception {
        Memory mem = currentProgram.getMemory();
        float[] targets = { -48.5f, 48.5f, -45.0f, 1000.0f, 1100.0f, 1200.0f, 35.0f, 30.0f, -135.0f, 45.0f };
        for (float f : targets) {
            int bits = Float.floatToIntBits(f);
            byte[] pat = new byte[] { (byte) bits, (byte) (bits >> 8), (byte) (bits >> 16), (byte) (bits >> 24) };
            Address a = mem.getMinAddress();
            int count = 0;
            while (a != null && count < 40) {
                a = mem.findBytes(a, pat, null, true, monitor);
                if (a == null) break;
                MemoryBlock b = mem.getBlock(a);
                Reference[] refs = getReferencesTo(a);
                if (refs.length > 0 && refs.length < 60) {
                    StringBuilder sb = new StringBuilder();
                    for (Reference r : refs) {
                        Function fn = getFunctionContaining(r.getFromAddress());
                        sb.append(r.getFromAddress()).append("(").append(fn == null ? "?" : fn.getName()).append(") ");
                    }
                    println("FLOAT " + f + " @ " + a + " [" + (b == null ? "?" : b.getName()) + "] refs=" + refs.length + " : " + sb);
                }
                count++;
                a = a.add(1);
            }
        }
        // Instrucciones con el inmediato WM_MOUSEWHEEL (0x20A)
        InstructionIterator it = currentProgram.getListing().getInstructions(true);
        int n = 0;
        while (it.hasNext() && n < 30) {
            Instruction ins = it.next();
            for (int i = 0; i < ins.getNumOperands(); i++) {
                for (Object o : ins.getOpObjects(i)) {
                    if (o instanceof Scalar && ((Scalar) o).getUnsignedValue() == 0x20A) {
                        Function fn = getFunctionContaining(ins.getAddress());
                        println("WM_MOUSEWHEEL " + ins.getAddress() + " " + ins + " in " + (fn == null ? "?" : fn.getName()));
                        n++;
                    }
                }
            }
        }
    }
}
