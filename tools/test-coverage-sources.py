#!/usr/bin/env python3
"""Testa a proveniência e a preparação das coletas de cobertura."""
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).with_name('coverage-sources.py')


class CoverageSourcesTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.git('init', '-q')
        self.git('config', 'user.email', 'test@example.invalid')
        self.git('config', 'user.name', 'Teste')
        (self.root / 'src').mkdir()
        (self.root / 'src' / 'A.cs').write_text('class A {}\n')
        self.git('add', 'src/A.cs')
        self.git('commit', '-qm', 'fixture')
        self.manifest = self.root / 'source.json'
        self.run_script('record', self.manifest, success=True)
        self.reports = self.root / 'reports'
        self.reports.mkdir()
        self.report = self.reports / 'coverage.cobertura.xml'
        self.report.write_text(f'<coverage><sources><source>{self.root}/src</source></sources>'
                               '<packages><package><classes><class filename="A.cs" />'
                               '</classes></package></packages></coverage>')

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, text=True).strip()

    def run_script(self, *args, success):
        result = subprocess.run(['python3', str(SCRIPT), *map(str, args)], cwd=self.root,
                                text=True, capture_output=True)
        self.assertEqual(result.returncode == 0, success, result.stderr)
        return result

    def test_prepara_sem_modificar_original(self):
        original = self.report.read_bytes()
        self.run_script('prepare', self.manifest, '--reports', self.reports,
                        '--output', self.root / 'prepared', success=True)
        self.assertEqual(self.report.read_bytes(), original)
        self.assertTrue((self.root / 'prepared' / self.report.name).is_file())

    def test_recusa_commit_diferente(self):
        data = json.loads(self.manifest.read_text())
        data['commit'] = 'outro-commit'
        self.manifest.write_text(json.dumps(data))
        result = self.run_script('verify', self.manifest, success=False)
        self.assertIn('outro-commit', result.stderr)

    def test_recusa_arquivo_ausente(self):
        (self.root / 'src' / 'A.cs').unlink()
        result = self.run_script('prepare', self.manifest, '--reports', self.reports,
                                 '--output', self.root / 'prepared', success=False)
        self.assertIn('src/A.cs', result.stderr)

    def test_recusa_referencia_a_arquivo_inexistente(self):
        self.report.write_text(self.report.read_text().replace('A.cs', 'Ausente.cs'))
        result = self.run_script('prepare', self.manifest, '--reports', self.reports,
                                 '--output', self.root / 'prepared', success=False)
        self.assertIn('Ausente.cs', result.stderr)

    def test_recusa_arvore_alterada(self):
        (self.root / 'src' / 'A.cs').write_text('class Alterada {}\n')
        result = self.run_script('verify', self.manifest, success=False)
        self.assertIn('src/A.cs', result.stderr)

    def test_preserva_coletas_independentes_identicas(self):
        for name in ('primeira', 'segunda'):
            folder = self.reports / name
            folder.mkdir()
            (folder / self.report.name).write_bytes(self.report.read_bytes())
        self.run_script('prepare', self.manifest, '--reports', self.reports,
                        '--output', self.root / 'prepared', success=True)
        self.assertEqual(len(list((self.root / 'prepared').rglob(self.report.name))), 3)


if __name__ == '__main__':
    unittest.main()
