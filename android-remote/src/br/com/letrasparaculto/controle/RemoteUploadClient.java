package br.com.letrasparaculto.controle;

import org.json.JSONException;
import org.json.JSONObject;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;

/** Streaming transfer on its own worker/connection, independent of presentation commands. */
public final class RemoteUploadClient {
    public static final long MAX_FILE_BYTES = 512L * 1024 * 1024;
    private volatile HttpURLConnection active;
    private volatile InputStream activeInput;

    public interface Progress { void onProgress(long sent, long total); }
    public interface Cancellation { boolean isCancelled(); }

    public JSONObject files(RemoteConnection connection) throws IOException, JSONException {
        return files(connection, null);
    }

    public JSONObject files(RemoteConnection connection, Cancellation cancellation) throws IOException, JSONException {
        cancelled(cancellation);
        HttpURLConnection request = open(connection);
        try { cancelled(cancellation); JSONObject result = response(request); cancelled(cancellation); return result; }
        finally { if (active == request) active = null; request.disconnect(); }
    }

    /** Takes ownership of source and closes it on success, failure, or cancellation. */
    public JSONObject upload(RemoteConnection connection, String name, long length, InputStream source,
                             Progress progress, Cancellation cancellation) throws IOException, JSONException {
        if (source == null) throw new IOException("Não foi possível ler o arquivo selecionado.");
        try (InputStream input = source) {
            validateName(name);
            if (length < -1 || length > MAX_FILE_BYTES) throw new IOException("Cada arquivo pode ter até 512 MB.");
            cancelled(cancellation);
            final MessageDigest digest;
            try { digest = MessageDigest.getInstance("SHA-256"); }
            catch (java.security.NoSuchAlgorithmException error) { throw new IOException("Não foi possível verificar a integridade do arquivo."); }
            HttpURLConnection request = open(connection);
            activeInput = input;
            try {
                request.setRequestMethod("POST");
                request.setRequestProperty("Content-Type", "application/octet-stream");
                request.setRequestProperty("X-File-Name", URLEncoder.encode(name, "UTF-8").replace("+", "%20"));
                request.setDoOutput(true);
                if (length >= 0) request.setFixedLengthStreamingMode(length);
                else request.setChunkedStreamingMode(64 * 1024);
                long sent = 0;
                if (progress != null) progress.onProgress(0, length);
                cancelled(cancellation);
                try (OutputStream output = request.getOutputStream()) {
                    byte[] buffer = new byte[16 * 1024];
                    while (true) {
                        cancelled(cancellation);
                        int read = input.read(buffer);
                        cancelled(cancellation);
                        if (read == -1) break;
                        if (read == 0) continue;
                        if (sent + read > MAX_FILE_BYTES) throw new IOException("Cada arquivo pode ter até 512 MB.");
                        if (length >= 0 && sent + read > length) throw new IOException("O tamanho do arquivo mudou. Selecione-o novamente.");
                        output.write(buffer, 0, read);
                        digest.update(buffer, 0, read);
                        sent += read;
                        if (progress != null) progress.onProgress(sent, length);
                    }
                    if (length >= 0 && sent != length) throw new IOException("A leitura do arquivo ficou incompleta. Selecione-o novamente.");
                }
                cancelled(cancellation);
                JSONObject result = response(request);
                JSONObject file = result.optJSONObject("file");
                StringBuilder hash = new StringBuilder(64);
                for (byte value : digest.digest()) hash.append(String.format(java.util.Locale.ROOT, "%02x", value & 255));
                if (!result.optBoolean("ok", false) || file == null || file.optLong("size", -1) != sent || !hash.toString().equalsIgnoreCase(file.optString("sha256", "")))
                    throw new IOException("O computador não confirmou o arquivo completo. Confira Recebidos no PC antes de tentar novamente.");
                return result;
            } finally {
                if (activeInput == input) activeInput = null;
                if (active == request) active = null;
                request.disconnect();
            }
        }
    }

    private HttpURLConnection open(RemoteConnection connection) throws IOException {
        if (connection == null || connection.token.isEmpty()) throw new IOException("Conecte o celular ao PC antes de enviar arquivos.");
        HttpURLConnection request = (HttpURLConnection)new URL(connection.origin + "/files").openConnection();
        active = request;
        request.setConnectTimeout(4000);
        request.setReadTimeout(60000);
        request.setInstanceFollowRedirects(false);
        request.setUseCaches(false);
        request.setRequestProperty("Accept", "application/json");
        request.setRequestProperty("Authorization", "Bearer " + connection.token);
        return request;
    }

    private JSONObject response(HttpURLConnection request) throws IOException, JSONException {
        int status = request.getResponseCode();
        if (status == 401) throw new RemoteClient.AccessExpiredException("Acesso expirado. Conecte novamente com o código atual do PC.");
        if (status == 404) throw new IOException("Atualize o programa do PC para 1.6.0 ou superior para receber arquivos.");
        if (status == 413) throw new IOException("O computador recusou o tamanho do arquivo. O limite é 512 MB por arquivo.");
        if (status == 429) throw new IOException("O computador está recebendo outro arquivo. Aguarde esse envio terminar.");
        if (status == 507) throw new IOException("A pasta Recebidos está cheia ou o PC está sem espaço. Libere espaço no computador.");
        if (status == 400) throw new IOException("O computador recusou o nome ou os dados do arquivo. Confira o nome selecionado.");
        if (status < 200 || status >= 300) throw new IOException("O computador não confirmou o envio (HTTP " + status + ").");
        if (request.getContentLengthLong() > 256 * 1024) throw new IOException("A resposta do computador excedeu o limite permitido.");
        try (InputStream input = request.getInputStream(); ByteArrayOutputStream bytes = new ByteArrayOutputStream()) {
            byte[] buffer = new byte[4096];
            int read;
            while ((read = input.read(buffer)) != -1) {
                if (bytes.size() + read > 256 * 1024) throw new IOException("A resposta do computador excedeu o limite permitido.");
                bytes.write(buffer, 0, read);
            }
            return new JSONObject(new String(bytes.toByteArray(), StandardCharsets.UTF_8));
        }
    }

    public void cancel() {
        HttpURLConnection request = active;
        if (request != null) request.disconnect();
        InputStream input = activeInput;
        if (input != null) { try { input.close(); } catch (IOException ignored) { } }
    }

    private static void cancelled(Cancellation cancellation) throws CancelledException {
        if (Thread.currentThread().isInterrupted() || cancellation != null && cancellation.isCancelled()) throw new CancelledException();
    }

    public static void validateName(String name) throws IOException {
        if (name == null || name.trim().isEmpty() || name.length() > 180 || name.endsWith(".") || name.endsWith(" "))
            throw new IOException("Escolha um arquivo com nome de até 180 caracteres, sem ponto ou espaço no final.");
        for (int i = 0; i < name.length(); i++) {
            char value = name.charAt(i);
            if (Character.isISOControl(value) || "<>:\"/\\|?*".indexOf(value) >= 0)
                throw new IOException("O nome do arquivo contém caracteres que o Windows não aceita.");
        }
        String stem = name.split("\\.", 2)[0].toUpperCase(java.util.Locale.ROOT);
        if (stem.matches("CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]")) throw new IOException("O nome do arquivo é reservado pelo Windows. Renomeie-o antes de enviar.");
    }

    public static final class CancelledException extends IOException {
        public CancelledException() { super("Envio cancelado. Arquivos já concluídos permanecem em Recebidos no PC."); }
    }
}
