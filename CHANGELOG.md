# Histórico de mudanças

O projeto é publicado como **Letras para culto**. As três primeiras distribuições usam **Worship Studio** como nome do aplicativo e dos arquivos. As versões foram preparadas em 22/11/2026.

## 1.2.0 — Granularidade e galeria de animações

- Adicionada galeria com **66 opções: 65 animadas e Nenhuma**, busca e filtro por categoria.
- Adicionada granularidade por grafema Unicode, palavra, linha visual após a quebra automática e bloco de texto.
- Adicionados duração por unidade, intervalo, atraso inicial, ordem direta/inversa/centro/aleatória repetível, seis curvas, intensidade e repetição com pausa.
- Adicionados prévia com reprodução/pausa, reinício, barra de tempo e edição antes de **Aplicar à cena**; **Cancelar** preserva a cena anterior.
- Implementados desfoque real pelo Skia, máscaras, brilho, separação RGB e transformações independentes por unidade.
- Compartilhado o motor entre prévia, projeção e MP4, com reinício da entrada em cada estrofe.
- Preservados os controles clássicos da versão 1.1 e a leitura de bibliotecas/cenas antigas.
- Validação: **159 verificações aprovadas** — 44 de núcleo, 29 visuais anteriores, 79 visuais granulares e sete de integração MP4 — além de operação das janelas e teste do instalador.

Os movimentos são interpretações das referências estáticas recebidas. [Notas da versão 1.2.0](docs/releases/v1.2.0.md).

## 1.1.0 — Fontes, cores, negrito e mais movimento

- Corrigida a troca de fonte que não aparecia ao manter a mesma estrofe, por reutilização indevida do cache de texto.
- Adicionadas lista de fontes instaladas, amostra visual e alternância de negrito entre pesos normal e 700.
- Substituídos os campos simples de cor por seletor visual para letra, fundo e efeito, com paleta, matiz, saturação/luminosidade, RGB e HEX.
- Adicionados **Aplicar cor**, **Cancelar** e restauração da cor anterior ao cancelar ou fechar o seletor.
- Ampliadas as entradas para **12 animadas e Corte direto**.
- Adicionados oito movimentos contínuos, velocidade independente do fundo e controle de intensidade.
- Preservados fonte, peso, cores e animações na biblioteca, cenas e backup; cenas antigas continuam legíveis.
- Validação: **30 testes de operação e 29 verificações visuais aprovados**, além dos fluxos manuais e teste isolado do instalador.

[Notas da versão 1.1.0](docs/releases/v1.1.0.md).

## 1.0.0 — Primeira versão funcional

- Adicionados biblioteca local, editor de letras, busca, roteiro do culto, importação TXT/JSON/OpenLyrics e backup `.wstudio`.
- Implementados painel de operação e janela de projeção independentes, escolha de monitor, preparação sem substituir o texto ao vivo, navegação, letra oculta e tela preta.
- Adicionados fundos de imagem/vídeo local, Aurora, túnel 3D e partículas 3D, controles de aparência e cenas reutilizáveis.
- Adicionadas entradas Dissolver, Deslizar, Aproximar e Máquina de escrever, além de Corte direto.
- Implementados captura WASAPI, reação por amplitude/RMS, sensibilidade e pulso manual.
- Implementados controle remoto autenticado na rede local e integração de leitura com a API oficial do Holyrics.
- Implementada exportação MP4 offline com mídia, trilha opcional e cancelamento que preserva o arquivo anterior.
- Preparado instalador Windows 11 x64 com .NET, LibVLC e FFmpeg incluídos.
- Validação: **27 testes de operação aprovados**, inspeção das janelas, reprodução local de vídeo e instalação/abertura/desinstalação.

[Notas da versão 1.0.0](docs/releases/v1.0.0.md).
