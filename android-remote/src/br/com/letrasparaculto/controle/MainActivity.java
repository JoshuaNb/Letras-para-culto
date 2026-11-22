package br.com.letrasparaculto.controle;

import android.app.Activity;
import android.content.Context;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.InputFilter;
import android.text.InputType;
import android.view.View;
import android.view.WindowInsets;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import org.json.JSONArray;
import org.json.JSONObject;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

@SuppressWarnings("deprecation") // API 26-compatible system-bar APIs; insets are applied explicitly below.
public final class MainActivity extends Activity {
    private static final int BACKGROUND = Color.rgb(23, 25, 29), PANEL = Color.rgb(35, 39, 45);
    private static final int TEXT = Color.rgb(239, 241, 244), MUTED = Color.rgb(180, 188, 200);
    private static final int ACCENT = Color.rgb(99, 201, 184), DANGER = Color.rgb(255, 161, 178);
    private final Handler ui = new Handler(Looper.getMainLooper());
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private final RemoteClient client = new RemoteClient();
    private RemoteConnection connection;
    private LinearLayout root, connectPanel, controller, slides;
    private TextView status, liveTitle, position, lyrics, connectedHost, connectionHelp;
    private EditText address, pairCode;
    private Button connect, next, previous, blackout, hide, pulse;
    private volatile boolean resumed, destroyed;
    private volatile int generation;
    private boolean busy, commandPending;
    private String lastSlides = "";
    private final Runnable poll = this::refresh;

    @Override public void onCreate(Bundle savedState) {
        super.onCreate(savedState);
        getWindow().setStatusBarColor(BACKGROUND);
        getWindow().setNavigationBarColor(BACKGROUND);
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        scroll.setBackgroundColor(BACKGROUND);
        root = vertical();
        root.setPadding(dp(20), dp(20), dp(20), dp(20));
        root.setOnApplyWindowInsetsListener((view, insets) -> {
            view.setPadding(dp(20) + insets.getSystemWindowInsetLeft(), dp(16) + insets.getSystemWindowInsetTop(), dp(20) + insets.getSystemWindowInsetRight(), dp(16) + insets.getSystemWindowInsetBottom());
            return insets;
        });
        scroll.addView(root, new ScrollView.LayoutParams(-1, -2));
        setContentView(scroll);
        TextView mark = text("W  /  WORSHIP STUDIO", 12, ACCENT, true);
        root.addView(mark);
        root.addView(spacer(8));
        root.addView(text("Controle do culto", 27, TEXT, true));
        TextView caption = text("Letras para culto · Rede local", 13, MUTED, false);
        root.addView(caption);
        status = text("Conecte o celular e o computador ao mesmo Wi-Fi.", 13, MUTED, false);
        status.setPadding(0, dp(16), 0, dp(16));
        status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        root.addView(status);
        buildConnectionPanel();
        buildController();
        TextView footer = text("1.4.0 · Gratuito, sem conta e sem nuvem", 11, MUTED, false);
        footer.setPadding(0, dp(20), 0, 0);
        root.addView(footer);
    }

    private void buildConnectionPanel() {
        connectPanel = card();
        connectPanel.addView(text("CONECTAR AO COMPUTADOR", 11, MUTED, true));
        connectPanel.addView(spacer(10));
        connectionHelp = text("No PC, abra Conexões e ative o controle remoto. Informe abaixo o endereço e os seis números exibidos.", 14, TEXT, false);
        connectPanel.addView(connectionHelp);
        connectPanel.addView(spacer(16));
        connectPanel.addView(text("Endereço do PC", 12, MUTED, false));
        address = edit("192.168.1.10:8765", InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_URI);
        address.setContentDescription("Endereço IPv4 do computador ou link completo de controle");
        address.setText(getPreferences(MODE_PRIVATE).getString("host", ""));
        address.setSaveEnabled(false);
        address.setImportantForAutofill(View.IMPORTANT_FOR_AUTOFILL_NO);
        address.setImeOptions(android.view.inputmethod.EditorInfo.IME_FLAG_NO_PERSONALIZED_LEARNING);
        connectPanel.addView(address, match(-2));
        connectPanel.addView(spacer(12));
        connectPanel.addView(text("Código de conexão", 12, MUTED, false));
        pairCode = edit("6 números mostrados no PC", InputType.TYPE_CLASS_NUMBER);
        pairCode.setFilters(new InputFilter[]{new InputFilter.LengthFilter(6)});
        pairCode.setSaveEnabled(false);
        pairCode.setImportantForAutofill(View.IMPORTANT_FOR_AUTOFILL_NO);
        pairCode.setImeOptions(android.view.inputmethod.EditorInfo.IME_FLAG_NO_PERSONALIZED_LEARNING);
        pairCode.setContentDescription("Código de conexão de seis dígitos mostrado no computador");
        connectPanel.addView(pairCode, match(-2));
        connectPanel.addView(spacer(16));
        connect = button("Conectar", true);
        connect.setOnClickListener(v -> connect());
        connectPanel.addView(connect, match(54));
        connectPanel.addView(spacer(12));
        connectPanel.addView(text("Você também pode colar o link completo do PC, com a chave de acesso. A chave permanece apenas nesta sessão do aplicativo.", 12, MUTED, false));
        root.addView(connectPanel, match(-2));
    }

    private void buildController() {
        controller = vertical();
        controller.setVisibility(View.GONE);
        connectedHost = text("", 12, MUTED, false);
        connectedHost.setPadding(0, 0, 0, dp(14));
        controller.addView(connectedHost);
        LinearLayout live = card();
        live.addView(text("AO VIVO", 11, ACCENT, true));
        liveTitle = text("Sem apresentação", 19, TEXT, true);
        liveTitle.setPadding(0, dp(9), 0, dp(5));
        live.addView(liveTitle);
        position = text("Selecione um louvor no computador", 12, MUTED, false);
        live.addView(position);
        lyrics = text("", 21, TEXT, false);
        lyrics.setPadding(0, dp(18), 0, dp(8));
        lyrics.setTextIsSelectable(true);
        lyrics.setMaxLines(10);
        live.addView(lyrics);
        controller.addView(live, match(-2));
        controller.addView(spacer(16));
        previous = button("← Anterior", false);
        next = button("Próxima →", true);
        previous.setOnClickListener(v -> command("previous", null));
        next.setOnClickListener(v -> command("next", null));
        controller.addView(row(previous, next, 64));
        controller.addView(spacer(10));
        blackout = button("Tela preta", false);
        hide = button("Ocultar letra", false);
        blackout.setOnClickListener(v -> command("blackout", null));
        hide.setOnClickListener(v -> command("clear", null));
        controller.addView(row(blackout, hide, 54));
        controller.addView(spacer(10));
        pulse = button("Pulso de efeito", false);
        pulse.setOnClickListener(v -> command("tap", null));
        controller.addView(pulse, match(52));
        TextView sequence = text("ESTROFES DA APRESENTAÇÃO", 11, MUTED, true);
        sequence.setPadding(0, dp(24), 0, dp(12));
        controller.addView(sequence);
        slides = vertical();
        controller.addView(slides, match(-2));
        Button disconnect = button("Desconectar", false);
        disconnect.setTextColor(DANGER);
        disconnect.setOnClickListener(v -> disconnect("Desconectado. A projeção no computador continua disponível."));
        LinearLayout.LayoutParams disconnectLayout = match(48);
        disconnectLayout.topMargin = dp(20);
        controller.addView(disconnect, disconnectLayout);
        root.addView(controller, match(-2));
    }

    private void connect() {
        if (busy) return;
        final RemoteConnection parsed;
        final String code = pairCode.getText().toString().trim();
        try {
            parsed = RemoteConnection.parse(address.getText().toString());
            if (parsed.token.isEmpty() && !RemoteConnection.isPairCode(code)) throw new IllegalArgumentException("Digite o código de seis números mostrado no PC.");
        } catch (IllegalArgumentException error) { message(error.getMessage(), true); return; }
        address.setText(parsed.origin); // Clear any pasted credential immediately; never save it.
        busy = true;
        connect.setEnabled(false);
        message("Conectando ao computador…", false);
        int current = ++generation;
        worker.execute(() -> {
            if (!valid(current) || !resumed) return;
            try {
                RemoteConnection ready = parsed.token.isEmpty() ? client.pair(parsed, code) : parsed;
                if (!valid(current) || !resumed) return;
                JSONObject state = client.state(ready);
                ui.post(() -> {
                    if (!valid(current)) return;
                    connection = ready;
                    busy = false;
                    connect.setEnabled(true);
                    pairCode.setText("");
                    getPreferences(MODE_PRIVATE).edit().putString("host", ready.origin).apply();
                    connectPanel.setVisibility(View.GONE);
                    controller.setVisibility(View.VISIBLE);
                    connectedHost.setText(ready.origin);
                    ((InputMethodManager)getSystemService(Context.INPUT_METHOD_SERVICE)).hideSoftInputFromWindow(address.getWindowToken(), 0);
                    updateState(state);
                    schedule();
                });
            } catch (Exception error) { ui.post(() -> failedConnect(current, error)); }
        });
    }

    private void failedConnect(int current, Exception error) {
        if (!valid(current)) return;
        busy = false;
        connect.setEnabled(true);
        message(friendly(error), true);
    }

    private void refresh() {
        if (!resumed || destroyed || connection == null || busy) return;
        busy = true;
        int current = generation;
        RemoteConnection selected = connection;
        worker.execute(() -> {
            if (!valid(current) || !resumed) return;
            try {
                JSONObject state = client.state(selected);
                ui.post(() -> {
                    if (!valid(current)) return;
                    if (!commandPending) busy = false;
                    updateState(state);
                    schedule();
                });
            } catch (Exception error) {
                ui.post(() -> {
                    if (!valid(current)) return;
                    if (!commandPending) busy = false;
                    if (error instanceof RemoteClient.AccessExpiredException) disconnect(error.getMessage());
                    else { controls(false); message(friendly(error) + " Tentando reconectar…", true); schedule(); }
                });
            }
        });
    }

    private void command(String action, Integer index) {
        if (connection == null || commandPending || !resumed) return;
        busy = true;
        commandPending = true;
        controls(false);
        ui.removeCallbacks(poll);
        int current = generation;
        RemoteConnection selected = connection;
        worker.execute(() -> {
            // A task queued behind a poll must not start after pause/disconnection.
            if (!valid(current) || !resumed) return;
            try {
                client.command(selected, action, index);
                if (!valid(current) || !resumed) return;
                JSONObject state = client.state(selected);
                ui.post(() -> {
                    if (!valid(current)) return;
                    busy = false;
                    commandPending = false;
                    updateState(state);
                    schedule();
                });
            } catch (Exception error) {
                ui.post(() -> {
                    if (!valid(current)) return;
                    busy = false;
                    commandPending = false;
                    if (error instanceof RemoteClient.AccessExpiredException) disconnect(error.getMessage());
                    else {
                        controls(false);
                        message("Não foi possível confirmar o comando. Confira a saída no PC. " + friendly(error), true);
                        schedule(); // A toggle command is never automatically resent.
                    }
                });
            }
        });
    }

    private void updateState(JSONObject state) {
        liveTitle.setText(state.optString("title", "Sem apresentação"));
        int count = state.optInt("count", 0), index = Math.max(0, state.optInt("index", 0));
        position.setText(count > 0 ? "Estrofe " + (index + 1) + " de " + count : "Selecione e projete um louvor no computador");
        boolean black = state.optBoolean("blackout", false), hidden = state.optBoolean("hideLyrics", false);
        String text = state.optString("text", "");
        lyrics.setText(black ? "Tela preta" : hidden ? "Letra oculta" : text.isEmpty() ? "Sem letra ao vivo" : text);
        lyrics.setTextColor(black || hidden ? MUTED : TEXT);
        blackout.setText(black ? "Restaurar tela" : "Tela preta");
        blackout.setBackground(shape(black ? Color.rgb(83, 41, 52) : PANEL, black ? DANGER : Color.rgb(62, 68, 77)));
        blackout.setTextColor(black ? DANGER : TEXT);
        hide.setText(hidden ? "Mostrar letra" : "Ocultar letra");
        hide.setBackground(shape(hidden ? Color.rgb(43, 73, 67) : PANEL, hidden ? ACCENT : Color.rgb(62, 68, 77)));
        JSONArray source = state.optJSONArray("slides");
        String key = (source == null ? "[]" : source.toString()) + ":" + index;
        if (!key.equals(lastSlides)) {
            lastSlides = key;
            slides.removeAllViews();
            int length = source == null ? 0 : Math.min(source.length(), 10001);
            for (int i = 0; i < length; i++) {
                JSONObject slide = source.optJSONObject(i);
                if (slide == null) continue;
                final int target = i;
                Button item = button(String.format(java.util.Locale.ROOT, "%02d  %s\n%s", i + 1, slide.optString("label", "Estrofe"), slide.optString("text", "")), false);
                item.setAllCaps(false);
                item.setTextSize(14);
                item.setGravity(android.view.Gravity.START | android.view.Gravity.CENTER_VERTICAL);
                item.setMinHeight(dp(80));
                item.setMaxLines(8);
                item.setBackground(shape(i == index ? Color.rgb(43, 73, 67) : PANEL, i == index ? ACCENT : Color.rgb(62, 68, 77)));
                item.setOnClickListener(v -> command("slide", target));
                LinearLayout.LayoutParams layout = match(-2);
                layout.bottomMargin = dp(9);
                slides.addView(item, layout);
            }
            if (length == 0) slides.addView(text("As estrofes aparecem aqui quando o PC inicia uma apresentação.", 13, MUTED, false));
        }
        controls(true);
        message("● Conectado ao computador", false);
    }

    private void controls(boolean enabled) {
        enabled = enabled && !commandPending;
        for (Button button : new Button[]{next, previous, blackout, hide, pulse}) button.setEnabled(enabled);
        for (int i = 0; i < slides.getChildCount(); i++) slides.getChildAt(i).setEnabled(enabled);
    }

    private void disconnect(String reason) {
        ++generation;
        busy = false;
        commandPending = false;
        connection = null;
        client.cancel();
        ui.removeCallbacks(poll);
        connectPanel.setVisibility(View.VISIBLE);
        controller.setVisibility(View.GONE);
        connect.setEnabled(true);
        lastSlides = "";
        message(reason, false);
    }

    private String friendly(Exception error) {
        if (error instanceof RemoteClient.AccessExpiredException) return error.getMessage();
        if (error instanceof java.net.SocketTimeoutException || error instanceof java.net.ConnectException || error instanceof java.net.NoRouteToHostException)
            return "Sem resposta do PC. Confira o Wi-Fi, o endereço e se o controle remoto está ativo.";
        if (error instanceof org.json.JSONException) return "A resposta do computador não é compatível com este controle.";
        if (error instanceof java.io.IOException || error instanceof IllegalArgumentException) {
            String value = error.getMessage();
            if (value != null && (value.startsWith("Digite ") || value.startsWith("Muitas ") || value.startsWith("Atualize ") || value.startsWith("O computador ") || value.startsWith("A resposta "))) return value;
            return "Conexão interrompida. Confira o controle remoto no computador.";
        }
        return "Não foi possível conectar. Confira as informações no PC.";
    }

    private boolean valid(int current) { return !destroyed && generation == current; }
    private void schedule() { ui.removeCallbacks(poll); if (resumed && connection != null && !destroyed && !commandPending) ui.postDelayed(poll, 900); }
    private void message(String value, boolean error) { status.setText(value); status.setTextColor(error ? DANGER : connection == null ? MUTED : ACCENT); }

    @Override protected void onResume() { super.onResume(); resumed = true; schedule(); }
    @Override protected void onPause() {
        resumed = false;
        ++generation;
        busy = false;
        commandPending = false;
        connect.setEnabled(true);
        ui.removeCallbacks(poll);
        client.cancel();
        super.onPause();
    }
    @Override protected void onDestroy() { destroyed = true; ++generation; ui.removeCallbacksAndMessages(null); client.cancel(); worker.shutdownNow(); connection = null; super.onDestroy(); }

    private LinearLayout vertical() { LinearLayout layout = new LinearLayout(this); layout.setOrientation(LinearLayout.VERTICAL); return layout; }
    private LinearLayout card() { LinearLayout layout = vertical(); layout.setPadding(dp(17), dp(18), dp(17), dp(18)); layout.setBackground(shape(PANEL, Color.rgb(57, 62, 69))); return layout; }
    private LinearLayout.LayoutParams match(int height) { return new LinearLayout.LayoutParams(-1, height < 0 ? height : dp(height)); }
    private View spacer(int height) { View view = new View(this); view.setLayoutParams(new LinearLayout.LayoutParams(1, dp(height))); return view; }
    private TextView text(String value, int size, int color, boolean bold) { TextView text = new TextView(this); text.setText(value); text.setTextSize(size); text.setTextColor(color); text.setFontFeatureSettings("kern"); if (bold) text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL)); text.setLineSpacing(dp(3), 1); return text; }
    private EditText edit(String hint, int inputType) { EditText edit = new EditText(this); edit.setHint(hint); edit.setTextColor(TEXT); edit.setHintTextColor(MUTED); edit.setTextSize(16); edit.setInputType(inputType); edit.setSingleLine(true); edit.setPadding(dp(12), dp(12), dp(12), dp(12)); edit.setBackground(shape(BACKGROUND, Color.rgb(75, 83, 94))); edit.setBackgroundTintList(null); return edit; }
    private Button button(String label, boolean primary) { Button button = new Button(this); button.setText(label); button.setAllCaps(false); button.setTextSize(15); button.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL)); button.setTextColor(primary ? Color.rgb(16, 39, 35) : TEXT); button.setPadding(dp(12), dp(12), dp(12), dp(12)); button.setMinHeight(dp(48)); button.setBackground(shape(primary ? ACCENT : PANEL, primary ? ACCENT : Color.rgb(62, 68, 77))); button.setBackgroundTintList(null); return button; }
    private LinearLayout row(Button first, Button second, int height) { LinearLayout row = new LinearLayout(this); LinearLayout.LayoutParams left = new LinearLayout.LayoutParams(0, dp(height), 1); left.rightMargin = dp(5); LinearLayout.LayoutParams right = new LinearLayout.LayoutParams(0, dp(height), 1); right.leftMargin = dp(5); row.addView(first, left); row.addView(second, right); return row; }
    private GradientDrawable shape(int fill, int border) { GradientDrawable shape = new GradientDrawable(); shape.setColor(fill); shape.setCornerRadius(dp(8)); shape.setStroke(dp(1), border); return shape; }
    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }
}
