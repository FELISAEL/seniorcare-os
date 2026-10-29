#!/usr/bin/python3
"""Monitor de rendimiento de SeniorCare OS (estilo Administrador de tareas).

Muestra CPU total y por núcleo, memoria, lectura/escritura de disco y el
consumo de RAM/CPU de cada aplicación abierta. Solo usa psutil y tkinter.
La paleta coincide con la del portal SeniorCare (verde azulado).
"""
import os
import socket
import time
import tkinter as tk
from collections import deque
from tkinter import font as tkfont
from tkinter import messagebox, ttk

import psutil

REFRESH_MS = 1500
HISTORY = 60
MB = 1024 * 1024

# Paleta del portal SeniorCare
BG = "#f2f8f6"
CARD = "#ffffff"
LINE = "#e2efec"
INK = "#102a43"
MUTED = "#6b7d78"
TEAL = "#1f8a7a"
TEAL_DARK = "#17695d"
TEAL_SOFT = "#e5f5f2"
AMBER = "#d9954a"
RED = "#c82d34"


def blend(color, ratio, base="#ffffff"):
    """Mezcla un color con blanco para rellenos suaves (sin depender de stipple)."""
    c = [int(color[i:i + 2], 16) for i in (1, 3, 5)]
    b = [int(base[i:i + 2], 16) for i in (1, 3, 5)]
    return "#%02x%02x%02x" % tuple(int(b[i] + (c[i] - b[i]) * ratio) for i in range(3))


def fmt_bytes(n):
    for unit in ("B", "KB", "MB", "GB", "TB"):
        if n < 1024 or unit == "TB":
            return f"{n:.0f} {unit}" if unit == "B" else f"{n:.1f} {unit}"
        n /= 1024


def level_color(pct):
    return RED if pct >= 90 else AMBER if pct >= 70 else TEAL


class Graph(tk.Canvas):
    """Gráfica de área con una o dos series y rejilla suave."""

    def __init__(self, master, colors=(TEAL,), maximum=100.0, **kw):
        super().__init__(master, height=96, bg=CARD, highlightthickness=1,
                         highlightbackground=LINE, **kw)
        self.colors = colors
        self.maximum = maximum
        self.fixed = maximum
        self.series = [deque([0.0] * HISTORY, maxlen=HISTORY) for _ in colors]
        self.bind("<Configure>", lambda _e: self.redraw())

    def push(self, *values, maximum=None):
        for s, v in zip(self.series, values):
            s.append(v)
        if maximum is not None:
            self.maximum = maximum
        self.redraw()

    def redraw(self):
        self.delete("all")
        w, h = max(self.winfo_width(), 10), max(self.winfo_height(), 10)
        for i in range(1, 4):
            y = h * i / 4
            self.create_line(0, y, w, y, fill=LINE)
        peak, step = max(self.maximum, 0.001), w / (HISTORY - 1)
        for color, data in zip(self.colors, self.series):
            pts = []
            for i, v in enumerate(data):
                pts += [i * step, h - 2 - (min(v, peak) / peak) * (h - 6)]
            self.create_polygon([0, h] + pts + [w, h], fill=blend(color, 0.18), outline="")
            self.create_line(pts, fill=color, width=2, smooth=False)


class Bar(tk.Canvas):
    """Barra de progreso redondeada que cambia de color según la carga."""

    def __init__(self, master, **kw):
        super().__init__(master, height=14, bg=CARD, highlightthickness=0, **kw)
        self.value = 0.0
        self.bind("<Configure>", lambda _e: self.draw())

    def set(self, value):
        self.value = value
        self.draw()

    def draw(self):
        self.delete("all")
        w = max(self.winfo_width(), 10)
        self.create_rectangle(0, 2, w, 12, fill=LINE, outline="")
        self.create_rectangle(0, 2, w * self.value / 100, 12,
                              fill=level_color(self.value), outline="")


class Card(tk.Frame):
    """Tarjeta blanca con título, valor grande y subtítulo."""

    def __init__(self, master, title, fonts):
        super().__init__(master, bg=CARD, highlightthickness=1, highlightbackground=LINE)
        tk.Label(self, text=title.upper(), bg=CARD, fg=MUTED, font=fonts["small"],
                 anchor="w").pack(fill="x", padx=14, pady=(10, 0))
        self.big = tk.Label(self, text="-", bg=CARD, fg=INK, font=fonts["big"], anchor="w")
        self.big.pack(fill="x", padx=14)
        self.sub = tk.Label(self, text="", bg=CARD, fg=MUTED, font=fonts["body"], anchor="w")
        self.sub.pack(fill="x", padx=14, pady=(0, 6))


class Monitor(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Monitor de rendimiento - SeniorCare OS")
        self.geometry("980x680")
        self.minsize(820, 580)
        self.configure(bg=BG)
        base = tkfont.nametofont("TkDefaultFont").actual("family")
        self.fonts = {
            "big": (base, 24, "bold"), "title": (base, 15, "bold"),
            "body": (base, 10), "small": (base, 9, "bold"), "bold": (base, 10, "bold"),
        }
        self.sort_col, self.sort_rev = "ram", True
        self.last_disk = psutil.disk_io_counters()
        self.last_time = time.monotonic()
        psutil.cpu_percent(percpu=True)
        self.setup_style()
        self.build_header()

        tabs = ttk.Notebook(self)
        tabs.pack(fill="both", expand=True, padx=14, pady=(10, 14))
        self.tab_perf = tk.Frame(tabs, bg=BG)
        self.tab_proc = tk.Frame(tabs, bg=BG)
        tabs.add(self.tab_perf, text="  Rendimiento  ")
        tabs.add(self.tab_proc, text="  Aplicaciones y procesos  ")
        self.build_perf()
        self.build_proc()
        self.tick()

    # ---------- Estilo ----------
    def setup_style(self):
        s = ttk.Style(self)
        s.theme_use("clam")
        s.configure("TNotebook", background=BG, borderwidth=0, tabmargins=(0, 0, 0, 0))
        s.configure("TNotebook.Tab", background=TEAL_SOFT, foreground=TEAL_DARK,
                    padding=(14, 8), font=self.fonts["bold"], borderwidth=0)
        s.map("TNotebook.Tab", background=[("selected", TEAL)],
              foreground=[("selected", "#ffffff")])
        s.configure("Treeview", background=CARD, fieldbackground=CARD, foreground=INK,
                    rowheight=28, borderwidth=0, font=self.fonts["body"])
        s.configure("Treeview.Heading", background=TEAL, foreground="#ffffff",
                    font=self.fonts["bold"], relief="flat", padding=(8, 6))
        s.map("Treeview.Heading", background=[("active", TEAL_DARK)])
        s.map("Treeview", background=[("selected", "#cfeee8")], foreground=[("selected", INK)])
        s.configure("Vertical.TScrollbar", background=TEAL_SOFT, troughcolor=BG,
                    bordercolor=BG, arrowcolor=TEAL)
        s.configure("Accent.TButton", background=TEAL, foreground="#ffffff",
                    font=self.fonts["bold"], padding=(16, 8), borderwidth=0)
        s.map("Accent.TButton", background=[("active", TEAL_DARK)])

    def build_header(self):
        head = tk.Frame(self, bg=CARD, highlightthickness=1, highlightbackground=LINE)
        head.pack(fill="x")
        logo = tk.Canvas(head, width=44, height=44, bg=CARD, highlightthickness=0)
        logo.create_rectangle(2, 2, 42, 42, fill=TEAL, outline="")
        logo.create_text(22, 22, text="SC", fill="#ffffff", font=(self.fonts["title"][0], 14, "bold"))
        logo.pack(side="left", padx=(16, 10), pady=10)
        txt = tk.Frame(head, bg=CARD)
        txt.pack(side="left")
        tk.Label(txt, text="Monitor de rendimiento", bg=CARD, fg=INK,
                 font=self.fonts["title"]).pack(anchor="w")
        self.lbl_host = tk.Label(txt, text="", bg=CARD, fg=MUTED, font=self.fonts["body"])
        self.lbl_host.pack(anchor="w")

    # ---------- Pestaña Rendimiento ----------
    def build_perf(self):
        p = self.tab_perf
        for c in (0, 1):
            p.columnconfigure(c, weight=1, uniform="col")
        p.rowconfigure(0, weight=1)
        p.rowconfigure(1, weight=1)
        pad = {"padx": 6, "pady": 6, "sticky": "nsew"}

        self.c_cpu = Card(p, "CPU", self.fonts)
        self.c_cpu.grid(row=0, column=0, **pad)
        self.g_cpu = Graph(self.c_cpu)
        self.g_cpu.pack(fill="both", expand=True, padx=14, pady=(0, 12))

        self.c_mem = Card(p, "Memoria", self.fonts)
        self.c_mem.grid(row=0, column=1, **pad)
        self.g_mem = Graph(self.c_mem, (TEAL,))
        self.g_mem.pack(fill="both", expand=True, padx=14, pady=(0, 12))

        self.c_disk = Card(p, "Disco", self.fonts)
        self.c_disk.grid(row=1, column=0, **pad)
        self.g_disk = Graph(self.c_disk, (TEAL, AMBER), maximum=1.0)
        self.g_disk.pack(fill="both", expand=True, padx=14, pady=(0, 4))
        legend = tk.Label(self.c_disk, text="●  Lectura      ", bg=CARD, fg=TEAL,
                          font=self.fonts["body"], anchor="w")
        legend.pack(side="left", padx=(14, 0), pady=(0, 10))
        tk.Label(self.c_disk, text="●  Escritura", bg=CARD, fg=AMBER,
                 font=self.fonts["body"]).pack(side="left", pady=(0, 10))

        cores = tk.Frame(p, bg=CARD, highlightthickness=1, highlightbackground=LINE)
        cores.grid(row=1, column=1, **pad)
        tk.Label(cores, text="NÚCLEOS DEL CPU", bg=CARD, fg=MUTED, font=self.fonts["small"],
                 anchor="w").pack(fill="x", padx=14, pady=(10, 4))
        area = tk.Frame(cores, bg=CARD)
        area.pack(fill="both", expand=True, padx=14, pady=(0, 10))
        n = psutil.cpu_count(logical=True) or 1
        cols = 2 if n <= 8 else 4
        self.core_widgets = []
        for i in range(n):
            cell = tk.Frame(area, bg=CARD)
            cell.grid(row=i // cols, column=i % cols, sticky="ew", padx=(0, 12), pady=3)
            area.columnconfigure(i % cols, weight=1)
            lbl = tk.Label(cell, text=f"Núcleo {i}", bg=CARD, fg=INK,
                           font=self.fonts["body"], anchor="w")
            lbl.pack(fill="x")
            bar = Bar(cell)
            bar.pack(fill="x")
            self.core_widgets.append((lbl, bar))

    # ---------- Pestaña Procesos ----------
    def build_proc(self):
        p = self.tab_proc
        p.rowconfigure(0, weight=1)
        p.columnconfigure(0, weight=1)
        cols = ("name", "count", "cpu", "ram", "read", "write")
        heads = {"name": "Aplicación", "count": "Procesos", "cpu": "CPU %",
                 "ram": "RAM", "read": "Lectura disco", "write": "Escritura disco"}
        box = tk.Frame(p, bg=CARD, highlightthickness=1, highlightbackground=LINE)
        box.grid(row=0, column=0, sticky="nsew", pady=(6, 0))
        box.rowconfigure(0, weight=1)
        box.columnconfigure(0, weight=1)
        self.tree = ttk.Treeview(box, columns=cols, show="headings", selectmode="browse")
        for c in cols:
            self.tree.heading(c, text=heads[c], anchor="w" if c == "name" else "e",
                              command=lambda c=c: self.sort_by(c))
            self.tree.column(c, width=100 if c != "name" else 280,
                             anchor="w" if c == "name" else "e")
        self.tree.tag_configure("odd", background="#f7fbfa")
        self.tree.tag_configure("hot", foreground=RED)
        sb = ttk.Scrollbar(box, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=sb.set)
        self.tree.grid(row=0, column=0, sticky="nsew")
        sb.grid(row=0, column=1, sticky="ns")
        foot = tk.Frame(p, bg=BG)
        foot.grid(row=1, column=0, sticky="ew", pady=(10, 0))
        self.lbl_summary = tk.Label(foot, bg=BG, fg=MUTED, font=self.fonts["body"])
        self.lbl_summary.pack(side="left")
        ttk.Button(foot, text="Finalizar tarea", style="Accent.TButton",
                   command=self.kill_selected).pack(side="right")

    def sort_by(self, col):
        self.sort_rev = not self.sort_rev if self.sort_col == col else col != "name"
        self.sort_col = col

    def kill_selected(self):
        sel = self.tree.selection()
        if not sel:
            return
        name = sel[0]
        if not messagebox.askyesno("Finalizar tarea",
                                   f"¿Cerrar todos los procesos de «{name}»?"):
            return
        me = os.getuid()
        for proc in psutil.process_iter(["name", "uids"]):
            try:
                if proc.info["name"] == name and proc.info["uids"].real == me:
                    proc.terminate()
            except (psutil.Error, AttributeError):
                pass

    # ---------- Actualización ----------
    def tick(self):
        try:
            self.update_perf()
            self.update_procs()
        except Exception as exc:  # el monitor nunca debe cerrarse por un fallo puntual
            self.title(f"Monitor de rendimiento - error: {exc}")
        self.after(REFRESH_MS, self.tick)

    def update_perf(self):
        up = int(time.time() - psutil.boot_time())
        self.lbl_host.config(
            text=f"{socket.gethostname()}  ·  encendido hace {up // 3600} h {up % 3600 // 60} min")

        per_core = psutil.cpu_percent(percpu=True)
        total = sum(per_core) / len(per_core)
        freq = psutil.cpu_freq()
        self.c_cpu.big.config(text=f"{total:.0f} %", fg=level_color(total))
        self.c_cpu.sub.config(text=f"{len(per_core)} núcleos lógicos"
                              + (f"  ·  {freq.current / 1000:.2f} GHz" if freq else ""))
        self.g_cpu.push(total)
        for i, ((lbl, bar), v) in enumerate(zip(self.core_widgets, per_core)):
            lbl.config(text=f"Núcleo {i}   {v:.0f} %")
            bar.set(v)

        vm, sw = psutil.virtual_memory(), psutil.swap_memory()
        self.c_mem.big.config(text=f"{vm.percent:.0f} %", fg=level_color(vm.percent))
        self.c_mem.sub.config(
            text=f"{fmt_bytes(vm.used)} de {fmt_bytes(vm.total)}  ·  swap {fmt_bytes(sw.used)}")
        self.g_mem.push(vm.percent)

        disk, now = psutil.disk_io_counters(), time.monotonic()
        dt = max(now - self.last_time, 0.001)
        if disk and self.last_disk:
            rd = (disk.read_bytes - self.last_disk.read_bytes) / dt / MB
            wr = (disk.write_bytes - self.last_disk.write_bytes) / dt / MB
        else:
            rd = wr = 0.0
        self.last_disk, self.last_time = disk, now
        self.c_disk.big.config(text=f"{rd + wr:.2f} MB/s")
        self.c_disk.sub.config(text=f"Lectura {rd:.2f} MB/s  ·  Escritura {wr:.2f} MB/s")
        peak = max(1.0, max(max(s) for s in self.g_disk.series) * 1.2)
        self.g_disk.push(rd, wr, maximum=peak)

    def update_procs(self):
        groups = {}
        ncpu = psutil.cpu_count(logical=True) or 1
        for proc in psutil.process_iter(["name", "memory_info"]):
            try:
                g = groups.setdefault(proc.info["name"] or "?",
                                      {"count": 0, "cpu": 0.0, "ram": 0, "read": 0, "write": 0})
                g["count"] += 1
                g["cpu"] += proc.cpu_percent(None) / ncpu
                mem = proc.info["memory_info"]
                g["ram"] += mem.rss if mem else 0
                try:
                    io = proc.io_counters()
                    g["read"] += io.read_bytes
                    g["write"] += io.write_bytes
                except (psutil.Error, AttributeError):
                    pass
            except psutil.Error:
                continue

        key = self.sort_col
        rows = sorted(groups.items(),
                      key=lambda kv: kv[0].lower() if key == "name" else kv[1][key],
                      reverse=self.sort_rev)
        selected = self.tree.selection()
        self.tree.delete(*self.tree.get_children())
        for i, (name, g) in enumerate(rows[:200]):
            tags = ("odd",) if i % 2 else ()
            if g["cpu"] >= 50:
                tags += ("hot",)
            self.tree.insert("", "end", iid=name, tags=tags, values=(
                name, g["count"], f"{g['cpu']:.1f}", fmt_bytes(g["ram"]),
                fmt_bytes(g["read"]), fmt_bytes(g["write"])))
        if selected and self.tree.exists(selected[0]):
            self.tree.selection_set(selected[0])
        self.lbl_summary.config(
            text=f"{len(groups)} aplicaciones  ·  {sum(g['count'] for g in groups.values())} procesos")


if __name__ == "__main__":
    Monitor().mainloop()
