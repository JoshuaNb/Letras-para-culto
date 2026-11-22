# Controle pelo celular Android

**Letras para culto · Controle** opera a apresentação do Worship Studio na mesma rede local. O aplicativo é nativo Android, em português brasileiro, gratuito e sem conta, anúncios, nuvem ou serviços Google. Requer Android 8.0 ou superior; o APK não depende de uma arquitetura específica de processador.

[Baixar o APK 1.6.0](https://github.com/JoshuaNb/Letras-para-culto/releases/download/v1.6.0/LetrasParaCulto-Controle-1.6.0.apk) · [Instalador de PC 1.6.0](https://github.com/JoshuaNb/Letras-para-culto/releases/download/v1.6.0/WorshipStudio-Setup-1.6.0.exe).

O instalador de PC também inclui o arquivo Android. Abra **Conexões → Baixar APK para Android…**, escolha onde salvar e transfira o APK ao celular, por exemplo por cabo. Salvar essa cópia não exige internet; se o arquivo não estiver presente na instalação, o botão abre o download oficial da mesma versão no GitHub.

## Preparar e conectar

Para enviar arquivos, atualize o PC e o APK para 1.6 ou superior. Depois de conectar, toque em **Enviar arquivos**. A seleção é local, com até dez arquivos e 512 MB por arquivo; progresso e cancelamento aparecem no próprio controle. O PC guarda os concluídos em **Conexões → Arquivos recebidos do celular**. [Guia de transferência](TRANSFERIR-ARQUIVOS.md).

1. Abra o APK baixado no Android e conclua a instalação pela interface do aparelho.
2. Conecte celular e computador à mesma rede local. O PC pode estar por cabo e o celular por Wi-Fi, desde que consigam comunicar-se nessa rede.
3. No Worship Studio 1.4.0, abra **Conexões** e clique em **Iniciar controle remoto**.
4. No celular, informe o IPv4 e a porta mostrados no PC, por exemplo `192.168.1.10:8765`. Digite o código de seis dígitos exibido no computador e toque em **Conectar**.
5. Prepare um louvor no PC e envie uma estrofe à projeção. O aplicativo Android mostra o retorno e os controles da apresentação atual.

O código de pareamento vale por dez minutos. Após o pareamento, o telefone permanece autorizado enquanto o controle remoto do PC estiver ativo. Desligar e iniciar novamente o controle remoto revoga o token anterior e gera novas credenciais; conecte o celular novamente quando isso ocorrer.

Não é preciso ter internet para controlar. A internet é usada para baixar o APK ou atualizações; os comandos passam somente pela rede local.

## Usar um link completo

O APK também aceita colar o endereço completo exibido pelo controle web do PC, no formato `http://IPv4:porta/?token=...`. Esse formato é compatível com as versões anteriores do servidor remoto. O pareamento por código de seis dígitos existe a partir do PC 1.4.0.

O endereço contém uma chave de acesso. Compartilhe-o apenas com os operadores autorizados. O programa do PC revoga a chave ao desligar o controle remoto.

## Operar a apresentação

| Controle | Resultado no PC |
| --- | --- |
| Anterior / Próxima | Navega na apresentação ao vivo |
| Uma estrofe da lista | Envia diretamente aquela estrofe |
| Ocultar letra | Alterna a visibilidade da letra, conservando o fundo |
| Tela preta | Alterna a tela preta da saída |
| Pulso de efeito | Aciona o pulso manual dos efeitos |

O celular acompanha título, letra, posição e estados da apresentação atual. A biblioteca, os fundos, o áudio e a projeção permanecem sob responsabilidade do computador. Se o telefone desconectar ou fechar o aplicativo, a apresentação continua no PC.

As consultas pausam quando o aplicativo vai para segundo plano e retomam ao voltar. Durante uma perda de conexão, os comandos ficam desativados até chegar um novo estado. O aplicativo não reenvia automaticamente um comando cujo resultado foi perdido, evitando alternar tela preta ou letra oculta duas vezes.

Comandos que ainda estavam enfileirados são descartados se o aplicativo entrar em segundo plano antes de enviá-los.

## Reconectar

- **Código expirado:** no PC, desligue e inicie novamente o controle remoto; informe o novo código no APK.
- **Acesso inválido após reiniciar o controle:** conecte novamente com o código ou link atual. O acesso anterior foi revogado.
- **Sem resposta do PC:** confira se o Worship Studio está aberto, o controle remoto está ativo e IPv4/porta correspondem aos exibidos. Confirme que os dispositivos estão na mesma rede; redes de convidados podem isolar clientes.
- **Conectado sem estrofes:** selecione um louvor no computador e envie-o à projeção. O aplicativo controla a apresentação ao vivo.
- **Muitas tentativas:** aguarde um minuto antes de tentar novamente. O servidor limita pareamentos por endereço e globalmente.

Somente o endereço do PC é lembrado. Código e token ficam na memória da sessão; se ela for encerrada, faça um novo pareamento. O APK não solicita câmera, microfone ou acesso ao armazenamento.

## Atualizar e construir

As próximas versões são publicadas nas [Releases do GitHub](https://github.com/JoshuaNb/Letras-para-culto/releases). Instale o novo APK sobre a versão anterior usando a interface do Android. O programa de PC tem seu próprio botão **Atualizações** a partir da versão 1.3; isso não instala automaticamente o APK no celular.

O código Android e os scripts de construção ficam em `android-remote` no ZIP completo da distribuição de PC. A construção usa JDK 21, Android SDK Platform 36 e Build Tools 36.0.0. As próximas distribuições Android precisam conservar a mesma chave de assinatura para permitir atualização sobre a instalação anterior. Essa chave e suas senhas não acompanham o código público.

Na versão 1.4, o APK foi compilado, assinado e alinhado. Foram executados 32 testes Java do parser de conexão e 17 de HTTP real com o mesmo cliente incluído no APK e a operação do PC no backend Avalonia Headless. Isso valida o protocolo e os comandos, sem executar a Activity ou simular toques Android. O emulador sem hypervisor ficou indisponível; a Activity e o uso em celular físico não foram observados.

[Instruções de compilação e testes](../android-remote/README.md) · [Notas da versão 1.4](releases/v1.4.0.md).
