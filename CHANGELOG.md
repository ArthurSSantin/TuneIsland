# Changelog

Todas as mudanças notáveis deste projeto serão documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o versionamento segue [SemVer](https://semver.org/lang/pt-BR/).

## [Unreleased]

## [0.1.0] - 2026-11-13

### Adicionado
- Ilha fixa no topo central do monitor principal, estilos "attached" e "floating".
- Leitura de título, artista, capa e posição via SMTC, com eventos (sem polling de mídia).
- Estados compacto e expandido com animações, auto-expansão ao trocar de faixa e marquee.
- Barra de progresso clicável, controles de anterior, play/pause e próxima.
- Scroll sobre a ilha ajusta o volume do sistema.
- Cor de destaque extraída da capa.
- Ícone na bandeja com Configurações, Iniciar com o Windows e Sair.
- Configurações em `%AppData%\TuneIsland\settings.json`.
- Esconde quando ocioso e em apps de tela cheia.
- Instância única e log de erros em `%AppData%\TuneIsland\logs\tuneisland.log`.
- Testes xUnit para formatação de tempo, cor dominante, configurações e estimativa de posição.
- Workflows do GitHub Actions: build e testes em push/PR, e release com zip e SHA-256 ao criar tag `v*`.

### Notas
- Sem rede e sem telemetria.
- Sem pacotes NuGet de terceiros.
