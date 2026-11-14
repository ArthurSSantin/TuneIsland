# TuneIsland

Ilha estilo Dynamic Island para Windows 10/11, fixa no topo central da tela e dedicada só a música.

![Ilha compacta](docs/screenshot-compact.png)
![Ilha expandida](docs/screenshot-expanded.png)

> English summary below. / Resumo em português acima.

## Recursos (v0.1.0)

- Lê título, artista, capa e posição da sessão de mídia do Windows (SMTC): Spotify, navegadores e outros apps compatíveis.
- Usa a sessão que está tocando. Sem nenhuma sessão tocando, a ilha fica ociosa ou some, conforme a configuração.
- Estado compacto (mini capa e equalizador animado) e expandido ao passar o mouse (capa grande, título com marquee, barra de progresso clicável, anterior, play/pause e próxima).
- Ao trocar de faixa, expande sozinha por alguns segundos.
- Scroll sobre a ilha ajusta o volume do sistema.
- Cor de destaque extraída da capa, aplicada à barra de progresso e ao brilho.
- Estilos "attached" (colada no topo) e "floating".
- Ícone na bandeja com Configurações, Iniciar com o Windows e Sair.
- Esconde quando nada estiver tocando e em apps de tela cheia (jogos, vídeos).
- Instância única, log de erros em arquivo e nenhuma conexão de rede.

## Requisitos

- Windows 10 (19041 ou superior) ou Windows 11.
- Para rodar a partir da release: [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).
- Para compilar: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Instalação

1. Baixe `TuneIsland-vX.Y.Z-win-x64.zip` na página de [Releases](https://github.com/ArthurSSantin/TuneIsland/releases).
2. Confira o SHA-256 com `SHA256SUMS-vX.Y.Z.txt`, se quiser.
3. Extraia o zip e execute `TuneIsland.exe`.

## Build local

```powershell
dotnet build TuneIsland.sln -c Release
dotnet test TuneIsland.sln -c Release
dotnet publish src/TuneIsland/TuneIsland.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## Configuração

As configurações ficam em `%AppData%\TuneIsland\settings.json` e também podem ser alteradas pela janela de Configurações, aberta pelo ícone da bandeja.

| Chave | Padrão | Descrição |
|---|---|---|
| `Style` | `Attached` | `Attached` (colada no topo) ou `Floating` (flutuante) |
| `MonitorIndex` | `0` | Monitor onde a ilha aparece (0 = principal) |
| `Width` | `380` | Largura expandida, de 240 a 800 px |
| `AutoExpandSeconds` | `3` | Tempo da auto-expansão ao trocar de faixa, de 1 a 10 s |
| `StartWithWindows` | `false` | Inicia com o Windows (chave `HKCU\...\Run`) |
| `HideWhenIdle` | `true` | Esconde quando nada estiver tocando |
| `HideInFullscreen` | `true` | Esconde quando um app está em tela cheia |

Logs: `%AppData%\TuneIsland\logs\tuneisland.log`.

## Privacidade

O app não faz nenhuma requisição de rede e não envia telemetria. Ele só lê metadados da sessão de mídia do próprio Windows e guarda as configurações e o log localmente.

## Roadmap

Veja as [issues](https://github.com/ArthurSSantin/TuneIsland/issues) com o rótulo de roadmap.

- Visualizador de áudio real (WASAPI loopback + FFT).
- Letras sincronizadas via LRCLIB, com cache local (opcional).
- Temas e fundo acrílico/blur.

## Licença

MIT. Veja [LICENSE](LICENSE).

---

# TuneIsland (English)

A Dynamic Island-style music widget for Windows 10/11. It sits fixed at the top center of the primary screen and only shows music.

**Features (v0.1.0):** reads title, artist, cover and playback position from the Windows System Media Transport Controls (SMTC); follows the session that is currently playing; compact and hover-expanded states with smooth animations; auto-expands on track change; scroll to change system volume; accent color from the cover; attached or floating style; tray icon with settings, start-with-Windows and exit; hides when idle and in fullscreen apps; single instance; no network access and no telemetry.

**Requirements:** Windows 10 (19041+) or Windows 11, and the .NET 8 Desktop Runtime (x64). To build, the .NET 8 SDK.

**Install:** download the zip from [Releases](https://github.com/ArthurSSantin/TuneIsland/releases), verify the SHA-256 if you like, extract it and run `TuneIsland.exe`.

**Build:** `dotnet build TuneIsland.sln -c Release` and `dotnet test TuneIsland.sln -c Release`.

**Settings:** `%AppData%\TuneIsland\settings.json` (see the table above), or the Settings window from the tray icon.

**License:** MIT.
