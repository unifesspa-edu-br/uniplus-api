# Fontes locais dos relatórios de cobertura

O build registra o commit, a árvore Git e o caminho do checkout. Os jobs de
coleta conferem esse registro antes de executar os testes e o enviam com os
XMLs originais. Na consolidação, `coverage-sources.py prepare` confere o
checkout novamente e prepara cópias dos XMLs com as raízes locais. Os arquivos
rastreados devem existir e a árvore não pode conter alterações rastreadas.
Cada caminho de produção deve corresponder a um único arquivo rastreado. Fontes
`*.g.cs` sob `obj` são geradas durante o build e podem não existir no checkout
do job de relatório; o ReportGenerator registra a ausência dessas fontes.

Nenhuma coleta é eliminada: mesmo XMLs com conteúdo idêntico são mantidos.
Os formatos existentes continuam disponíveis e o HTML detalhado permite
navegar até o código de produção. A opção `preserveTrailingEmptyLine` mantém
a contagem de linhas finais vazias que o leitor remoto apresentava. O gate usa
a taxa sem arredondamento.

Execute os testes da conferência com:

```bash
python3 tools/test-coverage-sources.py
```
