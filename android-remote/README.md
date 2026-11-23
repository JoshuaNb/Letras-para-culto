# Controle Android — Letras para culto 1.8.0

Aplicativo nativo Android para operar o Worship Studio na mesma rede local. Gratuito, sem conta, anúncios, nuvem, serviços Google ou dependências externas. Requer Android 8.0 ou superior. A interface é em português brasileiro.

## Conectar

1. Conecte o celular e o PC à mesma rede Wi-Fi/rede local. No Worship Studio 1.8.0, abra **Ferramentas → Conexões e Holyrics** e inicie o controle remoto. Nas versões anteriores, use a aba **Conexões**.
2. No aplicativo Android, informe o IPv4 e a porta mostrados no PC, por exemplo `192.168.1.10:8765`, e o código de seis números.
3. Toque em **Conectar**. Também é possível colar o link completo com a chave de acesso, compatível com o controle das versões anteriores.
4. Use Próxima, Anterior, Tela preta, Ocultar letra, Pulso de efeito ou toque diretamente na estrofe desejada.

O programa do PC continua responsável pela projeção, biblioteca, áudio e cenários. O celular acompanha a apresentação atual. Ao desconectar, a projeção continua no computador. Desligar e ligar o controle remoto no PC revoga o acesso anterior e gera novas credenciais.

O aplicativo pausa as consultas quando fica em segundo plano e retoma ao voltar. Se o PC perder a conexão, os comandos ficam desativados até receber um novo estado. Comandos que alternam tela preta e letra oculta não são reenviados automaticamente após falhas, evitando executar a mesma ação duas vezes.

## Enviar arquivos ao PC

Requer o programa de PC 1.6.0 ou superior, com o controle remoto ativo e o celular conectado. No cartão **Arquivos para o PC**, toque em **Enviar arquivos ao PC** e escolha até dez documentos, imagens, vídeos ou outros arquivos locais no seletor do Android. O aplicativo recebe acesso somente aos itens selecionados; não pede acesso geral ao armazenamento. O seletor solicita arquivos locais, para manter a operação sem nuvem ou internet.

Cada arquivo pode ter até **512 MB**. O envio ocorre um arquivo por vez, com nome, quantidade e progresso; tamanho desconhecido pelo provedor aparece como progresso indeterminado e continua sujeito ao mesmo limite. O transporte usa um buffer de 16 KiB e calcula SHA256 durante a transmissão. Só confirma sucesso quando o PC retorna o tamanho e SHA256 correspondentes. O programa de PC guarda os arquivos em **Recebidos**, sem executá-los ou projetá-los automaticamente.

Os comandos e as consultas da apresentação usam um trabalhador e conexão separados; o upload não fica à frente de Próxima, Tela preta e outros comandos. **Cancelar envio**, desconectar, sair do aplicativo ou fechar a Activity interrompe o lote. Os arquivos já concluídos permanecem no PC. Ao retornar, os arquivos não são reenviados automaticamente: confira **Recebidos** antes de selecionar novamente, principalmente se a conexão caiu depois da transmissão e antes da confirmação.

Uma seleção feita pelo seletor do Android começa somente quando a Activity volta ao primeiro plano, usando a mesma conexão pareada. Uma reconexão ao PC cancela a seleção anterior. O aplicativo não guarda URI, token ou lote pendente no estado persistente da Activity. Nomes com caracteres inválidos do Windows são recusados; arquivos com nomes iguais podem ser recebidos sem substituir os anteriores. Se a pasta recebida atingir a cota ou faltar espaço no PC, o aplicativo informa a recusa.

O protocolo é `GET /files` para consultar a recepção e `POST /files` com bytes binários, `Authorization: Bearer`, nome UTF-8 em `X-File-Name` e tamanho HTTP conhecido ou codificação chunked. Redirecionamentos ficam desativados. O limite de resposta é 256 KiB e as credenciais permanecem no mesmo IPv4 local escolhido pelo operador.

Somente o endereço do PC é lembrado. O token e o código não são salvos em preferências, backups, logs nem no estado da Activity. Nenhuma permissão de câmera, microfone ou armazenamento é solicitada. O tráfego HTTP fica restrito a IPv4 privados/link-local/loopback informados pelo operador, sem resolução DNS nem redirecionamento para outro servidor. Use uma rede local de confiança.

## Compilar e assinar

Requer JDK 21, Android SDK Platform 36 e Build Tools 36.0.0. Não precisa de Gradle, Maven, Android Studio, NuGet ou acesso à internet depois de instalar essas ferramentas.

```powershell
.\test.ps1 -JdkRoot C:\ferramentas\jdk-21
.\build.ps1 -AndroidSdkRoot C:\ferramentas\android-sdk -JdkRoot C:\ferramentas\jdk-21 -KeyStorePath C:\assinatura-privada\letras-controle.jks
```

As senhas devem estar nas variáveis `WORSHIP_ANDROID_STORE_PASSWORD` e `WORSHIP_ANDROID_KEY_PASSWORD`. O alias padrão é `worshipstudio-remote`. O script monta o APK com `javac`, `aapt2`, `d8`, `zipalign` e `apksigner`, verifica assinatura e alinhamento e mostra o SHA-256. O APK final fica ao lado dos instaladores de PC, com nome `LetrasParaCulto-Controle-1.8.0.apk`.

Mantenha o keystore e as senhas fora do repositório e do ZIP público; faça backup privado. As próximas versões Android devem usar a mesma chave para permitir atualização sobre a instalação anterior. Não publique um APK assinado com chave de debug como distribuição definitiva.

Os testes Java verificam interpretação do link, faixa IPv4 local, porta, token, código, rejeição de credenciais embutidas, DNS/endereço público e parâmetros inesperados. Compilação e assinatura não substituem validação em celular físico; os resultados da distribuição devem distinguir os testes realmente executados.

Na distribuição 1.7.0, os quatro arquivos Java e o DEX são idênticos aos da 1.6.0; apenas `versionName` e `versionCode` do pacote foram atualizados para `1.7.0` e `10700`. O APK foi recompilado e assinado com o mesmo certificado, com assinatura, alinhamento, versão e licença conferidos. O registro está em [verification/build-1.7.0.json](verification/build-1.7.0.json). Essa versão mantém o controle e a transferência já disponíveis na 1.6 e não exige substituir um APK 1.6 para operar o PC 1.7. A Activity e a interação em celular físico permanecem sem validação observada.

Na distribuição 1.8.0, os quatro arquivos Java e o DEX continuam idênticos aos da 1.7.0; `versionName` e `versionCode` são `1.8.0` e `10800`. O APK foi recompilado e assinado com o mesmo certificado, com assinatura, alinhamento, manifesto e licença conferidos. O relatório é [verification/build-1.8.0.json](verification/build-1.8.0.json). APKs 1.6 e 1.7 continuam operando o PC 1.8 e enviando arquivos; os novos fundos e modos gráficos são implementados no desktop. Não há nova alegação de execução da Activity em Android físico.

Na distribuição 1.4.0 passaram também **17 verificações HTTP reais** usando os arquivos de transporte Java exatos do APK na JVM, o servidor de produção e a operação/projeção desktop reais no backend Avalonia Headless. Foram exercitados pareamento, navegação, seleção, tela preta, ocultar letra, pulso manual, autenticação, revogação e reconexão. Os registros e o código dessa prova acompanham `test-evidence/android-1.4` no ZIP completo. A renderização e o toque na Activity Android não foram validados por essa prova; emulador sem hipervisor não substitui um teste concluído.

Referências de compatibilidade: [permissão de rede local](https://developer.android.com/privacy-and-security/local-network-permission) e [insets das barras do sistema](https://developer.android.com/develop/ui/views/layout/insets), documentação oficial Android. O alvo SDK 36 usa a permissão INTERNET para a conexão local; uma futura mudança para SDK 37 deve revisar a permissão de rede local.
