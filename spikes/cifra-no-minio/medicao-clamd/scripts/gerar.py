"""Gera o conjunto de arquivos da medição do clamd com PDFs e imagens.

Tipos:
  digitalizado   PDF com páginas JPEG A4 a 200 dpi (documento fotografado ou escaneado)
  texto          PDF de sistema: páginas de texto em streams Flate, fonte Helvetica
  formulario-js  PDF com AcroForm, campo com ação JavaScript e OpenAction JavaScript (em objeto próprio ou em linha)
  cifrado        o "digitalizado" regravado pelo Ghostscript com senha de proprietário (RC4 de 128 bits)
  jpeg / png     foto de celular
  eicar-pdf      PDF com o EICAR como arquivo anexado (stream Flate)
  texto-natural  PDF de sistema com texto em português, que o Flate comprime ~4 vezes (5 e 19 MB)
  eicar-depois-de-120mb  PDF de 2 MB com seis anexos Flate de 20 MB de zeros e, depois deles, o EICAR como anexo próprio
  eicar-anexo-de-1mb / -30mb  PDF de 2 MB com um anexo Flate que começa pelo EICAR e se completa com zeros até 1 ou 30 MB
  zip-cifrado-anexo  PDF sem senha com um ZIP cifrado (7z, ZipCrypto) que contém o EICAR, como anexo

Uso: python3 -I gerar.py <diretório de saída>
"""
import io
import random
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path

from PIL import Image, ImageDraw

MB = 1024 * 1024
EICAR = rb"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"


class Pdf:
    def __init__(self):
        self.objs = []

    def add(self, body: bytes) -> int:
        self.objs.append(body)
        return len(self.objs)

    def stream(self, dict_extra: bytes, data: bytes) -> int:
        return self.add(b"<< /Length %d %s >>\nstream\n" % (len(data), dict_extra) + data + b"\nendstream")

    def bytes(self, root: int, info: int) -> bytes:
        out = io.BytesIO()
        out.write(b"%PDF-1.7\n%\xe2\xe3\xcf\xd3\n")
        offs = []
        for i, body in enumerate(self.objs, 1):
            offs.append(out.tell())
            out.write(b"%d 0 obj\n" % i + body + b"\nendobj\n")
        xref = out.tell()
        out.write(b"xref\n0 %d\n0000000000 65535 f \n" % (len(self.objs) + 1))
        for o in offs:
            out.write(b"%010d 00000 n \n" % o)
        out.write(b"trailer\n<< /Size %d /Root %d 0 R /Info %d 0 R >>\nstartxref\n%d\n%%%%EOF\n"
                  % (len(self.objs) + 1, root, info, xref))
        return out.getvalue()


def pagina_jpeg(rnd: random.Random, ruido: int) -> bytes:
    """Página A4 a 200 dpi com texto e ruído de digitalização."""
    w, h = 1654, 2339
    img = Image.effect_noise((w, h), ruido).convert("RGB")
    fundo = Image.new("RGB", (w, h), (238, 236, 230))
    img = Image.blend(fundo, img, 0.35)
    d = ImageDraw.Draw(img)
    for y in range(120, h - 120, 46):
        d.text((110, y), "".join(rnd.choice("abcdefghij klmnop qrstuvwxyz 0123456789") for _ in range(120)), fill=(20, 20, 20))
    buf = io.BytesIO()
    img.save(buf, "JPEG", quality=90)
    return buf.getvalue()


def montar(paginas_jpeg=(), paginas_texto=(), formulario_js=False, abrir_em_linha=False, anexos=(), titulo="Documento") -> bytes:
    pdf = Pdf()
    font = pdf.add(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
    kids, page_ids = [], []
    pages_id_placeholder = len(pdf.objs) + 1
    pdf.add(b"")  # Pages, preenchido depois
    w, h = 595, 842
    for jpg in paginas_jpeg:
        im = Image.open(io.BytesIO(jpg))
        img = pdf.stream(b"/Type /XObject /Subtype /Image /Width %d /Height %d /ColorSpace /DeviceRGB "
                         b"/BitsPerComponent 8 /Filter /DCTDecode" % im.size, jpg)
        content = pdf.stream(b"", b"q %d 0 0 %d 0 0 cm /Im0 Do Q" % (w, h))
        page_ids.append(pdf.add(b"<< /Type /Page /Parent %d 0 R /MediaBox [0 0 %d %d] /Contents %d 0 R "
                                b"/Resources << /XObject << /Im0 %d 0 R >> >> >>" % (pages_id_placeholder, w, h, content, img)))
    for texto in paginas_texto:
        data = zlib.compress(texto)
        content = pdf.stream(b"/Filter /FlateDecode", data)
        page_ids.append(pdf.add(b"<< /Type /Page /Parent %d 0 R /MediaBox [0 0 %d %d] /Contents %d 0 R "
                                b"/Resources << /Font << /F1 %d 0 R >> >> >>" % (pages_id_placeholder, w, h, content, font)))
    extras = b""
    if formulario_js:
        js_campo = pdf.add(b"<< /S /JavaScript /JS (var v = event.value; if (v && !/^\\\\d{8}$/.test(v)) "
                           b"{ app.alert('Informe o CEP com 8 digitos.'); event.rc = false; }) >>")
        widget = pdf.add(b"<< /Type /Annot /Subtype /Widget /FT /Tx /T (cep) /Rect [100 700 300 720] "
                         b"/P %d 0 R /AA << /V %d 0 R >> /DA (/F1 12 Tf 0 g) >>" % (page_ids[0], js_campo))
        pdf.objs[page_ids[0] - 1] = pdf.objs[page_ids[0] - 1][:-2] + b" /Annots [%d 0 R] >>" % widget
        acao = b"<< /S /JavaScript /JS (this.zoomType = zoomtype.fitW; this.pageNum = 0;) >>"
        extras += b" /AcroForm << /Fields [%d 0 R] /DA (/F1 12 Tf 0 g) /DR << /Font << /F1 %d 0 R >> >> >>" % (widget, font)
        # em linha, como em formulário que imprime ou ajusta o zoom ao abrir; ou em objeto próprio
        abrir = b" /OpenAction " + (acao if abrir_em_linha else b"%d 0 R" % pdf.add(acao))
        extras = abrir + extras
    if anexos:
        nomes = []
        for nome, conteudo in anexos:
            ef = pdf.stream(b"/Type /EmbeddedFile /Filter /FlateDecode", zlib.compress(conteudo))
            spec = pdf.add(b"<< /Type /Filespec /F (%s) /EF << /F %d 0 R >> >>" % (nome, ef))
            nomes.append(b"(%s) %d 0 R" % (nome, spec))
        extras += b" /Names << /EmbeddedFiles << /Names [%s] >> >>" % b" ".join(nomes)
    pdf.objs[pages_id_placeholder - 1] = b"<< /Type /Pages /Count %d /Kids [%s] >>" % (
        len(page_ids), b" ".join(b"%d 0 R" % p for p in page_ids))
    root = pdf.add(b"<< /Type /Catalog /Pages %d 0 R%s >>" % (pages_id_placeholder, extras))
    info = pdf.add(b"<< /Title (%s) /Producer (gerador da medicao) >>" % titulo.encode())
    return pdf.bytes(root, info)


def digitalizado(rnd, alvo_mb):
    paginas, total = [], 0
    while total < alvo_mb * MB * 0.97:
        jpg = pagina_jpeg(rnd, 60)
        paginas.append(jpg)
        total += len(jpg)
    return montar(paginas_jpeg=paginas, titulo="Documento digitalizado")


def texto(rnd, alvo_mb):
    paginas, total = [], 0
    while total < alvo_mb * MB:
        linhas = [b"BT /F1 9 Tf 40 800 Td 11 TL"]
        for _ in range(70):
            frase = "".join(rnd.choice("abcdefghijklmnopqrstuvwxyz0123456789 ") for _ in range(110))
            linhas.append(b"(%s) '" % frase.encode())
        linhas.append(b"ET")
        t = b"\n".join(linhas)
        paginas.append(t)
        total += len(zlib.compress(t)) + 160  # objetos da página e do conteúdo, e a entrada do xref
    return montar(paginas_texto=paginas, titulo="Historico escolar")


PALAVRAS = ("candidato inscrição processo seletivo documento histórico escolar ensino médio nota média "
            "disciplina matemática português história geografia biologia química física aprovado "
            "período letivo carga horária frequência unidade federativa município declaração").split()


def texto_natural(rnd, alvo_mb):
    paginas, total = [], 0
    while total < alvo_mb * MB:
        linhas = [b"BT /F1 9 Tf 40 800 Td 11 TL"]
        for _ in range(70):
            frase = " ".join(rnd.choice(PALAVRAS) for _ in range(14))
            linhas.append(b"(%s) '" % frase.encode("latin-1"))
        linhas.append(b"ET")
        t = b"\n".join(linhas)
        paginas.append(t)
        total += len(zlib.compress(t)) + 160  # objetos da página e do conteúdo, e a entrada do xref
    return montar(paginas_texto=paginas, titulo="Historico escolar")


def foto(rnd, alvo_mb, formato):
    lado = 1200
    while True:
        img = Image.blend(Image.new("RGB", (lado, lado * 3 // 4), (180, 170, 150)),
                          Image.effect_noise((lado, lado * 3 // 4), 80).convert("RGB"), 0.5)
        buf = io.BytesIO()
        img.save(buf, formato, **({"quality": 95} if formato == "JPEG" else {"compress_level": 6}))
        if len(buf.getvalue()) >= alvo_mb * MB * 0.95:
            return buf.getvalue()
        lado = int(lado * max(1.05, (alvo_mb * MB / len(buf.getvalue())) ** 0.5))


def zip_cifrado(conteudo: bytes) -> bytes:
    with tempfile.TemporaryDirectory() as tmp:
        (Path(tmp) / "leia.txt").write_bytes(conteudo)
        subprocess.run(["7z", "a", "-tzip", "-punifesspa", "leia.zip", "leia.txt"], cwd=tmp, check=True,
                       stdout=subprocess.DEVNULL)
        return (Path(tmp) / "leia.zip").read_bytes()


def main():
    saida = Path(sys.argv[1])
    saida.mkdir(parents=True, exist_ok=True)
    rnd = random.Random(20261023)
    tamanhos = [1, 5, 10, 20]
    for mb in tamanhos:
        (saida / f"digitalizado-{mb}mb.pdf").write_bytes(digitalizado(rnd, mb))
        (saida / f"jpeg-{mb}mb.jpg").write_bytes(foto(rnd, mb, "JPEG"))
        (saida / f"png-{mb}mb.png").write_bytes(foto(rnd, mb, "PNG"))
        subprocess.run(["gs", "-q", "-dNOPAUSE", "-dBATCH", "-sDEVICE=pdfwrite", "-dPassThroughJPEGImages=true",
                        "-sOwnerPassword=unifesspa", "-dEncryptionR=3", "-dKeyLength=128", "-dPermissions=-3904",
                        f"-sOutputFile={saida / f'cifrado-{mb}mb.pdf'}", str(saida / f"digitalizado-{mb}mb.pdf")],
                       check=True)
    for mb in (1, 5):
        (saida / f"texto-{mb}mb.pdf").write_bytes(texto(rnd, mb))
    (saida / "formulario-js-1mb.pdf").write_bytes(
        montar(paginas_jpeg=[pagina_jpeg(rnd, 60)], formulario_js=True, titulo="Formulario"))
    (saida / "formulario-js-5mb.pdf").write_bytes(
        montar(paginas_jpeg=[pagina_jpeg(rnd, 60) for _ in range(3)], formulario_js=True, titulo="Formulario"))
    (saida / "formulario-js-em-linha-1mb.pdf").write_bytes(
        montar(paginas_jpeg=[pagina_jpeg(rnd, 60)], formulario_js=True, abrir_em_linha=True, titulo="Formulario"))
    for mb in (5, 19):
        (saida / f"texto-natural-{mb}mb.pdf").write_bytes(texto_natural(rnd, mb))
    zeros = [(b"parte%d.bin" % i, bytes(20 * MB)) for i in range(6)]
    (saida / "eicar-depois-de-120mb.pdf").write_bytes(
        montar(paginas_jpeg=[pagina_jpeg(rnd, 60)], anexos=zeros + [(b"leia.txt", EICAR)], titulo="Anexo"))
    for mb in (1, 30):
        (saida / f"eicar-anexo-de-{mb}mb.pdf").write_bytes(
            montar(paginas_jpeg=[pagina_jpeg(rnd, 60)], anexos=[(b"leia.bin", EICAR + bytes(mb * MB - len(EICAR)))],
                   titulo="Anexo"))
    (saida / "eicar-anexo.pdf").write_bytes(
        montar(paginas_jpeg=[pagina_jpeg(rnd, 60)], anexos=[(b"leia.txt", EICAR)], titulo="Anexo"))
    (saida / "zip-cifrado-anexo.pdf").write_bytes(
        montar(paginas_jpeg=[pagina_jpeg(rnd, 60)], anexos=[(b"leia.zip", zip_cifrado(EICAR))], titulo="Anexo"))
    for f in sorted(saida.iterdir()):
        print(f"{f.name:28} {f.stat().st_size / MB:7.2f} MB")


if __name__ == "__main__":
    main()
