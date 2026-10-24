"""RELOAD da base com o clamd sob carga de JPEG de 20 MB e paralelismo 4.

Uso: python3 -I recarga_sob_carga.py <arquivos> <saída.jsonl> <nome do clamd> <porta>

A janela "durante" cobre os 60 s amostrados depois do RELOAD.
"""
import json
import subprocess
import sys
import threading
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from medir import anon_mib, cgroup, comando, instream, mem_mib  # noqa: E402

arquivos, saida, nome, porta = Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3], int(sys.argv[4])
dados = (arquivos / "jpeg-20mb.jpg").read_bytes()
cg = cgroup(nome)
parar, lat, erros, mem = threading.Event(), [], [], []


def varrer():
    while not parar.is_set():
        t0 = time.perf_counter()
        try:
            v = instream(porta, dados, timeout=120)
            if v != "OK":
                erros.append(v)
        except Exception as e:  # noqa: BLE001
            erros.append(repr(e))
        lat.append(((time.perf_counter() - t0) * 1000, t0, time.perf_counter()))


def recargas_no_log():
    return int(subprocess.run(["docker", "exec", f"medicao-clamd-{nome}", "sh", "-c",
                               "grep -c 'Database correctly reloaded' /var/log/clamav/clamd.log"],
                              capture_output=True, text=True).stdout.strip() or 0)


antes_log = recargas_no_log()
ts = [threading.Thread(target=varrer) for _ in range(4)]
for t in ts:
    t.start()
time.sleep(10)
t_reload = time.perf_counter()
resp = comando(porta, "RELOAD")
while time.perf_counter() < t_reload + 60:
    mem.append((time.perf_counter() - t_reload, mem_mib(cg), anon_mib(cg)))
    time.sleep(0.05)
parar.set()
for t in ts:
    t.join()
# cada verificação conta pelo intervalo em que esteve em andamento: a que atravessa o RELOAD é "durante"
antes = [ms for ms, t0, fim in lat if fim < t_reload]
# a janela cobre toda a amostragem de memória, para não deixar de fora o fim da recarga
durante = [ms for ms, t0, fim in lat if fim >= t_reload and t0 <= t_reload + 60]
log = recargas_no_log()
reg = {"modo": "recarga-sob-carga", "config": nome, "arquivo": "jpeg-20mb.jpg", "paralelismo": 4, "resposta": resp,
       "pico_mib": round(max(m for _, m, _ in mem)), "pico_anon_mib": round(max(a for _, _, a in mem)),
       "final_mib": round(mem[-1][1]), "verificacoes_antes": len(antes), "verificacoes_durante": len(durante),
       "latencia_max_antes_ms": round(max(antes), 1), "latencia_max_durante_ms": round(max(durante), 1) if durante else None,
       "falhas": len(erros), "erros": sorted(set(erros))[:3], "recargas_concluidas_no_log": log - antes_log,
       "pico_apos_s": round(next(t for t, m, _ in mem if m == max(x for _, x, _ in mem)), 1),
       "versao_depois": comando(porta, "VERSION")}
saida.open("a").write(json.dumps(reg, ensure_ascii=False) + "\n")
print(reg)
