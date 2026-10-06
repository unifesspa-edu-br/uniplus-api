# Corpus de casos do formulário

Casos de avaliação do formulário que a API e o interpretador do front rodam igual (ADR-0139). Se um caso diverge entre os dois, o front está interpretando uma regra de forma diferente do servidor.

Cada arquivo é o corpo do `POST /api/configuracao/admin/avaliacoes-de-formulario` — `regras`, `respostas`, `grupos`, `etapasConcluidas` e `pressupostos` — acrescido de duas chaves:

- `descricao` — a regra que o caso protege, em prosa;
- `esperado` — parte do resultado da avaliação (`AvaliacaoPortavel`) que precisa aparecer nele.

O `esperado` é comparado como subconjunto:

- cada propriedade de um objeto esperado é conferida com a do resultado;
- cada objeto de uma lista esperada precisa ser encontrado em algum objeto da lista do resultado;
- valor simples e lista de valores simples são comparados por igualdade.

Na API, o teste `CorpusDeAvaliacaoTests` (`tests/Unifesspa.UniPlus.Configuracao.Application.UnitTests`) roda todos os arquivos desta pasta. Um caso novo entra só com o arquivo.
