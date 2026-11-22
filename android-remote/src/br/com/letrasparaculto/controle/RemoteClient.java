package br.com.letrasparaculto.controle;

import org.json.JSONException;
import org.json.JSONObject;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;

/** All calls are made by the activity's worker; credentials never leave the chosen LAN origin. */
public final class RemoteClient {
    private volatile HttpURLConnection active;

    public JSONObject state(RemoteConnection connection) throws IOException, JSONException {
        return request(connection, "/state", null, true);
    }

    public RemoteConnection pair(RemoteConnection connection, String code) throws IOException, JSONException {
        if (!RemoteConnection.isPairCode(code)) throw new IOException("Digite os seis números mostrados no PC.");
        JSONObject response = request(connection, "/pair", new JSONObject().put("code", code), false);
        return connection.withToken(response.optString("token", ""));
    }

    public void command(RemoteConnection connection, String command, Integer index) throws IOException, JSONException {
        JSONObject body = new JSONObject().put("command", command);
        if (index != null) body.put("index", index);
        request(connection, "/command", body, true);
    }

    public void cancel() { HttpURLConnection connection = active; if (connection != null) connection.disconnect(); }

    private JSONObject request(RemoteConnection connection, String path, JSONObject body, boolean authenticated) throws IOException, JSONException {
        HttpURLConnection request = (HttpURLConnection) new URL(connection.origin + path).openConnection();
        active = request;
        try {
            request.setConnectTimeout(4000);
            request.setReadTimeout(5000);
            request.setInstanceFollowRedirects(false);
            request.setUseCaches(false);
            request.setRequestProperty("Accept", "application/json");
            if (authenticated) request.setRequestProperty("Authorization", "Bearer " + connection.token);
            if (body != null) {
                byte[] bytes = body.toString().getBytes(StandardCharsets.UTF_8);
                request.setRequestMethod("POST");
                request.setRequestProperty("Content-Type", "application/json; charset=utf-8");
                request.setFixedLengthStreamingMode(bytes.length);
                request.setDoOutput(true);
                try (java.io.OutputStream stream = request.getOutputStream()) { stream.write(bytes); }
            }
            int status = request.getResponseCode();
            if (status == 401 && path.equals("/pair")) throw new IOException("O computador não aceitou o código. Confira os seis números mostrados no PC.");
            if (status == 401) throw new AccessExpiredException("Acesso expirado. Conecte novamente com o código atual do PC.");
            if (status == 429) throw new IOException("Muitas tentativas. Aguarde um minuto e confira o código no PC.");
            if (status == 404 && path.equals("/pair")) throw new IOException("Atualize o PC para 1.4.0 ou cole o link completo com a chave de acesso.");
            if (status < 200 || status >= 300) throw new IOException("O computador não aceitou a solicitação (HTTP " + status + ").");
            if (request.getContentLengthLong() > 2 * 1024 * 1024) throw new IOException("A resposta do computador excedeu o limite permitido.");
            try (InputStream stream = request.getInputStream(); ByteArrayOutputStream output = new ByteArrayOutputStream()) {
                byte[] buffer = new byte[8192];
                int read;
                while ((read = stream.read(buffer)) != -1) {
                    if (output.size() + read > 2 * 1024 * 1024) throw new IOException("A resposta do computador excedeu o limite permitido.");
                    output.write(buffer, 0, read);
                }
                return new JSONObject(new String(output.toByteArray(), StandardCharsets.UTF_8));
            }
        } finally {
            if (active == request) active = null;
            request.disconnect();
        }
    }

    public static final class AccessExpiredException extends IOException {
        public AccessExpiredException(String message) { super(message); }
    }
}
