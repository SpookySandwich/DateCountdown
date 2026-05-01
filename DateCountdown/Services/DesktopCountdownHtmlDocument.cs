using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DateCountdown.Core;

namespace DateCountdown.Services;

internal static class DesktopCountdownHtmlDocument
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Create(IReadOnlyList<DesktopCountdownContent> contents)
    {
        string overlayJson = JsonSerializer.Serialize(
            contents.Select((content, index) => new OverlayPayload(
                $"overlay-{index}",
                content.DaysLeft,
                content.Title,
                content.DaysTextSize,
                content.TitleTextSize,
                content.PositionX,
                content.PositionY,
                content.Scale,
                string.IsNullOrWhiteSpace(content.Css)
                    ? CountdownPreferences.DefaultDesktopCountdownCss
                    : content.Css)),
            JsonOptions);

        return $$"""
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:; font-src local;">
            <style>
            html, body {
              margin: 0;
              width: 100%;
              height: 100%;
              overflow: hidden;
              background: transparent;
              pointer-events: none;
              user-select: none;
            }

            .desktop-placement {
              position: absolute !important;
              transform-origin: center center !important;
              max-width: min(90vw, 900px) !important;
              pointer-events: none !important;
            }
            </style>
            </head>
            <body>
              {{CreateHosts(contents.Count)}}
              <script type="application/json" id="overlay-data">{{overlayJson}}</script>
              <script>
              (() => {
                const overlays = JSON.parse(document.getElementById('overlay-data').textContent || '[]');
                const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, character => ({
                  '&': '&amp;',
                  '<': '&lt;',
                  '>': '&gt;',
                  '"': '&quot;',
                  "'": '&#39;'
                }[character]));

                for (const overlay of overlays) {
                  const host = document.getElementById(overlay.hostId);
                  if (!host) {
                    continue;
                  }

                  host.style.setProperty('left', `${overlay.positionX * 100}vw`, 'important');
                  host.style.setProperty('top', `${overlay.positionY * 100}vh`, 'important');
                  host.style.setProperty('transform', `translate(-50%, -50%) scale(${overlay.scale})`, 'important');

                  const root = host.attachShadow({ mode: 'open' });
                  root.innerHTML = `
                    <style>
                      :host { display: block; }
                      ${overlay.css}
                      .days { font-size: ${overlay.daysTextSize}px !important; }
                      .title { font-size: ${overlay.titleTextSize}px !important; }
                    </style>
                    <div class="countdown">
                      <div class="days">${escapeHtml(overlay.daysLeft)}</div>
                      <div class="title">${escapeHtml(overlay.title)}</div>
                    </div>`;
                }
              })();
              </script>
            </body>
            </html>
            """;
    }

    private static string CreateHosts(int count)
    {
        return string.Join(
            "\n              ",
            Enumerable.Range(0, count).Select(index => $"<main id=\"overlay-{index}\" class=\"desktop-placement\" aria-label=\"Date countdown\"></main>"));
    }

    private sealed record OverlayPayload(
        string HostId,
        string DaysLeft,
        string Title,
        double DaysTextSize,
        double TitleTextSize,
        double PositionX,
        double PositionY,
        double Scale,
        string Css);
}
