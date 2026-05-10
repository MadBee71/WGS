# -*- coding: utf-8 -*-
"""
Windows Game Server – User Manual PDF (English)
Clean layout: explicit page breaks, CondPageBreak guards, large logo.
"""

import os
from reportlab.lib.pagesizes import A4
from reportlab.lib import colors
from reportlab.lib.units import cm, mm
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT
from reportlab.lib.utils import ImageReader
from reportlab.platypus import (
    Paragraph, Spacer, Table, TableStyle,
    PageBreak, HRFlowable, KeepTogether, CondPageBreak
)
from reportlab.platypus import BaseDocTemplate, Frame, PageTemplate
from reportlab.lib.colors import HexColor

# ── Constants ─────────────────────────────────────────────────────────────────
C_DARK    = HexColor("#1a1d2e")
C_ACCENT  = HexColor("#5b8fff")
C_ACCENT2 = HexColor("#3ecf8e")
C_WARN    = HexColor("#f59e0b")
C_ERROR   = HexColor("#ef4444")
C_LIGHT   = HexColor("#e2e8f0")
C_MID     = HexColor("#94a3b8")
C_BG      = HexColor("#0f1117")
C_CARD    = HexColor("#1e2235")
C_BORDER  = HexColor("#2d3354")
C_WHITE   = colors.white
C_SURFACE = HexColor("#252b42")

LOGO_PATH   = r"E:\WindowsGameServer\WGS\wgs.png"
SPLASH_PATH = r"E:\WindowsGameServer\WGS\splash.png"
OUTPUT    = r"E:\WindowsGameServer\github\Windows_Game_Server_Manual.pdf"
YEAR      = "2026"
AUTHOR    = "MadBee71"

W, H = A4          # 595.3 x 841.9 pt
TOP_BAR    = 30    # pt
BOT_BAR    = 24    # pt
L_MARGIN   = 1.8*cm
R_MARGIN   = 1.8*cm
T_MARGIN   = 1.5*cm + TOP_BAR
B_MARGIN   = 1.2*cm + BOT_BAR
PAGE_W     = W - L_MARGIN - R_MARGIN   # ≈ 17.4 cm

# ── Styles ────────────────────────────────────────────────────────────────────
def make_styles():
    def s(name, **kw):
        return ParagraphStyle(name, **kw)
    return {
        "h1": s("h1", fontName="Helvetica-Bold", fontSize=19, leading=26,
                 textColor=C_ACCENT, spaceBefore=0, spaceAfter=4),
        "h2": s("h2", fontName="Helvetica-Bold", fontSize=13, leading=18,
                 textColor=C_WHITE, spaceBefore=0, spaceAfter=4),
        "h3": s("h3", fontName="Helvetica-Bold", fontSize=10, leading=14,
                 textColor=C_ACCENT2, spaceBefore=6, spaceAfter=2),
        "body": s("body", fontName="Helvetica", fontSize=10, leading=15,
                   textColor=C_LIGHT, spaceBefore=2, spaceAfter=3),
        "body_j": s("body_j", fontName="Helvetica", fontSize=10, leading=15,
                     textColor=C_LIGHT, alignment=TA_JUSTIFY,
                     spaceBefore=2, spaceAfter=3),
        "bullet": s("bullet", fontName="Helvetica", fontSize=10, leading=14,
                     textColor=C_LIGHT, leftIndent=14, spaceBefore=1, spaceAfter=1),
        "code": s("code", fontName="Courier", fontSize=8.5, leading=13,
                   textColor=C_ACCENT2, backColor=HexColor("#151a2a"),
                   leftIndent=8, rightIndent=8, spaceBefore=4, spaceAfter=4,
                   borderPadding=5),
    }

ST = make_styles()   # module-level so helpers can use them


# ── Page decorators ───────────────────────────────────────────────────────────
class ContentPage:
    def __call__(self, canv, doc):
        canv.saveState()
        canv.setFillColor(C_BG)
        canv.rect(0, 0, W, H, fill=1, stroke=0)
        # top bar
        canv.setFillColor(C_DARK)
        canv.rect(0, H - TOP_BAR, W, TOP_BAR, fill=1, stroke=0)
        try:
            canv.drawImage(ImageReader(LOGO_PATH),
                           L_MARGIN, H - TOP_BAR + 5, width=20, height=20,
                           preserveAspectRatio=True, mask='auto')
        except Exception:
            pass
        canv.setFont("Helvetica-Bold", 8)
        canv.setFillColor(C_ACCENT)
        canv.drawString(L_MARGIN + 24, H - TOP_BAR + 10, "WINDOWS GAME SERVER")
        canv.setFont("Helvetica", 8)
        canv.setFillColor(C_MID)
        canv.drawRightString(W - R_MARGIN, H - TOP_BAR + 10, "User Manual")
        canv.setStrokeColor(C_ACCENT)
        canv.setLineWidth(0.8)
        canv.line(L_MARGIN, H - TOP_BAR, W - R_MARGIN, H - TOP_BAR)
        # bottom bar
        canv.setFillColor(C_DARK)
        canv.rect(0, 0, W, BOT_BAR, fill=1, stroke=0)
        canv.setStrokeColor(C_BORDER)
        canv.setLineWidth(0.4)
        canv.line(L_MARGIN, BOT_BAR, W - R_MARGIN, BOT_BAR)
        canv.setFont("Helvetica", 8)
        canv.setFillColor(C_MID)
        canv.drawString(L_MARGIN, 8, "Windows Game Server")
        canv.drawCentredString(W / 2, 8, f"— {doc.page} —")
        canv.drawRightString(W - R_MARGIN, 8, f"© {YEAR} {AUTHOR}")
        canv.restoreState()


def cover_page(canv, doc):
    """
    Cover page geometry (y=0 is page bottom, H=841.9pt):
      Gradient band   : H-320 … H        (top 320pt)
      Splash (360pt)  : bottom=602, top≈746  (includes WINDOWS GAME SERVER text)
      Subtitle        : y=576
      Tech pill       : y=538 … y=570
      Divider         : y=518
      Tile row 0      : y=448 … y=506    (58pt tall, 12pt below divider)
      Tile row 1      : y=381 … y=439    (67pt pitch)
      Tile row 2      : y=314 … y=372
      Sep. line       : y=294            (20pt below row 2)
      Desc line 1     : y=276
      Desc line 2     : y=260
      Bottom strip    : y=0  … y=52
      Footer text     : y=20
    """
    canv.saveState()

    # ── Full dark background ──────────────────────────────────────────────────
    canv.setFillColor(C_BG)
    canv.rect(0, 0, W, H, fill=1, stroke=0)

    # ── Gradient band ────────────────────────────────────────────────────────
    BAND = 320
    STEPS = 100
    for i in range(STEPS):
        r  = i / STEPS
        c  = colors.linearlyInterpolatedColor(C_ACCENT, C_DARK, 0, 1, r)
        canv.setFillColor(c)
        y0 = H - BAND + i * (BAND / STEPS)
        canv.rect(0, y0, W, BAND / STEPS + 1, fill=1, stroke=0)

    # ── Splash logo (wide, includes "WINDOWS GAME SERVER" text, ~360×144 pt) ──
    SPLASH_W = 360
    LX = W / 2 - SPLASH_W / 2
    LY = 602                        # bottom=602, top≈746
    try:
        canv.drawImage(ImageReader(SPLASH_PATH),
                       LX, LY, width=SPLASH_W, height=144,
                       preserveAspectRatio=True, mask='auto')
    except Exception:
        canv.setFillColor(C_ACCENT)
        canv.roundRect(LX, LY, SPLASH_W, 144, 22, fill=1, stroke=0)
        canv.setFillColor(C_WHITE)
        canv.setFont("Helvetica-Bold", 32)
        canv.drawCentredString(W / 2, LY + 56, "WINDOWS GAME SERVER")

    # ── Subtitle ──────────────────────────────────────────────────────────────
    canv.setFont("Helvetica", 13)
    canv.setFillColor(C_ACCENT)
    canv.drawCentredString(W / 2, 576,
        "Single-window management panel for Windows game servers")

    # ── Tech pill ─────────────────────────────────────────────────────────────
    pw, ph = 300, 32
    canv.setFillColor(C_CARD)
    canv.roundRect(W / 2 - pw / 2, 538, pw, ph, 8, fill=1, stroke=0)
    canv.setFont("Helvetica-Bold", 10)
    canv.setFillColor(C_ACCENT2)
    canv.drawCentredString(W / 2, 552, ".NET 8  ·  WPF  ·  SteamCMD  ·  17+ games")

    # ── Divider ───────────────────────────────────────────────────────────────
    canv.setStrokeColor(C_BORDER)
    canv.setLineWidth(1)
    canv.line(2 * cm, 518, W - 2 * cm, 518)

    # ── Feature tiles 3×3 ─────────────────────────────────────────────────────
    # Row 0 bottom=344, row 1 bottom=277, row 2 bottom=210  (pitch=67)
    feats = [
        ("17+ Games",    "Ready-made plugins"),
        ("SteamCMD",     "Install & update"),
        ("Auto Restart", "Crash recovery"),
        ("Dashboard",    "Live metrics"),
        ("Firewall",     "Auto rules"),
        ("Backups",      "Zip + restore"),
        ("RCON",         "Remote console"),
        ("CPU Affinity", "Core selection"),
        ("Encrypted",    "DPAPI security"),
    ]
    TW  = (W - 4 * cm) / 3      # tile width
    TH  = 58                     # tile height
    for i, (t, s) in enumerate(feats):
        col = i % 3
        row = i // 3
        x = 2 * cm + col * TW
        y = 448 - row * 67       # row0=448, row1=381, row2=314
        canv.setFillColor(C_CARD)
        canv.setStrokeColor(C_BORDER)
        canv.setLineWidth(0.5)
        canv.roundRect(x + 4, y, TW - 8, TH, 8, fill=1, stroke=1)
        canv.setFont("Helvetica-Bold", 11)
        canv.setFillColor(C_ACCENT)
        canv.drawCentredString(x + TW / 2, y + 36, t)
        canv.setFont("Helvetica", 9)
        canv.setFillColor(C_MID)
        canv.drawCentredString(x + TW / 2, y + 19, s)

    # ── Description (row 2 bottom = 314; separator 20pt below = 294) ─────────
    canv.setStrokeColor(C_BORDER)
    canv.setLineWidth(0.4)
    canv.line(2 * cm, 294, W - 2 * cm, 294)

    canv.setFont("Helvetica", 9.5)
    canv.setFillColor(C_MID)
    canv.drawCentredString(W / 2, 276,
        "Free, open-source Windows game server manager — no command-line required.")
    canv.drawCentredString(W / 2, 260,
        "Supports 17+ games with SteamCMD integration, backups, firewall, RCON and more.")

    # ── Bottom strip ──────────────────────────────────────────────────────────
    canv.setFillColor(C_DARK)
    canv.rect(0, 0, W, 52, fill=1, stroke=0)
    canv.setStrokeColor(C_BORDER)
    canv.setLineWidth(0.5)
    canv.line(0, 52, W, 52)
    canv.setFont("Helvetica-Bold", 9)
    canv.setFillColor(C_ACCENT2)
    canv.drawString(2 * cm, 20, "USER MANUAL")
    canv.setFont("Helvetica", 9)
    canv.setFillColor(C_MID)
    canv.drawRightString(W - 2 * cm, 20, f"© {YEAR} {AUTHOR}")
    canv.restoreState()


# ── Flowable helpers ──────────────────────────────────────────────────────────

def sec(text):
    """Section header — always guarded by CondPageBreak(7cm)."""
    return [
        CondPageBreak(7*cm),
        HRFlowable(width="100%", thickness=0.4, color=C_BORDER,
                   spaceBefore=4, spaceAfter=0),
        Paragraph(text, ST["h1"]),
        HRFlowable(width="35%", thickness=2.5, color=C_ACCENT,
                   spaceBefore=0, spaceAfter=6),
    ]


def h2(text):
    # No CondPageBreak here — caller wraps h2+content in KeepTogether to avoid orphans
    return [Paragraph(text, ST["h2"]), Spacer(1, 2)]


def h3(text):
    return [Paragraph(text, ST["h3"])]


def para(text, justify=False):
    return Paragraph(text, ST["body_j"] if justify else ST["body"])


def bul(items):
    return [Paragraph(f"• {t}", ST["bullet"]) for t in items]


def gap(n=6):
    return Spacer(1, n)


def note(label, text, color=None):
    c = color or C_ACCENT
    lbl_style = ParagraphStyle("nl", fontName="Helvetica-Bold",
                                fontSize=9, textColor=c)
    txt_style = ParagraphStyle("nt", fontName="Helvetica",
                                fontSize=9, textColor=C_LIGHT, leading=13)
    t = Table([[Paragraph(f"<b>{label}</b>", lbl_style),
                Paragraph(text, txt_style)]],
              colWidths=[0.22 * PAGE_W, 0.78 * PAGE_W])
    t.setStyle(TableStyle([
        ("BACKGROUND",   (0, 0), (-1, -1), C_CARD),
        ("LEFTPADDING",  (0, 0), (-1, -1), 10),
        ("RIGHTPADDING", (0, 0), (-1, -1), 10),
        ("TOPPADDING",   (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 6),
        ("LINEAFTER",    (0, 0), (0, -1),  3, c),
    ]))
    # Always return flat list — never nested inside KeepTogether
    return [t, gap(4)]


def steps(rows):
    """Numbered step table. Each row = (title, description)."""
    data = []
    for i, (title, desc) in enumerate(rows, 1):
        data.append([
            Paragraph(f"<b>{i}</b>",
                ParagraphStyle("sn", fontName="Helvetica-Bold", fontSize=13,
                               textColor=C_ACCENT, alignment=TA_CENTER)),
            Paragraph(f"<b>{title}</b><br/>{desc}",
                ParagraphStyle("sd", fontName="Helvetica", fontSize=10,
                               textColor=C_LIGHT, leading=14)),
        ])
    t = Table(data, colWidths=[1.1*cm, PAGE_W - 1.1*cm])
    t.setStyle(TableStyle([
        ("BACKGROUND",    (0, 0), (-1, -1), C_CARD),
        ("BACKGROUND",    (0, 0), (0, -1),  C_SURFACE),
        ("LEFTPADDING",   (0, 0), (-1, -1), 9),
        ("RIGHTPADDING",  (0, 0), (-1, -1), 9),
        ("TOPPADDING",    (0, 0), (-1, -1), 7),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
        ("ROWBACKGROUNDS",(1, 0), (1, -1),  [C_CARD, C_SURFACE]),
        ("LINEBELOW",     (0, 0), (-1, -2), 0.4, C_BORDER),
        ("VALIGN",        (0, 0), (-1, -1), "MIDDLE"),
    ]))
    # allow splitting on long step tables
    return [t, gap(8)]


def tbl(header, rows, widths, hcol=None):
    """Generic data table. ALWAYS returns [Table, Spacer] — never KeepTogether.
    Use *tbl(...) when embedding inside a KeepTogether to avoid nesting."""
    hc   = hcol or C_ACCENT
    data = [header] + rows
    t = Table(data, colWidths=widths, repeatRows=1)
    t.setStyle(TableStyle([
        ("BACKGROUND",    (0, 0), (-1, 0),  hc),
        ("TEXTCOLOR",     (0, 0), (-1, 0),  C_WHITE),
        ("FONTNAME",      (0, 0), (-1, 0),  "Helvetica-Bold"),
        ("FONTSIZE",      (0, 0), (-1, 0),  9),
        ("BOTTOMPADDING", (0, 0), (-1, 0),  6),
        ("TOPPADDING",    (0, 0), (-1, 0),  6),
        ("FONTNAME",      (0, 1), (-1, -1), "Helvetica"),
        ("FONTSIZE",      (0, 1), (-1, -1), 9),
        ("TEXTCOLOR",     (0, 1), (-1, -1), C_LIGHT),
        ("ROWBACKGROUNDS",(0, 1), (-1, -1), [C_CARD, C_SURFACE]),
        ("TOPPADDING",    (0, 1), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 1), (-1, -1), 5),
        ("LEFTPADDING",   (0, 0), (-1, -1), 7),
        ("RIGHTPADDING",  (0, 0), (-1, -1), 7),
        ("GRID",          (0, 0), (-1, -1), 0.3, C_BORDER),
        ("FONTNAME",      (0, 1), (0, -1),  "Helvetica-Bold"),
        ("TEXTCOLOR",     (0, 1), (0, -1),  C_WHITE),
    ]))
    return [t, gap(8)]


# ── Story ─────────────────────────────────────────────────────────────────────
def build_story():
    S = []

    def add(*items):
        for x in items:
            if isinstance(x, list):
                S.extend(x)
            else:
                S.append(x)

    # ══════════════════════════════════════════════════════════════════════════
    # 1 + 2  Introduction & Installation
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("1.  Introduction"))
    add(para(
        "Windows Game Server (WGS) is a free, open-source management application that makes "
        "installing, running and managing Windows game servers straightforward. "
        "Every essential operation is available from a single window — no command-line "
        "knowledge required for day-to-day use.", justify=True))
    add(gap(5))
    add(note("Technology",
        ".NET 8  ·  WPF  ·  CommunityToolkit.Mvvm  ·  SteamCMD  ·  "
        "Microsoft.Extensions.DependencyInjection", C_ACCENT))
    add(note("Requirements",
        "Windows 10 / Server 2019 or newer  ·  .NET 8 Runtime  ·  "
        "Administrator rights for firewall management", C_WARN))

    add(*sec("2.  Installation"))
    add(*h2("2.1  Pre-built binary (recommended)"))
    add(*steps([
        ("Download",      "Grab the latest release zip from the GitHub Releases page."),
        ("Extract",       "Extract the zip to any folder, e.g. C:\\WGS\\"),
        ("Run",           "Double-click WindowsGameServer.exe — the app starts immediately."),
        (".NET Runtime",  "If you see a runtime error, install .NET 8 Runtime from dotnet.microsoft.com"),
        ("SteamCMD",      "WGS downloads SteamCMD automatically the first time you install a game."),
    ]))
    add(*h2("2.2  Build from source"))
    add(para("Requires .NET 8 SDK."))
    add(Paragraph(
        "git clone https://github.com/YOUR_USERNAME/WindowsGameServer.git<br/>"
        "cd WindowsGameServer/WGS<br/>"
        "dotnet publish -c Release -o publish",
        ST["code"]))

    # ══════════════════════════════════════════════════════════════════════════
    # 3 + 4 + 5  First Launch · Adding · Install
    # ══════════════════════════════════════════════════════════════════════════
    add(PageBreak())

    add(*sec("3.  First Launch"))
    add(KeepTogether([
        para("When the app opens for the first time you will see an empty server list. "
             "The left sidebar contains:"),
        gap(3),
        *bul([
            "<b>Dashboard</b> — global system metrics and server status summary",
            "<b>Server list</b> — all configured servers with their current status",
            "<b>+ Add Server</b> — opens the new server creation dialog",
            "<b>Icon toolbar</b> — quick access to Settings, Discord, Web API, Workshop, Mods and more",
        ]),
        gap(6),
    ]))
    add(note("Tip",
        "The system-tray icon (bottom-right corner): WGS stays running when you close the "
        "window. Double-click the tray icon to reopen.", C_ACCENT2))

    add(*sec("4.  Adding a Server"))
    add(*steps([
        ("Open dialog",    "Click the '+ Add Server' button in the left sidebar."),
        ("Select game",    "Pick from 17+ pre-configured games in the dropdown."),
        ("Name",           "Give the server a recognisable name (e.g. 'Valheim — Family')."),
        ("Install folder", "WGS suggests a path automatically. Change it if needed."),
        ("Create",         "Click 'Create server' — it appears with status 'Not Installed'."),
    ]))
    add(note("Note",
        "No files are downloaded here — only the server definition is created. "
        "Use 'Install / Update' to download game files.", C_WARN))

    add(*sec("5.  Install & Update"))
    add(para(
        "WGS uses Valve's SteamCMD to download game files. SteamCMD is fetched automatically "
        "into WGS's own AppData folder on first use.", justify=True))
    add(gap(4))
    add(*steps([
        ("Select server",    "Click a server in the left list."),
        ("Install / Update", "Click 'Install / Update' in the server detail view."),
        ("Login",            "Games requiring a purchased copy will ask for Steam credentials. "
                             "Most games support anonymous login."),
        ("Progress",         "The console shows real-time download percentage from SteamCMD."),
        ("Done",             "When the download finishes the status changes to 'Stopped'."),
    ]))
    add(note("Steam Guard",
        "If Steam Guard is enabled, SteamCMD will request an email code on first login. "
        "Enter the code in the console input field.", C_WARN))

    # ══════════════════════════════════════════════════════════════════════════
    # 6  Starting & Stopping
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("6.  Starting & Stopping"))
    add(para("Control buttons in the server detail view:"))
    add(gap(4))
    add(tbl(
        ["Button", "Action", "Notes"],
        [
            ["Start",        "Launches the server process",                "Runs a port pre-flight check first"],
            ["Stop",         "Sends graceful stop command; kills after 5 s","Waits for clean shutdown"],
            ["Kill",         "Kills the entire process tree immediately",   "No grace period"],
            ["Restart",      "Stop → 3 s delay → Start",                   ""],
            ["Show Window",  "Brings the server console window to the front","Only if the process has a window"],
        ],
        [3.2*cm, PAGE_W*0.44, PAGE_W*0.33]))

    add(KeepTogether([
        Paragraph("Status colours", ST["h2"]),
        Spacer(1, 2),
        *tbl(
            ["Status", "Colour", "Meaning"],
            [
                ["Running",       "Green",  "Server is running normally"],
                ["Starting",      "Blue",   "Process is starting up"],
                ["Stopping",      "Orange", "Graceful shutdown in progress"],
                ["Stopped",       "Grey",   "Stopped — game files are installed"],
                ["Not Installed", "Grey",   "Game files have not been downloaded yet"],
                ["Error",         "Red",    "Process failed to start or crash limit reached"],
            ],
            [4*cm, 3*cm, PAGE_W - 7*cm], hcol=C_SURFACE),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 7  Automation
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("7.  Automation & Auto-Start"))
    add(KeepTogether([
        *bul([
            "<b>Auto Start</b> — starts the server automatically when WGS is launched",
            "<b>Auto Restart</b> — restarts the server after a crash with a configurable delay (default 10 s)",
            "<b>Auto Restart Max Retries</b> — if the server crashes more than N times within 10 minutes, "
            "auto-restart is suspended to prevent an infinite crash loop (default 5)",
            "<b>Auto Update</b> — runs Install/Update on a configurable interval (minutes) while the server "
            "is running; stops and restarts the server automatically",
        ]),
        gap(6),
    ]))
    add(note("Crash loop detection",
        "WGS counts crashes within a rolling 10-minute window. If the count exceeds Max Retries, "
        "auto-restart is disabled and an error is logged. Re-enable Auto Restart manually after "
        "fixing the underlying problem.", C_WARN))
    add(note("Auto Update interval",
        "Set 'Auto Update Interval (min)' in the server Settings tab. 0 disables periodic updates. "
        "A backup is created automatically before each update if Backup Enabled is checked.", C_ACCENT2))

    # ══════════════════════════════════════════════════════════════════════════
    # 8  Server Settings
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("8.  Server Settings"))
    add(*h2("8.1  General fields"))
    add(tbl(
        ["Field", "Description"],
        [
            ["Display Name",    "Internal name used inside WGS"],
            ["Server Name",     "Public name shown in the in-game server browser"],
            ["Max Players",     "Maximum allowed player count"],
            ["Server IP",       "Listen address (0.0.0.0 = all network interfaces)"],
            ["Game Port",       "Main port — players connect here"],
            ["Query Port",      "Steam Query port used by the server browser"],
            ["Steam Port",      "Steam network port (required by some games, e.g. Wreckfest)"],
            ["RCON Port",       "Remote console port (Source RCON protocol)"],
            ["RCON Password",   "RCON password — do not leave blank on a public server"],
            ["Server Password", "Connection password for players (optional)"],
            ["GSLT Token",      "Steam Game Server Login Token (requires a Steam account)"],
            ["Custom Args",     "Extra command-line arguments appended to the start command"],
            ["Install Path",    "Folder where the game files live on disk"],
        ],
        [5*cm, PAGE_W - 5*cm]))

    add(KeepTogether([
        Paragraph("8.2  Performance settings", ST["h2"]),
        Spacer(1, 2),
        para("Each server can be restricted to specific CPU cores and assigned a Windows "
             "process priority. Changes apply on the next startup.", justify=True),
        gap(4),
        *tbl(
            ["Setting", "Values", "Description"],
            [
                ["CPU Affinity",     "Core checkboxes",
                 "Which CPU cores the server may use. All unchecked = no restriction."],
                ["Process Priority", "Normal / AboveNormal / High / BelowNormal / RealTime",
                 "Windows process priority. High and RealTime require admin rights."],
            ],
            [4*cm, 5.5*cm, PAGE_W - 9.5*cm]),
        *note("Warning",
            "RealTime priority can freeze the entire system if the server runs at 100 % CPU. "
            "Use with caution.", C_ERROR),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 9 + 10  Firewall & Ports
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("9.  Firewall"))
    add(KeepTogether([
        para("WGS manages Windows Firewall rules automatically when "
             "'Firewall Auto-Manage' is enabled (default: on)."),
        gap(4),
        *bul([
            "On server <b>start</b>: WGS adds inbound TCP + UDP rules for game, query and Steam ports.",
            "On server <b>stop</b>: those rules are removed automatically.",
            "If Auto-Manage is <b>off</b>: add rules manually in Windows Defender Firewall.",
            "Rules use <b>netsh advfirewall</b> — administrator rights are required.",
        ]),
        gap(8),
    ]))

    add(*sec("10.  Ports & Networking"))
    add(KeepTogether([
        para("Before startup WGS checks whether the required ports are free locally. "
             "A port-in-use warning is logged but startup continues."),
        gap(5),
        *tbl(
            ["Port type", "Purpose", "Default"],
            [
                ["Game Port",  "Players connect to this port",            "Varies by game"],
                ["Query Port", "Steam server browser queries this port",  "Usually Game Port + 1"],
                ["Steam Port", "Steam network traffic (e.g. Wreckfest)", "27015"],
                ["RCON Port",  "Remote console (Source RCON protocol)",   "Varies"],
            ],
            [4.5*cm, PAGE_W * 0.55, PAGE_W * 0.25]),
        *note("Router / NAT",
            "To make the server reachable from the internet you must set up Port Forwarding "
            "on your router. WGS's pre-flight check only detects local conflicts.", C_MID),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 11  Console & RCON
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("11.  Console & RCON"))
    add(KeepTogether([
        Paragraph("11.1  Console", ST["h2"]),
        Spacer(1, 2),
        para("The Console tab shows server output in real time. "
             "Messages are colour-coded:"),
        gap(4),
        *tbl(
            ["Colour", "Type", "Description"],
            [
                ["White / Grey", "Info",    "Normal log messages"],
                ["Yellow",       "Warning", "Lines containing 'warn'"],
                ["Red",          "Error",   "Lines containing 'error', 'exception' or 'fatal'"],
                ["Cyan",         "System",  "WGS messages and SteamCMD output"],
            ],
            [3.5*cm, 3*cm, PAGE_W - 6.5*cm]),
        *note("Filter",
            "The Filter field above the console searches the log in real time "
            "(case-insensitive).", C_ACCENT2),
    ]))

    add(KeepTogether([
        Paragraph("11.2  RCON remote console", ST["h2"]),
        Spacer(1, 2),
        para(
            "RCON is the Valve Source protocol for sending admin commands over the network. "
            "Works with Rust, 7 Days to Die, and other Source-based games."),
        gap(4),
        *steps([
            ("Set RCON port & password",
             "Fill in 'RCON Port' and 'RCON Password' in the server's Settings tab."),
            ("Start the server", "RCON becomes available when the server process is running."),
            ("Connect",          "Click 'RCON: Connect' in the Console tab."),
            ("Send a command",   "Type the command and press Enter or click 'Send'."),
        ]),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 12  Config Editor  (NEW)
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("12.  Config Editor"))
    add(KeepTogether([
        para("The <b>Config</b> tab lets you browse and edit server configuration files "
             "directly inside WGS — no file manager or text editor needed."),
        gap(4),
        *bul([
            "Click the <b>Config</b> tab in the server detail view.",
            "WGS scans the install folder and lists all recognised configuration files.",
            "Click a file name to load its contents into the built-in text editor.",
            "Edit the content, then click <b>Save</b> to write changes to disk.",
            "Click <b>Open folder</b> (📁) to open the install directory in Windows Explorer.",
        ]),
        gap(6),
    ]))
    add(note("Tip",
        "Stop the server before editing files it holds open. Changes take effect on the next start.",
        C_ACCENT2))

    # ══════════════════════════════════════════════════════════════════════════
    # 13  Performance Charts  (NEW)
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("13.  Performance Charts"))
    add(KeepTogether([
        para("The <b>Charts</b> tab displays real-time CPU and RAM usage history for the "
             "selected server while it is running."),
        gap(4),
        *bul([
            "<b>CPU % chart</b> — process CPU usage as a percentage of total system CPU capacity",
            "<b>RAM chart</b> — working-set memory consumption in MB",
            "History window: last <b>6 minutes</b> — 180 data points sampled every 2 seconds",
            "Charts reset automatically when the server is stopped",
        ]),
        gap(4),
    ]))
    add(note("Note",
        "CPU % is normalised across all logical cores. A server fully using one core on a "
        "4-core machine shows ~25 %.", C_MID))

    # ══════════════════════════════════════════════════════════════════════════
    # 14  Mod Manager  (NEW)
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("14.  Mod Manager"))
    add(KeepTogether([
        para("The <b>Mods</b> tab manages server-side modding frameworks. "
             "Two frameworks are supported:"),
        gap(4),
        *bul([
            "<b>Oxide / uMod</b> — for Rust and other supported games. Downloads the latest "
            "Oxide release and installs it into the server folder.",
            "<b>Minecraft plugins</b> — for Paper/Spigot servers. "
            "Displays the contents of the plugins folder.",
        ]),
        gap(6),
        *tbl(
            ["Action", "Description"],
            [
                ["Install Oxide",
                 "Downloads and installs the latest Oxide/uMod release for this game"],
                ["Update Oxide",
                 "Re-downloads Oxide to update to the latest version"],
                ["Open plugins folder",
                 "Opens the server's plugins directory in Windows Explorer"],
            ],
            [5*cm, PAGE_W - 5*cm]),
    ]))
    add(note("Note",
        "Stop the server before installing or updating Oxide. "
        "Oxide injects into the server process on startup.", C_WARN))

    # ══════════════════════════════════════════════════════════════════════════
    # 15  Steam Workshop  (NEW)
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("15.  Steam Workshop"))
    add(para(
        "The <b>Workshop</b> tab is visible for games that support Steam Workshop (e.g. Arma Reforger). "
        "It uses SteamCMD to download Workshop items into WGS's steamapps workshop folder.",
        justify=True))
    add(gap(4))
    add(*steps([
        ("Find the item ID",
         "Open the Workshop page in a browser. The ID is the number in the URL: "
         "steamcommunity.com/sharedfiles/filedetails/?id=XXXXXXXXX"),
        ("Enter the ID",
         "Paste the Workshop item ID into the input field in the Workshop tab."),
        ("Download",
         "Click 'Download'. WGS runs SteamCMD in the background and shows progress in the console."),
        ("Done",
         "The item appears in the Installed Items list with its name and local folder path."),
    ]))
    add(KeepTogether([
        *bul([
            "Click <b>Remove</b> next to an item to delete it from disk.",
            "Workshop content is stored in: "
            "<b>%APPDATA%\\WGS\\steamcmd\\steamapps\\workshop\\content\\</b>",
        ]),
        gap(8),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 16  Player Statistics  (NEW)
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("16.  Player Statistics"))
    add(KeepTogether([
        para("The <b>Players</b> tab shows session history for everyone who has connected to "
             "the server. Data is stored in a local SQLite database and survives restarts."),
        gap(4),
        *tbl(
            ["Column", "Description"],
            [
                ["Player Name", "Name as reported in the server log"],
                ["Join Time",   "Timestamp when the player connected"],
                ["Leave Time",  "Timestamp when the player disconnected (blank if still online)"],
                ["Duration",    "Total time in session — format HH:MM:SS"],
            ],
            [4*cm, PAGE_W - 4*cm]),
        gap(4),
        *bul([
            "The <b>Total Playtime</b> view shows cumulative playtime per player across all sessions.",
            "Join/leave detection works by parsing the server's log output — "
            "accuracy depends on each game's log format.",
            "Stored in: <b>%APPDATA%\\WGS\\player_stats.db</b>",
        ]),
        gap(8),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 17  Backups
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("17.  Backups"))
    add(KeepTogether([
        para("WGS creates zip archives of a server's save data. "
             "Backups are configured per server."),
        gap(4),
        *bul([
            "<b>Backup Enabled</b> — activates automatic backups for this server",
            "<b>Backup Retention</b> — number of backups to keep (default 5); "
            "older ones are deleted automatically",
            "<b>Create Backup</b> button — takes an immediate backup",
            "<b>Backup All</b> (Dashboard) — backs up every server at once",
            "<b>Restore</b> — extracts a backup into the install folder "
            "(stop the server first)",
            "Stored in: <b>%APPDATA%\\WGS\\backups\\&lt;server-id&gt;\\</b>",
        ]),
        gap(6),
    ]))
    add(note("Disk space",
        "Large servers (ARK, Valheim) produce big backups. "
        "Set a sensible retention value and monitor free disk space.", C_WARN))
    add(note("Auto backup before update",
        "If Backup Enabled is checked, WGS creates a backup automatically before every "
        "Install/Update and before every Auto Update cycle.", C_ACCENT2))

    # ══════════════════════════════════════════════════════════════════════════
    # 18  Scheduled Tasks
    # ══════════════════════════════════════════════════════════════════════════
    add(CondPageBreak(9*cm))
    add(*sec("18.  Scheduled Tasks"))
    add(KeepTogether([
        para("The built-in scheduler runs actions automatically at set times. "
             "Add tasks in the <b>Schedule</b> tab of any server."),
        gap(4),
        *tbl(
            ["Field", "Options / Description"],
            [
                ["Action",      "Restart · Stop · Start · Backup · Update"],
                ["Frequency",   "Once · Daily · Weekly"],
                ["Time of day", "HH:MM (24-hour clock)"],
                ["Day of week", "Monday–Sunday (Weekly only)"],
                ["Enabled",     "Toggle a task on/off without deleting it"],
                ["Last / Next", "Timestamps — Next run is calculated automatically"],
            ],
            [4*cm, PAGE_W - 4*cm]),
        *note("Polling interval",
            "The scheduler checks every 30 seconds, so actual execution may be up to "
            "30 s after the configured time.", C_MID),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 19  Dashboard
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("19.  Dashboard"))
    add(KeepTogether([
        para("Click <b>Dashboard</b> in the sidebar to open the live system overview. "
             "Data refreshes every 2 seconds."),
        gap(4),
        *bul([
            "<b>CPU Usage</b> — overall processor load as a percentage",
            "<b>RAM</b> — used and free memory in GB with percentage bar",
            "<b>Drives</b> — usage and free space for every connected drive",
            "<b>Server Count / Online / Stopped</b> — server status summary",
            "<b>Backup All</b> — triggers an immediate backup of every configured server",
        ]),
        gap(8),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 20  Discord Notifications
    # ══════════════════════════════════════════════════════════════════════════
    add(PageBreak())
    add(*sec("20.  Discord Notifications"))
    add(KeepTogether([
        para("WGS posts rich embed messages to a Discord channel via a webhook URL. "
             "Configure in <b>Settings → Discord Notifications</b>."),
        gap(4),
        *tbl(
            ["Event", "Trigger", "Embed colour"],
            [
                ["Server started", "Status → Running",  "Green"],
                ["Server stopped", "Status → Stopped",  "Red"],
                ["Server crashed", "Status → Error",    "Red"],
                ["Update started", "Status → Updating", "Blue"],
            ],
            [5*cm, 6*cm, PAGE_W - 11*cm]),
    ]))
    add(KeepTogether([
        *steps([
            ("Create webhook",
             "Discord: channel Settings → Integrations → Webhooks → New Webhook → Copy URL."),
            ("Paste URL",  "Open WGS Settings → Discord Notifications → paste into 'Webhook URL'."),
            ("Enable",     "Check 'Enable Discord notifications' and choose which events to send."),
            ("Test",       "Click 'Test' to send a test embed immediately."),
            ("Save",       "Click 'Save'. The URL is encrypted with Windows DPAPI before writing."),
        ]),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 21  Discord Remote Control Bot  (NEW)
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("21.  Discord Remote Control Bot"))
    add(para(
        "The Discord bot lets you control your servers interactively from any Discord channel. "
        "Unlike the notification webhook, the bot listens for commands and replies with results. "
        "Configure in <b>Settings → Discord Remote Control Bot</b>.", justify=True))
    add(gap(4))
    add(*steps([
        ("Create a bot",
         "Go to discord.com/developers/applications → New Application → Bot → Reset Token → Copy Token."),
        ("Set permissions",
         "Under OAuth2 → URL Generator select 'bot' scope and 'Read Messages / View Channels' + "
         "'Send Messages' permissions. Invite the bot to your server."),
        ("Get channel ID",
         "Enable Developer Mode in Discord (Settings → Advanced). Right-click your control channel → "
         "Copy Channel ID."),
        ("Configure WGS",
         "Open WGS Settings → Discord Remote Control Bot. Paste the Bot Token and Channel ID. "
         "Optionally set a custom command prefix (default: !)"),
        ("Restrict access",
         "Enter comma-separated Discord User IDs in 'Allowed User IDs' to limit who can send commands. "
         "Leave blank to allow everyone in the channel."),
        ("Save & test",
         "Click Save, then 'Test connection'. The bot posts a message in the channel if working."),
    ]))
    add(*h2("Bot commands"))
    add(tbl(
        ["Command", "Description"],
        [
            ["!help",                "Lists all available commands"],
            ["!status",              "Shows all servers with their current status"],
            ["!start <name>",        "Starts the named server"],
            ["!stop <name>",         "Stops the named server"],
            ["!restart <name>",      "Restarts the named server"],
            ["!update <name>",       "Runs Install/Update on the named server"],
            ["!backup <name>",       "Creates an immediate backup of the named server"],
            ["!cmd <name> <command>","Sends a console command to the running server"],
        ],
        [5.5*cm, PAGE_W - 5.5*cm]))
    add(note("Command prefix",
        "The default prefix is '!'. Change it in Settings → Command prefix. "
        "Server names are matched case-insensitively.", C_MID))
    add(note("Security",
        "Keep the bot token secret — treat it like a password. "
        "Use Allowed User IDs on public servers to prevent unauthorised control.", C_WARN))

    # ══════════════════════════════════════════════════════════════════════════
    # 22  Web API  (NEW)
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("22.  Web API"))
    add(para(
        "WGS includes a built-in HTTP server that exposes a REST API and a browser-based "
        "management UI. Enable it in <b>Settings → Web Remote Control</b>.", justify=True))
    add(gap(4))
    add(*steps([
        ("Enable",
         "Check 'Enable Web API' in Settings and choose a port (default: 8765)."),
        ("Token",
         "An access token is generated automatically. Copy it or enter your own."),
        ("Save",
         "Click Save — the API starts immediately and the status indicator turns green."),
        ("Open browser UI",
         "Navigate to http://localhost:8765/ui — a management page loads with server controls."),
        ("Remote access",
         "Use your machine's LAN IP (e.g. http://192.168.1.100:8765) from other devices. "
         "For internet access, add a port forwarding rule on your router."),
    ]))
    add(*h2("REST endpoints"))
    add(tbl(
        ["Method", "Endpoint", "Description"],
        [
            ["GET",  "/api/servers",              "List all servers with status and port info"],
            ["GET",  "/api/servers/{id}",         "Get a single server's details"],
            ["POST", "/api/servers/{id}/start",   "Start a server"],
            ["POST", "/api/servers/{id}/stop",    "Stop a server"],
            ["POST", "/api/servers/{id}/restart", "Restart a server"],
            ["POST", "/api/servers/{id}/update",  "Run Install/Update"],
            ["POST", "/api/servers/{id}/backup",  "Create a backup"],
            ["POST", "/api/servers/{id}/cmd",     "Send console command (body: {\"command\":\"...\"})"],
            ["GET",  "/api/system",               "System metrics — CPU, RAM, drives"],
        ],
        [2*cm, 6.5*cm, PAGE_W - 8.5*cm]))
    add(note("Authentication",
        "All /api/* endpoints require the token via the "
        "Authorization: Bearer <token> header or the ?token=<token> query parameter.",
        C_ACCENT))
    add(note("CORS",
        "All origins are allowed — the API can be called from any web page or external tool.",
        C_MID))

    # ══════════════════════════════════════════════════════════════════════════
    # 23  Supported Games
    # ══════════════════════════════════════════════════════════════════════════
    add(PageBreak())

    add(*sec("23.  Supported Games"))
    CW = [6*cm, 3*cm, 3*cm, PAGE_W - 12*cm]

    add(*h2("Survival"))
    add(tbl(
        ["Game", "AppID", "Max Players", "Default Port"],
        [
            ["Valheim",               "896660",  "10",  "2456"],
            ["Rust",                  "258550",  "100", "28015"],
            ["7 Days to Die",         "294420",  "8",   "26900"],
            ["Conan Exiles",          "443030",  "40",  "7777"],
            ["ARK: Survival Evolved", "376030",  "70",  "7777"],
            ["Sons of the Forest",    "2465200", "8",   "8766"],
            ["The Forest",            "556450",  "8",   "27015"],
            ["Survive the Nights",    "1502300", "16",  "7777"],
            ["SCUM",                  "3792580", "32",  "10000"],
            ["Vein",                  "2131400", "16",  "7777"],
        ], CW))

    add(*h2("Racing"))
    add(tbl(
        ["Game", "AppID", "Max Players", "Default Port"],
        [
            ["Wreckfest",     "361580",  "24", "33540"],
            ["Wreckfest 2",   "3519390", "24", "27020"],
            ["Assetto Corsa", "302550",  "18", "9600"],
        ], CW))

    add(*h2("Other"))
    add(tbl(
        ["Game", "AppID", "Max Players", "Default Port"],
        [
            ["Minecraft Java",         "—",       "20", "25565"],
            ["Euro Truck Simulator 2", "1948160", "8",  "27015"],
            ["Arma Reforger",          "1874900", "64", "2001"],
            ["Black Mesa",             "346680",  "24", "27015"],
        ], CW))

    # ══════════════════════════════════════════════════════════════════════════
    # 24  Custom Plugin Creator
    # ══════════════════════════════════════════════════════════════════════════
    add(PageBreak())

    add(*sec("24.  Custom Plugin Creator"))
    add(para(
        "The Plugin Creator lets you add any game server without writing code. "
        "Open it from <b>Tools → Plugin Creator</b>.", justify=True))
    add(gap(4))
    add(tbl(
        ["Field", "Req.", "Description"],
        [
            ["Game ID",            "Yes", "Unique identifier, e.g. 'mygame'"],
            ["Game Name",          "Yes", "Display name in the game dropdown"],
            ["Description",        "No",  "Short description shown in the Info tab"],
            ["Category",           "No",  "Survival / Racing / Shooter / etc."],
            ["Steam AppID",        "Yes", "Used by SteamCMD to download the server files"],
            ["Executable",         "Yes", "Server .exe — relative path from install folder"],
            ["Default Port",       "Yes", "Game port"],
            ["Default Query Port", "Yes", "Steam Query port"],
            ["Default Steam Port", "No",  "Steam network port (0 = not used)"],
            ["Max Players",        "Yes", "Default player count"],
            ["Start Arguments",    "Yes", "Command-line args (supports placeholder variables)"],
            ["Stop Command",       "No",  "Graceful shutdown command (blank = kill process)"],
        ],
        [4.5*cm, 1.5*cm, PAGE_W - 6*cm]))

    add(*h3("Placeholder variables for Start Arguments"))
    add(KeepTogether([
        *[Paragraph(
            f"  <font color='#3ecf8e'><b>{v}</b></font>"
            f"  <font color='#94a3b8'>→</font>  {d}",
            ST["bullet"])
          for v, d in [
              ("{ip}",      "ServerIp"),
              ("{port}",    "ServerPort"),
              ("{qport}",   "QueryPort"),
              ("{name}",    "ServerName"),
              ("{max}",     "MaxPlayers"),
              ("{map}",     'GameSpecificSettings["map"]'),
              ("{password}","ServerPassword"),
          ]],
        gap(8),
    ]))

    add(*h2("24.2  Import / Export plugins"))
    add(KeepTogether([
        para("Plugins can be exported to a .cs source file and imported on another machine."),
        gap(4),
        *bul([
            "<b>Export</b>: Tools → Export Plugin → select a plugin → save as .cs file",
            "<b>Import</b>: Tools → Import Plugin → select a .cs file — the plugin is "
            "compiled in memory and registered immediately without restarting WGS",
            "Exported files are self-contained C# source files — inspect, edit and share freely",
        ]),
        gap(8),
    ]))

    # ══════════════════════════════════════════════════════════════════════════
    # 25  Troubleshooting
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("25.  Troubleshooting"))
    for title, desc in [
        ("Server won't start — 'executable not found'",
         "Game files are not installed or the executable path is wrong. "
         "Re-run 'Install / Update', or check manually that the .exe exists in the install folder."),
        ("SteamCMD fails — ERROR! App state is 0x…",
         "Usually a network issue or Steam servers are temporarily down. "
         "Try again shortly. If login is required, verify your Steam Guard code."),
        ("Port already in use (pre-flight warning)",
         "Another program is using the same port. Change the port in Settings "
         "or stop the conflicting program."),
        ("Wreckfest crashes on startup",
         "Wreckfest needs UseNativeConsole=true — already set in the plugin. "
         "Verify the install path and that server_config.cfg exists."),
        ("RCON won't connect",
         "Confirm the RCON port and password match the server's config file "
         "and that a firewall rule exists for that port."),
        ("Backup fails",
         "Disk space may be full or WGS lacks write permission to %APPDATA%. "
         "Check the Windows Event Log for details."),
        ("Discord bot won't connect",
         "Verify the bot token and channel ID are correct. The bot needs Read Messages and "
         "Send Messages permissions in that channel. Check the bot status in Settings."),
        ("Web API returns 401",
         "The request is missing the token. Add Authorization: Bearer <token> header or "
         "append ?token=<token> to the URL."),
        ("Auto-restart disabled — crash limit reached",
         "The server crashed too many times in 10 minutes. Fix the underlying problem, "
         "then re-enable Auto Restart in the server's Settings tab."),
        ("App won't launch — .NET error",
         "Install .NET 8 Runtime: dotnet.microsoft.com/download/dotnet/8.0"),
        ("Server processes survive after WGS closes",
         "Always stop servers with the Stop button before closing WGS. "
         "Force-closing WGS via Task Manager may leave server processes running."),
    ]:
        S.extend(note(f"? {title}", desc, C_WARN))

    # ══════════════════════════════════════════════════════════════════════════
    # 26 + 27  Keyboard Shortcuts & File Locations
    # ══════════════════════════════════════════════════════════════════════════
    add(*sec("26.  Keyboard Shortcuts"))
    add(tbl(
        ["Shortcut", "Action"],
        [
            ["Ctrl + V", "Paste   (all text fields)"],
            ["Ctrl + C", "Copy    (all text fields)"],
            ["Ctrl + X", "Cut     (all text fields)"],
            ["Ctrl + A", "Select all  (text fields)"],
            ["Enter",    "Send console / RCON command"],
        ],
        [3.5*cm, PAGE_W - 3.5*cm]))

    add(*sec("27.  File Locations"))
    add(para("WGS stores configuration in Windows AppData and writes nothing permanent "
             "to its own installation directory."))
    add(gap(5))
    add(tbl(
        ["Path", "Contents"],
        [
            ["%APPDATA%\\WGS\\servers.json",        "All server configurations"],
            ["%APPDATA%\\WGS\\settings.json",        "Global application settings"],
            ["%APPDATA%\\WGS\\notifications.json",   "Discord notification settings"],
            ["%APPDATA%\\WGS\\scheduled_tasks.json", "Scheduled task definitions"],
            ["%APPDATA%\\WGS\\custom_plugins.json",  "Custom Plugin Creator entries"],
            ["%APPDATA%\\WGS\\player_stats.db",      "Player session history (SQLite)"],
            ["%APPDATA%\\WGS\\steamcmd\\",           "SteamCMD installation (auto-downloaded)"],
            ["%APPDATA%\\WGS\\backups\\",            "Server backup zip files"],
        ],
        [7.5*cm, PAGE_W - 7.5*cm]))
    add(note("Config backup tip",
        "Copy %APPDATA%\\WGS\\servers.json before reinstalling WGS — "
        "all server definitions are stored in that one file.", C_ACCENT2))

    return S


# ── Build ─────────────────────────────────────────────────────────────────────
def build_pdf():
    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)

    doc = BaseDocTemplate(
        OUTPUT, pagesize=A4,
        leftMargin=L_MARGIN, rightMargin=R_MARGIN,
        topMargin=T_MARGIN,  bottomMargin=B_MARGIN,
        title="Windows Game Server — User Manual",
        author=AUTHOR, subject="User Manual",
    )
    content_frame = Frame(
        doc.leftMargin, doc.bottomMargin,
        doc.width, doc.height, id="normal")
    cover_frame = Frame(0, 0, W, H, id="cover")

    doc.addPageTemplates([
        PageTemplate(id="Cover",   frames=[cover_frame],   onPage=cover_page),
        PageTemplate(id="Content", frames=[content_frame], onPage=ContentPage()),
    ])

    from reportlab.platypus import NextPageTemplate
    story = [NextPageTemplate("Content"), PageBreak()] + build_story()
    doc.build(story)
    print(f"PDF created: {OUTPUT}")


if __name__ == "__main__":
    build_pdf()
