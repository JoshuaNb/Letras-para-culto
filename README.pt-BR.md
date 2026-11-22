# Worship Studio 1.0.0

Aplicativo desktop em C#/.NET 10 e Avalonia UI para preparar e projetar letras de louvores. A operação, a biblioteca, a reprodução de mídia e a exportação funcionam sem internet. Não há conta, assinatura, anúncios ou licença paga para operar o aplicativo.

## Instalação e primeira projeção

1. Execute `WorshipStudio-Setup-1.0.0.exe` no Windows 11 x64. A instalação é por usuário, sem exigir administrador. .NET, LibVLC e FFmpeg necessários já acompanham o pacote.
2. Para dois monitores, escolha **Estender estes vídeos** nas configurações de vídeo do Windows. Ligue o projetor antes de iniciar.
3. Abra Worship Studio. Na aba **Cena**, escolha o monitor de saída.
4. Clique **Abrir projeção**. A janela de operação permanece independente da projeção. A janela de saída pode funcionar no mesmo monitor quando não há segunda tela.
5. Selecione um louvor na biblioteca. Um clique prepara a prévia; duplo clique envia ao vivo. Selecione uma estrofe e use **Projetar estrofe**. Esse comando também abre a saída automaticamente quando estiver fechada.
6. Use **Próxima**, **Anterior**, **Ocultar letra** e **Tela preta** durante o culto.

O louvor inicial é um texto original curto para conferir a operação. Importe ou cadastre a biblioteca da igreja.

## Biblioteca e roteiro

**+ Novo** e **Editar** abrem o editor de letras. Informe título, autoria e letra. Separe slides com uma linha em branco; títulos entre colchetes, como `[Verso 1]` e `[Refrão]`, identificam as partes.

```text
[Verso 1]
Primeira linha
Segunda linha

[Refrão]
Terceira linha
Quarta linha
```

O campo de busca procura título, autoria e letra. **Adicionar ao roteiro** monta a ordem do culto; escolha **Roteiro do culto** para reordenar ou remover entradas. A seleção na biblioteca prepara a prévia e não muda por si só o slide ao vivo.

**Importar** aceita letras TXT UTF-8, músicas JSON e OpenLyrics XML. JSON aceita uma música, lista, `songs` ou resposta Holyrics `data`, com `title`, `artist` e `lyrics` ou `slides`. OpenLyrics respeita `verseOrder`. `.hcb` e `.hcc` são formatos proprietários e não são lidos.

## Cenas, animações e mídia

A aba **Cena** contém os controles reais do renderizador: fundo procedural, imagem/vídeo local, velocidade, escurecimento, cor do efeito, cor de fundo, fonte, tamanho da letra, cor da letra, posição vertical, tipo e duração de transição. As alterações atualizam o renderizador em tempo real. **Testar transição na prévia** repete a animação sem alterar a saída ao vivo. **Reiniciar vídeo do fundo** reinicia a mídia nas superfícies de prévia e projeção. **Salvar cena** e **Carregar** permitem reutilizar os parâmetros.

Os efeitos procedurais usam geometria em perspectiva e animação contínua. O editor desta versão ajusta parâmetros e transições; não possui linha do tempo de keyframes nem importação de modelos 3D. Imagens e vídeos escolhidos são copiados para a pasta de mídia da biblioteca. A reprodução de fundo usa LibVLC local, em loop e sem som, para preservar o áudio do culto.

O Avalonia usa Skia/ANGLE no Windows com aceleração gráfica e fallback de software. O LibVLC solicita decodificação por hardware quando disponível. O uso efetivo da GPU depende do driver e do formato da mídia. A versão não inclui um benchmark certificado no Ryzen 5 5600G/RX 6600.

## Reação ao áudio

Ative **Sincronizar efeitos** e escolha **Som do computador (loopback)** ou **Entrada padrão / microfone**. O medidor mostra o nível capturado; ajuste **Sensibilidade**. O nível RMS altera a intensidade dos efeitos em tempo real. **Pulso manual** permite disparar uma reação sem captura.

É uma reação à amplitude do áudio; esta versão não estima BPM, não detecta compassos e não faz alinhamento automático de letra com fonemas. Para entrada da mesa de som, configure o dispositivo de gravação padrão do Windows antes de ativar a captura.

## Controle pelo celular

Na aba **Conexões**, clique **Iniciar controle remoto**, copie o endereço mostrado e abra no navegador do celular conectado à mesma rede local. Não é necessário instalar aplicativo no celular nem ter internet. A página contém navegação de slides, ocultar letra, tela preta e pulso manual.

O endereço contém uma chave aleatória. Quem tiver o endereço na rede pode operar a projeção. Ao desligar e religar o controle, a chave anterior é revogada. Se o Windows pedir autorização de rede, permita para **redes privadas**. Redes Wi-Fi com isolamento entre clientes impedem o acesso entre celular e computador; conecte ambos a uma rede que permita a comunicação local. O servidor é desligado ao fechar o programa.

## Holyrics

A integração usa somente a API oficial local do Holyrics e não precisa de internet. No Holyrics, habilite o **API Server**, crie um token com permissões de leitura para `GetSongs`, `GetLyrics` e `GetCurrentPresentation`, e informe endereço e token em **Conexões**.

- **Importar biblioteca via API** copia título, autoria e slides para a biblioteca local.
- **Seguir apresentação** consulta a apresentação atual e envia sua letra à janela Worship Studio. Também acompanha as indicações de tela preta e letra oculta retornadas pela API.

O token não é salvo no arquivo de biblioteca. A integração é de leitura; o Worship Studio não envia comandos ao Holyrics. Referência: [API Server oficial do Holyrics](https://github.com/holyrics/API-Server). A validação automatizada usa servidor local de teste compatível com os contratos documentados; valide com a versão do Holyrics usada pela sua igreja antes do culto.

## MP4

**Exportar MP4** gera um vídeo a partir das estrofes e dos parâmetros de cena. A renderização usa quadros reais do mesmo motor de letras e efeitos; fundos em vídeo são compostos pelo FFmpeg. A exportação é offline. A duração por slide é fixa conforme a configuração da exportação; não é uma gravação da sessão ao vivo.

O codificador padrão é MPEG-4 Part 2 em contêiner MP4, com pixel format YUV420P. Uma opção de aceleração AMD AMF pode ser usada quando disponível; o programa testa o codificador antes de escolhê-lo. A trilha de áudio, quando selecionada, é codificada em AAC. Não há dependência de codecs pagos. O cancelamento preserva um arquivo de destino preexistente e remove a saída parcial.

## Dados e cópias de segurança

Os dados ficam em `%LOCALAPPDATA%\WorshipStudio`: `library.json`, revisões `.bak`, pasta `media` e `studio.log`. O pacote e o instalador não precisam de rede para abrir ou usar essa biblioteca.

**Backup** cria um arquivo `.wstudio` com biblioteca, roteiro, cena e a mídia atualmente referenciada pela cena. **Restaurar** substitui a biblioteca depois da validação do arquivo e conserva a revisão anterior. O backup desta versão inclui a mídia da cena ativa, não arquivos avulsos que não estejam referenciados. Faça cópia antes de excluir louvores ou restaurar uma biblioteca. A desinstalação preserva os dados locais.

## Atalhos

| Tecla | Ação |
| --- | --- |
| Seta direita / Espaço | Próximo slide ao vivo |
| Seta esquerda | Slide anterior |
| B | Alternar tela preta |
| L | Ocultar / exibir letra |
| F11 | Alternar tela cheia da projeção |
| Esc na projeção | Sair da tela cheia |

Os atalhos de operação não devem interromper a digitação nos campos de edição.

## Compilar e verificar

Requer SDK .NET 10 x64 para desenvolvimento e Inno Setup 6 para reconstruir o instalador. O executável distribuído é self-contained e dispensa SDK/runtime instalado.

```powershell
.\build.ps1
# Ou com ferramentas em caminhos personalizados:
.\build.ps1 -DotnetPath 'C:\sdk\dotnet.exe' -InnoCompiler 'C:\Inno Setup 6\ISCC.exe'
# Sem reconstruir instalador:
.\build.ps1 -SkipInstaller
```

A primeira restauração de pacotes de desenvolvimento usa NuGet. Isso não é necessário na operação do aplicativo pronto. `build.ps1` executa os testes, publica em `app\win-x64` e compila o instalador. Os testes incluem biblioteca, importação, contrato Holyrics, autenticação remota e geração/decodificação de MP4 real.

Consulte `VERIFICACAO.md` para resultados observados e limites da validação. Nenhum vídeo de referência chegou acessível nesta conversa; a aparência foi desenvolvida a partir dos requisitos escritos.

## Licenças

Worship Studio: MIT. As dependências têm licenças próprias; consulte `THIRD-PARTY-NOTICES.txt`, a pasta `licenses` e `third-party-sources`. Fontes do aplicativo e arquivos de construção acompanham a entrega.
