package br.com.letrasparaculto.controle;

import java.net.URI;
import java.net.URISyntaxException;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.util.Locale;

/** Parses only literal private/link-local/loopback IPv4 addresses. Never resolves DNS. */
public final class RemoteConnection {
    public final String origin;
    public final String token;

    private RemoteConnection(String origin, String token) {
        this.origin = origin;
        this.token = token;
    }

    public static RemoteConnection parse(String input) {
        if (input == null || input.trim().isEmpty()) throw new IllegalArgumentException("Informe o endereço mostrado no computador.");
        String text = input.trim();
        if (!text.contains("://")) text = "http://" + text;
        final URI uri;
        try { uri = new URI(text); }
        catch (URISyntaxException ex) { throw new IllegalArgumentException("Endereço inválido. Use o IPv4 e a porta mostrados no computador."); }
        if (!"http".equalsIgnoreCase(uri.getScheme()) || uri.getUserInfo() != null || uri.getFragment() != null)
            throw new IllegalArgumentException("Use um endereço HTTP da rede local, sem usuário, senha ou fragmento.");
        String host = uri.getHost();
        if (!isLocalIpv4(host)) throw new IllegalArgumentException("Informe um IPv4 da rede local, como 192.168.1.10.");
        String path = uri.getRawPath();
        if (path != null && !path.isEmpty() && !path.equals("/")) throw new IllegalArgumentException("Copie o endereço principal mostrado no computador.");
        int port = uri.getPort() == -1 ? 8765 : uri.getPort();
        if (port < 1024 || port > 65535) throw new IllegalArgumentException("Use uma porta entre 1024 e 65535.");
        String token = "";
        if (uri.getRawQuery() != null && !uri.getRawQuery().isEmpty()) {
            String[] pairs = uri.getRawQuery().split("&", -1);
            if (pairs.length != 1 || !pairs[0].startsWith("token=")) throw new IllegalArgumentException("O link deve conter apenas a chave de acesso do programa.");
            try { token = URLDecoder.decode(pairs[0].substring(6), StandardCharsets.UTF_8.name()); }
            catch (Exception ex) { throw new IllegalArgumentException("A chave do link é inválida. Copie novamente do computador."); }
            if (!token.matches("[0-9a-fA-F]{48}")) throw new IllegalArgumentException("A chave do link é inválida. Copie novamente do computador.");
            token = token.toLowerCase(Locale.ROOT);
        }
        return new RemoteConnection("http://" + host + ":" + port, token);
    }

    public RemoteConnection withToken(String value) {
        if (value == null || !value.matches("[0-9a-fA-F]{48}")) throw new IllegalArgumentException("O computador retornou uma chave de acesso inválida.");
        return new RemoteConnection(origin, value.toLowerCase(Locale.ROOT));
    }

    public static boolean isPairCode(String value) { return value != null && value.matches("[0-9]{6}"); }

    public static boolean isLocalIpv4(String host) {
        if (host == null || !host.matches("[0-9]{1,3}(\\.[0-9]{1,3}){3}")) return false;
        String[] parts = host.split("\\.");
        int[] octets = new int[4];
        for (int i = 0; i < 4; i++) {
            if (parts[i].length() > 1 && parts[i].startsWith("0")) return false;
            octets[i] = Integer.parseInt(parts[i]);
            if (octets[i] > 255) return false;
        }
        if (octets[0] == 127) return octets[3] != 255;
        if (octets[3] == 0 || octets[3] == 255) return false;
        return octets[0] == 10 || octets[0] == 192 && octets[1] == 168
            || octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31
            || octets[0] == 169 && octets[1] == 254;
    }
}
