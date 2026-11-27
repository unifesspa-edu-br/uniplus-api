---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
  - "P.O. CEPS"
---

# ADR-0013: Classificação dos candidatos como serviços de domínio puros

## Contexto e enunciado do problema

A classificação dos candidatos é a atividade mais sensível do `uniplus-api` do ponto de vista legal e institucional. Ela calcula notas finais, ordena candidatos por modalidade, aplica desempate e executa o remanejamento de vagas de cotas. Erros têm consequências diretas — alocação errada de candidatos, violação da Lei de Cotas, judicialização e dano reputacional.

O sistema precisa cobrir cinco tipos atuais de processo seletivo (PSIQ, PSE Educação do Campo, PS Convênios, PSVR, SiSU), cada um com fórmula, eliminações, desempate, Fator de Atenuação das Desigualdades de Acesso (FADA) e regime de cotas próprios. Adicionalmente, UNI-REQ-0019 (congelamento de parâmetros por edital) exige que o resultado de qualquer execução seja reproduzível anos depois com o snapshot da configuração vinculada ao edital.

## Drivers da decisão

- Determinismo: mesma entrada produz sempre a mesma saída.
- Auditabilidade: cada passo do ranking e remanejamento rastreável.
- Testabilidade sem infraestrutura: a classificação funciona como função pura.
- Operação destrava engenharia: admin do CEPS cadastra novos tipos de edital sem deploy.
- Reaproveitabilidade: o mesmo serviço de classificação serve auxílio estudantil, mestrado, mobilidade etc.

## Opções consideradas

- Serviços de domínio puros com configuração declarativa
- Orquestrador na camada de aplicação com queries intermediárias
- Strategy de classes por tipo de edital
- Biblioteca externa de programação linear (OR-Tools, LpSolveDotNet)
- Incorporar dentro do módulo Seleção como namespace interno

## Resultado da decisão

**Escolhida:** classificação dos candidatos como serviços de domínio puros e determinísticos no módulo `Classificacao`, com zero dependências de infraestrutura.

Dois serviços de domínio principais:

- **Serviço de classificação** — recebe `ConfiguracaoCalculo` declarativa congelada por edital (UNI-REQ-0019) e a interpreta. Aplica fórmula de agregação configurada, regra de precisão, FADA, regras de eliminação, ordem de desempate e — quando o edital exige — o passo de concorrência dupla. **Não conhece** "PSIQ", "SiSU" ou qualquer tipo de processo — conhece apenas o catálogo de primitivas.
- **Serviço de remanejamento** — executa a cascata de remanejamento conforme `CascataRemanejamento` configurada. Produz audit log passo-a-passo (qual vaga, qual modalidade origem, qual destino, qual candidato beneficiado, qual regra aplicada).

Configuração declarativa (snapshot UNI-REQ-0019 por edital):

```text
ConfiguracaoCalculo
├── Etapas:               [{ codigo, peso, escala }]
├── FormulaAgregacao:     MediaPonderada
│                       | SomaPonderadaComFator(fator)
│                       | MediaSimples
│                       | MediaPonderadaEnem(pesosArea, grupo)
├── Precisao:             Truncar2Casas | ArredondarParaCima2Casas
├── Bonus:                opt { tipo, valor, modalidadesAplicaveis }
├── RegrasEliminacao:     [NotaMinimaEtapa | RedacaoEnemMinima | Falta | …]
├── OrdemDesempate:       [Idoso60 | MaiorNotaEtapa(x) | MaiorIdade | …]
├── ConcorrenciaDupla:    bool
└── CascataRemanejamento: refTabela
```

**Linha de corte clara:**

- **Cadastrar/editar/clonar tipo de edital** (compor primitivas) → operação de admin, sem código.
- **Adicionar primitiva matemática nova** (ex.: fórmula que nenhum dos 5 processos atuais cobre) → mudança de código com versionamento explícito (ex.: `MediaPonderadaEnem v2`).

A orquestração (hidratar dados do banco, persistir resultado) fica na camada de aplicação, que invoca o serviço de classificação via handler Wolverine (ADR-0003) e grava o resultado via EF Core (ADR-0007).

## Consequências

### Positivas

- Funções puras são trivialmente testáveis — sem mock, sem banco, sem fixture.
- Determinismo é demonstrável e enforçável por property-based tests.
- Audit log é saída de primeira classe — rastreabilidade por construção.
- Conformidade legal verificável por stakeholders não-técnicos via golden files.
- Reutilizável por qualquer processo de seleção/alocação institucional sem refatoração.
- Operação destrava engenharia — admin compõe primitivas via UI, sem deploy.

### Negativas

- Camada de aplicação precisa hidratar todo o dataset de entrada antes de chamar o serviço de classificação.
- Sem lazy loading, paginação ou streaming dentro do serviço de classificação — conjunto inteiro processado em memória.
- Disciplina exigida: tentativa futura de "otimizar" com acesso direto ao banco quebra a pureza e invalida a estratégia de testes.

### Riscos

- **Pressão de memória em processos com muitos cursos** (ex.: SiSU). Mitigação: estratégia de particionamento e execução documentada em ADR própria sobre orquestração da classificação.
- **Alteração nas regras da Lei 14.723/2023 ou regulamentação infralegal.** Mitigação em duas camadas: (1) parâmetros novos viram reconfiguração no admin sem deploy; UNI-REQ-0019 garante imutabilidade de editais antigos; (2) fórmulas estruturalmente novas viram nova primitiva no catálogo com versionamento explícito.
- **Não-determinismo acidental** (ex.: `HashSet<T>` em vez de `SortedSet<T>`). Mitigação: prova de determinismo de 1000 iterações no CI.
- **Retenção do audit log.** Documentos de processo seletivo têm retenção mínima de 5 anos. Mitigação: tratar audit log como dado de processo seletivo na política de retenção; soft delete obrigatório.

## Confirmação

Quatro camadas complementares de teste:

1. **Property-based tests (FsCheck)** — verificam invariantes matemáticas e legais para qualquer entrada válida (total alocado ≤ vagas; ordem da cascata; determinismo de desempate; concorrência dupla; nenhum candidato em duas modalidades simultâneas).
2. **Golden file tests** — re-executam configurações reais de editais históricos e comparam saída byte-a-byte.
3. **Unit tests** — cobrem casos de borda (empates múltiplos, modalidade sem candidatos elegíveis, arredondamento vs. truncamento por edital, vagas fracionárias com regra de teto).
4. **Prova de determinismo** — executa a classificação 1000 vezes com ordem de entrada embaralhada, afirmando saída idêntica em todas as iterações.

Meta de cobertura: ≥95% no serviço de classificação e no serviço de remanejamento.

## Prós e contras das opções

### Orquestrador na aplicação

- Bom, porque segue o padrão handler CQRS típico.
- Ruim, porque lógica de negócio vaza para a aplicação e fica mais difícil testar ranking + remanejamento como unidade pura.

### Strategy de classes por tipo

- Bom, porque cada tipo fica em uma classe pequena.
- Ruim, porque novo tipo de edital vira novo deploy — viola a premissa operacional.

### Biblioteca de programação linear

- Bom, porque traz algoritmo pronto.
- Ruim, porque a classificação dos candidatos não é um problema de otimização — é aplicação determinística de regras; OR-Tools sobrepesa.

### Namespace interno do Seleção

- Bom, porque é mais simples no curto prazo.
- Ruim, porque acopla classificação aos processos do CEPS; módulos futuros teriam que duplicar.

## Mais informações

- ADR-0001 fundamenta a separação `Classificacao` como módulo próprio.
- ADR-0002 estabelece que regra de negócio fica no domínio.
- ADR-0003 define Wolverine como invocador do serviço de classificação.
- ADR-0007 define PostgreSQL como persistência do resultado.
- Legislação relevante: Lei 12.711/2012 (atualizada pela Lei 14.723/2023), Lei 13.409/2016 (PcD), Lei 10.741/2003 (Estatuto do Idoso), Portaria MEC 704/2025, Portaria MEC 18/2012, IN MGI 23/2023.
- **Origem:** revisão da ADR interna Uni+ ADR-028 (não publicada). Detalhes da matriz de cotas, fórmulas por processo seletivo e mapeamento da legislação infralegal permanecem em base de conhecimento institucional.

## Emenda 1 (2026-11-25) — nomes e desenho vigentes

A decisão permanece: a classificação é feita por serviços de domínio puros e determinísticos, sem acesso a banco ou a outro sistema. A camada de aplicação busca os dados antes e grava o resultado depois, por handler Wolverine (ADR-0003) e EF Core (ADR-0007). Fórmula, precisão, eliminação, desempate e remanejamento vêm da configuração. A classificação decide pelo retrato da publicação (UNI-REQ-0019 e [ADR-0109](0109-envelope-canonico-v2-do-congelamento.md)); a linha de corte entre cadastro e código segue a [ADR-0112](0112-fronteira-append-only-do-catalogo-de-regras.md). Permanecem as quatro camadas de teste descritas na confirmação.

Ficam superados os seguintes pontos do desenho original:

- O módulo `Classificacao` próprio: a classificação fica na Seleção ([ADR-0097](0097-topologia-de-deploy-em-tres-apis-monolito-modular.md)).
- A estrutura `ConfiguracaoCalculo`: agora há `ConfiguracaoClassificacao`, que aponta para regras do rol por código, versão e hash, além de `CriterioDesempate`, `RegraEliminacao`, `ConfiguracaoCascataRemanejamento` e da regra de consolidação da etapa ([ADR-0112](0112-fronteira-append-only-do-catalogo-de-regras.md) e [ADR-0150](0150-parametros-de-avaliacao-da-etapa.md)).
- O catálogo de primitivas composto pelo administrador na tela: há um rol de regras versionado ([ADR-0112](0112-fronteira-append-only-do-catalogo-de-regras.md)).
- A premissa de regras por tipo de edital e os cinco tipos de processo: não há regra por tipo de processo (UNI-REQ-0015).
- A concorrência dupla gravada: ela é derivada das modalidades.
- O acréscimo `Bonus`: passou a ser o Fator de Atenuação das Desigualdades de Acesso ([ADR-0142](0142-elegibilidade-do-fator-de-atenuacao.md)).
- As opções de precisão `Truncar2Casas` e `ArredondarParaCima2Casas`: agora se definem a regra e as casas de arredondamento.
- O soft delete obrigatório no registro de auditoria: soft delete é opcional, e entidades de evidência forense usam `IForensicEntity` ([ADR-0063](0063-entidades-forensics-isentas-de-soft-delete.md)).
- A referência RN08: o congelamento da configuração é tratado pela UNI-REQ-0019 e pela [ADR-0109](0109-envelope-canonico-v2-do-congelamento.md).

Os nomes figurados do texto original foram trocados por nomes que dizem o que a parte faz, por decisão do líder técnico em 25/11/2026, sem mudar a decisão.
