# Enviar arquivos do celular ao PC

A versão 1.6 adiciona transferência pela rede local. PC e celular devem estar na mesma rede; não usa conta, internet ou serviço de nuvem.

## Pelo aplicativo Android

1. Atualize o PC e o APK para **1.6.0 ou superior**. O instalador também inclui o APK para salvar uma cópia em **Conexões → Baixar APK para Android**.
2. No PC, abra **Conexões → Iniciar controle remoto**. No APK, informe o endereço do computador e o código de seis dígitos.
3. No celular conectado, toque em **Enviar arquivos** e escolha os arquivos locais no seletor do Android. É possível selecionar até dez arquivos por lote, cada um com até **512 MB (512 MiB)**.
4. Acompanhe nome, quantidade e progresso. **Cancelar envio** interrompe o arquivo atual e o restante do lote; arquivos já concluídos permanecem no PC.
5. No computador, abra **Conexões → Arquivos recebidos do celular**.

O aplicativo pede acesso apenas aos arquivos escolhidos no seletor do sistema, sem permissão geral sobre o armazenamento. Sair do aplicativo, bloquear a tela ou desconectar cancela o envio em andamento; não há retomada ou reenvio automático. Se houver queda de rede após o último byte, confira a lista no PC antes de reenviar.

## Pelo navegador do celular

Abra o link completo do controle remoto exibido no PC. Na seção **Enviar arquivos ao PC**, selecione os arquivos e clique em **Enviar arquivos**. O mesmo limite de dez arquivos e 512 MB por arquivo se aplica. A barra informa o progresso e o botão de cancelamento interrompe o lote. Os comandos do culto continuam disponíveis durante o envio.

O navegador também permite usar esse recurso em celulares sem o APK Android, desde que tenha suporte ao seletor de arquivos e esteja conectado ao controle local.

## Usar os arquivos recebidos

Os arquivos permanecem guardados no PC até serem usados ou removidos pelo operador. Eles não alteram a apresentação ao chegar.

- **Salvar cópia…** grava qualquer arquivo em uma pasta escolhida no computador.
- **Usar como fundo** aplica imagens PNG/JPG/JPEG/WebP/BMP ou vídeos MP4/MOV/MKV/WebM/AVI. A mídia é copiada para a biblioteca; o arquivo recebido permanece guardado.
- **Importar letras** lê TXT UTF-8, JSON de músicas ou OpenLyrics XML e adiciona os louvores à biblioteca. Um documento de outro formato pode ser salvo como cópia, mas não vira letra automaticamente.
- **Abrir pasta** abre a pasta do arquivo selecionado ou a caixa de entrada. **Atualizar lista** consulta os arquivos concluídos mais recentes.

A lista mostra os cem recebimentos mais recentes. A caixa de entrada tem limite de **5 GB (5 GiB)** e **5.000 arquivos**; para liberar espaço, abra a pasta e remova recebimentos que não precisa manter. Os nomes iguais são guardados separadamente, sem substituir os anteriores. Nomes que o Windows não aceita precisam ser alterados no celular.

O armazenamento fica em `%LocalAppData%\WorshipStudio\received`, separado da biblioteca. Os backups `.wstudio` mantêm seu escopo de biblioteca e mídia da cena; os demais arquivos recebidos devem ser copiados separadamente. O programa não executa arquivos recebidos.

## Conexão e interrupções

- Só celulares com a chave de acesso da sessão podem enviar. Desligar o controle remoto revoga os acessos e cancela o recebimento atual.
- O PC recebe um arquivo por vez. Se outro operador estiver enviando, aguarde terminar.
- Cancelamento, desconexão, arquivo incompleto ou limite excedido não adicionam um recebimento parcial à lista.
- Uma transferência pode durar até quinze minutos e não deve ficar mais de um minuto sem enviar dados. Em caso de falha, confira a lista e tente novamente.
- Não é necessário desligar a projeção. Leitura e gravação do arquivo acontecem fora da interface de operação.

## Verificação

Os testes exercitam os clientes Java exatos do APK contra o servidor C# de produção, com arquivos binários e nomes Unicode, corpos HTTP conhecidos e chunked, SHA256, cancelamento, limites e comandos durante o envio. O fluxo do navegador também foi observado no PC com um arquivo sintético; a interface desktop foi exercitada em Avalonia Headless/Skia. Esses ensaios não executam o seletor/Activity em um celular físico nem comprovam uma rede Wi-Fi específica. Veja [VERIFICACAO.md](../VERIFICACAO.md).
