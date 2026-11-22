#!/usr/bin/env python3
"""Confere a origem das coletas e prepara cópias com fontes locais."""
import argparse
import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()


def checkout():
    root = Path(git('rev-parse', '--show-toplevel')).resolve()
    sha = git('rev-parse', 'HEAD')
    tree = git('rev-parse', 'HEAD^{tree}')
    changes = git('diff', '--name-only', 'HEAD', '--')
    if changes:
        raise ValueError(f'A árvore do checkout contém arquivos rastreados alterados ou ausentes: {changes}')
    return root, sha, tree


def validate(manifest, sha, tree):
    if manifest['commit'] != sha or manifest['tree'] != tree:
        raise ValueError(f"Checkout {sha} (árvore {tree}) não corresponde à coleta "
                         f"{manifest['commit']} (árvore {manifest['tree']})")


def prepare(root, source_root, tracked, report, target):
    document = ET.parse(report)
    sources = document.findall('./sources/source')
    local_sources = []
    for source in sources:
        original = Path(source.text or '')
        try:
            relative = original.relative_to(source_root)
        except ValueError as error:
            raise ValueError(f'Fonte fora do checkout de compilação em {report}: {original}') from error
        local = (root / relative).resolve()
        if not local.is_relative_to(root):
            raise ValueError(f'Caminho de fonte inválido em {report}: {original}')
        local_sources.append(local)
        source.text = str(local)
    for item in document.findall('.//class'):
        filename = item.get('filename', '')
        if not filename or Path(filename).is_absolute():
            raise ValueError(f'Caminho de arquivo inválido em {report}: {filename}')
        candidates = {(source / filename).resolve() for source in local_sources}
        if any(not path.is_relative_to(root) for path in candidates):
            raise ValueError(f'Caminho fora do checkout em {report}: {filename}')
        matches = [path for path in candidates if str(path.relative_to(root)) in tracked]
        if len(matches) != 1 or not matches[0].is_file():
            # Geradores podem referenciar fontes de obj que não existem no Git.
            generated = len(candidates) == 1 and 'obj' in Path(filename).parts and filename.endswith('.g.cs')
            if not generated:
                raise ValueError(f'Arquivo de produção ausente ou ambíguo no checkout: {filename} ({report})')
    target.parent.mkdir(parents=True, exist_ok=True)
    document.write(target, encoding='utf-8', xml_declaration=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['record', 'verify', 'prepare'])
    parser.add_argument('manifest', type=Path)
    parser.add_argument('--reports', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    root, sha, tree = checkout()
    if args.command == 'record':
        args.manifest.write_text(json.dumps({'commit': sha, 'tree': tree, 'root': str(root)}) + '\n')
        return
    manifest = json.loads(args.manifest.read_text())
    validate(manifest, sha, tree)
    if args.command == 'prepare':
        if not args.reports or not args.output:
            parser.error('prepare exige --reports e --output')
        reports = sorted(args.reports.rglob('coverage.cobertura.xml'))
        if not reports:
            raise ValueError(f'Nenhuma coleta encontrada em {args.reports}')
        tracked = set(git('ls-files', "-z").split("\0"))
        for report in reports:
            prepare(root, Path(manifest['root']), tracked, report, args.output / report.relative_to(args.reports))
        print(f'{len(reports)} coletas conferidas em {args.reports}')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, ET.ParseError, subprocess.CalledProcessError) as error:
        sys.exit(f'Erro ao conferir fontes da cobertura: {error}')
