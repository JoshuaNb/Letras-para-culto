import br.com.letrasparaculto.controle.RemoteConnection;

public final class RemoteConnectionTests {
    private static int passed;
    public static void main(String[] args) {
        equal(RemoteConnection.parse("192.168.1.10:8765").origin, "http://192.168.1.10:8765");
        equal(RemoteConnection.parse("http://10.0.2.2:8765/").origin, "http://10.0.2.2:8765");
        equal(RemoteConnection.parse("172.16.0.10").origin, "http://172.16.0.10:8765");
        equal(RemoteConnection.parse("169.254.10.15:9000").origin, "http://169.254.10.15:9000");
        equal(RemoteConnection.parse("127.0.0.1:8765").origin, "http://127.0.0.1:8765");
        String token = "0123456789abcdef0123456789abcdef0123456789abcdef";
        RemoteConnection full = RemoteConnection.parse("http://192.168.1.10:8765/?token=" + token);
        equal(full.token, token);
        equal(full.origin, "http://192.168.1.10:8765");
        equal(RemoteConnection.parse("192.168.1.10").withToken(token.toUpperCase(java.util.Locale.ROOT)).token, token);
        reject("http://example.com:8765/");
        reject("http://8.8.8.8:8765/");
        reject("https://192.168.1.10:8765/");
        reject("file:///192.168.1.10");
        reject("http://192.168.1.10:80/");
        reject("http://192.168.1.10:65536/");
        reject("http://user:pass@192.168.1.10:8765/");
        reject("http://192.168.1.10:8765/state");
        reject("http://192.168.1.10:8765/#token=secret");
        reject("http://192.168.1.10:8765/?token=bad");
        reject("http://192.168.1.10:8765/?token=" + token + "&other=anything");
        reject("http://192.168.1.10:8765/?token=" + token + "&token=" + token);
        reject("http://192.168.999.10:8765/");
        reject("http://192.168.01.10:8765/");
        reject("http://172.32.1.10:8765/");
        reject("http://[::1]:8765/");
        reject(" ");
        check(RemoteConnection.isPairCode("123456"));
        check(RemoteConnection.isPairCode("000001"));
        check(!RemoteConnection.isPairCode("12345"));
        check(!RemoteConnection.isPairCode("1234567"));
        check(!RemoteConnection.isPairCode("12345a"));
        check(!RemoteConnection.isPairCode("１２３４５６"));
        check(!RemoteConnection.isPairCode(null));
        System.out.println("RemoteConnection: " + passed + " passed, zero failures.");
    }
    private static void equal(String actual, String expected) { if (!expected.equals(actual)) throw new AssertionError("Parsed connection differs."); passed++; }
    private static void check(boolean value) { if (!value) throw new AssertionError("Validation failed."); passed++; }
    private static void reject(String value) { try { RemoteConnection.parse(value); } catch (IllegalArgumentException expected) { passed++; return; } throw new AssertionError("Unsafe connection accepted: " + value); }
}
