"""Mede o clamd direto pelo INSTREAM, sem MinIO nem verificador.

  deteccao <arquivos> <saida.jsonl>                 veredicto de cada arquivo nas configurações de CONFIGS
  carga <arquivos> <saida.jsonl>                    vazão e latência por tipo, tamanho e paralelismo (CONFIG_CARGA, padrão aprovada)
  recarga <arquivos> <saida.jsonl>                  memória e latência durante o RELOAD da base, nas configurações de CONFIGS

Uso: python3 -I medir.py <modo> <diretório dos arquivos> <saída>
"""
import json
import os
import socket
import statistics
import struct
import subprocess
import sys
import threading
import time
from pathlib import Path

CLAMDS = {"hml-atual": 13320, "aprovada": 13321, "final": 13322, "final-alerta": 13323, "final-limites": 13324, "adotada": 13325}
# sem CONFIGS, detecção e recarga rodam nas duas primeiras, como nos resultados publicados
PADRAO = "hml-atual,aprovada"


def configs():
    so = os.environ.get("CONFIGS", PADRAO).split(",")
    return {n: p for n, p in CLAMDS.items() if n in so}
CHUNK = 64 * 1024


def cgroup(nome: str) -> Path:
    cid = subprocess.run(["docker", "inspect", "-f", "{{.Id}}", f"medicao-clamd-{nome}"],
                         check=True, capture_output=True, text=True).stdout.strip()
    return Path(f"/sys/fs/cgroup/system.slice/docker-{cid}.scope")


def cpu_usec(cg: Path) -> int:
    for linha in (cg / "cpu.stat").read_text().splitlines():
        if linha.startswith("usage_usec"):
            return int(linha.split()[1])
    raise RuntimeError("cpu.stat sem usage_usec")


def mem_mib(cg: Path) -> float:
    return int((cg / "memory.current").read_text()) / 1024 / 1024


def anon_mib(cg: Path) -> float:
    """Memória anônima: a que o kernel não recupera descartando cache de arquivo."""
    for linha in (cg / "memory.stat").read_text().splitlines():
        if linha.startswith("anon "):
            return int(linha.split()[1]) / 1024 / 1024
    raise RuntimeError("memory.stat sem anon")


def instream(porta: int, dados: bytes, timeout: float = 120) -> str:
    with socket.create_connection(("127.0.0.1", porta), timeout=timeout) as s:
        s.sendall(b"zINSTREAM\0")
        for i in range(0, len(dados), CHUNK):
            parte = dados[i:i + CHUNK]
            s.sendall(struct.pack("!L", len(parte)) + parte)
        s.sendall(struct.pack("!L", 0))
        resp = b""
        while not resp.endswith(b"\0"):
            r = s.recv(4096)
            if not r:
                break
            resp += r
    return resp.rstrip(b"\0").decode().removeprefix("stream: ")


def comando(porta: int, cmd: str) -> str:
    with socket.create_connection(("127.0.0.1", porta), timeout=30) as s:
        s.sendall(f"z{cmd}\0".encode())
        return s.recv(4096).rstrip(b"\0").decode()


def pct(xs, p):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(round(p / 100 * (len(xs) - 1))))]


def deteccao(arquivos: Path, saida):
    for f in sorted(arquivos.iterdir()):
        dados = f.read_bytes()
        for nome, porta in configs().items():
            t0 = time.perf_counter()
            veredicto = instream(porta, dados)
            ms = (time.perf_counter() - t0) * 1000
            reg = {"modo": "deteccao", "arquivo": f.name, "mb": round(len(dados) / 2**20, 2), "config": nome,
                   "veredicto": veredicto, "ms": round(ms, 1)}
            print(json.dumps(reg, ensure_ascii=False), file=saida, flush=True)
            print(f"{f.name:26} {nome:10} {ms:8.1f} ms  {veredicto}")


def rodar_carga(porta, cg, dados, paralelismo, total):
    lat, erros, lock, fila = [], [], threading.Lock(), list(range(total))

    def trabalhador():
        while True:
            with lock:
                if not fila:
                    return
                fila.pop()
            t0 = time.perf_counter()
            try:
                v = instream(porta, dados)
                if v != "OK":
                    erros.append(v)
            except Exception as e:  # noqa: BLE001 - a medição registra qualquer falha
                erros.append(repr(e))
            with lock:
                lat.append((time.perf_counter() - t0) * 1000)

    pico, pico_anon = [mem_mib(cg)], [anon_mib(cg)]
    parar = threading.Event()

    def amostrar():
        while not parar.is_set():
            pico[0] = max(pico[0], mem_mib(cg))
            pico_anon[0] = max(pico_anon[0], anon_mib(cg))
            time.sleep(0.05)

    a = threading.Thread(target=amostrar)
    a.start()
    c0, t0 = cpu_usec(cg), time.perf_counter()
    ts = [threading.Thread(target=trabalhador) for _ in range(paralelismo)]
    for t in ts:
        t.start()
    for t in ts:
        t.join()
    parede = time.perf_counter() - t0
    cpu = (cpu_usec(cg) - c0) / 1e6 / parede
    parar.set()
    a.join()
    return {"arquivos": total, "falhas": len(erros), "erros": sorted(set(erros))[:3], "parede_s": round(parede, 2),
            "arq_s": round(total / parede, 2), "mb_s": round(total * len(dados) / 2**20 / parede, 1),
            "p50_ms": round(statistics.median(lat), 1), "p95_ms": round(pct(lat, 95), 1),
            "max_ms": round(max(lat), 1), "cpu_media": round(cpu, 2), "pico_mib": round(pico[0]),
            "pico_anon_mib": round(pico_anon[0])}


def carga(arquivos: Path, saida):
    nome = os.environ.get("CONFIG_CARGA", "aprovada")
    porta = CLAMDS[nome]
    cg = cgroup(nome)
    so = os.environ.get("ARQUIVOS")  # lista separada por vírgula, para repetir só alguns casos
    casos = [f for f in sorted(arquivos.iterdir())
             if not f.name.startswith("eicar") and (not so or f.name in so.split(","))]
    for f in casos:
        dados = f.read_bytes()
        mb = len(dados) / 2**20
        instream(porta, dados)  # aquece
        for par in [int(x) for x in os.environ.get("PARALELISMOS", "1,4,8").split(",")]:
            total = max(par * 4, min(int(os.environ.get("TOTAL_MAX", 200)), int(400 / max(mb, 1))))
            r = rodar_carga(porta, cg, dados, par, total)
            reg = {"modo": "carga", "config": nome, "arquivo": f.name, "mb": round(mb, 2), "paralelismo": par, **r}
            print(json.dumps(reg, ensure_ascii=False), file=saida, flush=True)
            print(f"{f.name:26} par={par} {r['arq_s']:6.2f} arq/s {r['mb_s']:6.1f} MB/s "
                  f"p50={r['p50_ms']:8.1f} p95={r['p95_ms']:8.1f} cpu={r['cpu_media']:.2f} pico={r['pico_mib']} anon={r['pico_anon_mib']} MiB "
                  f"falhas={r['falhas']}")


def recarga(arquivos: Path, saida):
    dados = (arquivos / "digitalizado-5mb.pdf").read_bytes()
    for nome, porta in configs().items():
        cg = cgroup(nome)
        time.sleep(5)
        base = mem_mib(cg)
        lat, erros, parar = [], [], threading.Event()
        mem = []

        def varrer():
            while not parar.is_set():
                t0 = time.perf_counter()
                try:
                    v = instream(porta, dados, timeout=60)
                    if v != "OK":
                        erros.append(v)
                except Exception as e:  # noqa: BLE001
                    erros.append(repr(e))
                lat.append(((time.perf_counter() - t0) * 1000, t0))

        ts = [threading.Thread(target=varrer) for _ in range(2)]
        for t in ts:
            t.start()
        time.sleep(3)
        t_reload = time.perf_counter()
        resp = comando(porta, "RELOAD")
        # janela fixa de 150 s depois do RELOAD; a recarga sem carga leva ~13 s
        fim = time.perf_counter() + 150
        while time.perf_counter() < fim:
            mem.append((time.perf_counter() - t_reload, mem_mib(cg), anon_mib(cg)))
            time.sleep(0.05)
        parar.set()
        for t in ts:
            t.join()
        durante = [ms for ms, t0 in lat if t0 >= t_reload - 1 and t0 <= t_reload + 120]
        pico = max(m for _, m, _ in mem)
        pico_anon = max(a for _, _, a in mem)
        t_pico = next(t for t, m, _ in mem if m == pico)
        reg = {"modo": "recarga", "config": nome, "resposta_reload": resp, "base_mib": round(base),
               "pico_mib": round(pico), "pico_anon_mib": round(pico_anon), "pico_apos_s": round(t_pico, 1),
               "final_mib": round(mem[-1][1]),
               "verificacoes_durante": len(durante), "latencia_max_durante_ms": round(max(durante), 1) if durante else None,
               "latencia_p50_durante_ms": round(statistics.median(durante), 1) if durante else None,
               "falhas": len(erros), "erros": sorted(set(erros))[:3]}
        print(json.dumps(reg, ensure_ascii=False), file=saida, flush=True)
        print(reg)


def main():
    modo, arquivos, destino = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3])
    with destino.open("a") as saida:
        {"deteccao": deteccao, "carga": carga, "recarga": recarga}[modo](arquivos, saida)


if __name__ == "__main__":
    main()
