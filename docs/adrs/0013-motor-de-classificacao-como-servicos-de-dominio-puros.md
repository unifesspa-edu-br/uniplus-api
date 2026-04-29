---
status: "accepted"
date: "2026-04-28"
decision-makers:
  - "Tech Lead (CTIC)"
  - "P.O. CEPS"
---

# ADR-0013: Motor de classificação como serviços de domínio puros

## Contexto e enunciado do problema

O motor de classificação é o componente mais sensível do `uniplus-api` do ponto de vista legal e institucional. Ele calcula notas finais, ordena candidatos por modalidade, aplica desempate e executa o remanejamento de vagas de cotas. Erros têm consequências diretas — alocação errada de candidatos, violação da Lei de Cotas, judicialização e dano reputacional.

O sistema precisa cobrir cinco tipos atuais de processo seletivo (PSIQ, PSE Educação do Campo, PS Convênios, PSVR, SiSU), cada um com fórmula, eliminações, desempate, bônus e regime de cotas próprios. Adicionalmente, RN08 (congelamento de parâmetros por edital) exige que o resultado de qualquer execução seja reproduzível anos depois com o snapshot da configuração vinculada ao edital.

## Drivers da decisão

- Determinismo: mesma entrada produz sempre a mesma saída.
- Auditabilidade: cada passo do ranking e remanejamento rastreável.
- Testabilidade sem infraestrutura: motor roda como função pura.
- Operação destrava engenharia: admin do CEPS cadastra novos tipos de edital sem deploy.
- Reaproveitabilidade: mesmo motor serve auxílio estudantil, mestrado, mobilidade etc.

## Opções consideradas

- Serviços de domínio puros com configuração declarativa
- Orquestrador na camada de aplicação com queries intermediárias
- Strategy de classes por tipo de edital
- Biblioteca externa de programação linear (OR-Tools, LpSolveDotNet)
- Incorporar dentro do módulo Seleção como namespace interno

## Resultado da decisão

**Escolhida:** motor de classificação como serviços de domínio puros e determinísticos no módulo `Classificacao`, com zero dependências de infraestrutura.

Dois serviços de domínio principais:

- **`MotorClassificacao`** — recebe `ConfiguracaoCalculo` declarativa congelada por edital (RN08) e a interpreta. Aplica fórmula de agregação configurada, regra de precisão, bônus, regras de eliminação, ordem de desempate e — quando o edital exige — o passo de concorrência dupla. **Não conhece** "PSIQ", "SiSU" ou qualquer tipo de processo — conhece apenas o catálogo de primitivas.
- **`ServicoRemanejamento`** — executa a cascata de remanejamento conforme `CascataRemanejamento` configurada. Produz audit log passo-a-passo (qual vaga, qual modalidade origem, qual destino, qual candidato beneficiado, qual regra aplicada).

Configuração declarativa (snapshot RN08 por edital):

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

A orquestração (hidratar dados do banco, persistir resultado) fica na camada de aplicação, que invoca o motor via handler Wolverine (ADR-0003) e grava o resultado via EF Core (ADR-0007).

## Consequências

### Positivas

- Funções puras são trivialmente testáveis — sem mock, sem banco, sem fixture.
- Determinismo é demonstrável e enforçável por property-based tests.
- Audit log é saída de primeira classe — rastreabilidade por construção.
- Conformidade legal verificável por stakeholders não-técnicos via golden files.
- Reutilizável por qualquer processo de seleção/alocação institucional sem refatoração.
- Operação destrava engenharia — admin compõe primitivas via UI, sem deploy.

### Negativas

- Camada de aplicação precisa hidratar todo o dataset de entrada antes de chamar o motor.
- Sem lazy loading, paginação ou streaming dentro do motor — conjunto inteiro processado em memória.
- Disciplina exigida: tentativa futura de "otimizar" com acesso direto ao banco quebra a pureza e invalida a estratégia de testes.

### Riscos

- **Pressão de memória em processos com muitos cursos** (ex.: SiSU). Mitigação: estratégia de particionamento e execução documentada em ADR própria sobre orquestração da classificação.
- **Alteração nas regras da Lei 14.723/2023 ou regulamentação infralegal.** Mitigação em duas camadas: (1) parâmetros novos viram reconfiguração no admin sem deploy; RN08 garante imutabilidade de editais antigos; (2) fórmulas estruturalmente novas viram nova primitiva no catálogo com versionamento explícito.
- **Não-determinismo acidental** (ex.: `HashSet<T>` em vez de `SortedSet<T>`). Mitigação: prova de determinismo de 1000 iterações no CI.
- **Retenção do audit log.** Documentos de processo seletivo têm retenção mínima de 5 anos. Mitigação: tratar audit log como dado de processo seletivo na política de retenção; soft delete obrigatório.

## Confirmação

Quatro camadas complementares de teste:

1. **Property-based tests (FsCheck)** — verificam invariantes matemáticas e legais para qualquer entrada válida (total alocado ≤ vagas; ordem da cascata; determinismo de desempate; concorrência dupla; nenhum candidato em duas modalidades simultâneas).
2. **Golden file tests** — re-executam configurações reais de editais históricos e comparam saída byte-a-byte.
3. **Unit tests** — cobrem casos de borda (empates múltiplos, modalidade sem candidatos elegíveis, arredondamento vs. truncamento por edital, vagas fracionárias com regra de teto).
4. **Prova de determinismo** — executa a classificação 1000 vezes com ordem de entrada embaralhada, afirmando saída idêntica em todas as iterações.

Meta de cobertura: ≥95% em `MotorClassificacao` e `ServicoRemanejamento`.

## Prós e contras das opções

### Orquestrador na aplicação

- Bom, porque segue o padrão handler CQRS típico.
- Ruim, porque lógica de negócio vaza para a aplicação e fica mais difícil testar ranking + remanejamento como unidade pura.

### Strategy de classes por tipo

- Bom, porque cada tipo fica em uma classe pequena.
- Ruim, porque novo tipo de edital vira novo deploy — viola a premissa operacional.

### Biblioteca de programação linear

- Bom, porque traz algoritmo pronto.
- Ruim, porque o motor não é um problema de otimização — é aplicação determinística de regras; OR-Tools sobrepesa.

### Namespace interno do Seleção

- Bom, porque é mais simples no curto prazo.
- Ruim, porque acopla classificação aos processos do CEPS; módulos futuros teriam que duplicar.

## Mais informações

- ADR-0001 fundamenta a separação `Classificacao` como módulo próprio.
- ADR-0002 estabelece que regra de negócio fica no domínio.
- ADR-0003 define Wolverine como invocador do motor.
- ADR-0007 define PostgreSQL como persistência do resultado.
- Legislação relevante: Lei 12.711/2012 (atualizada pela Lei 14.723/2023), Lei 13.409/2016 (PcD), Lei 10.741/2003 (Estatuto do Idoso), Portaria MEC 704/2025, Portaria MEC 18/2012, IN MGI 23/2023.
- **Origem:** revisão da ADR interna Uni+ ADR-028 (não publicada). Detalhes da matriz de cotas, fórmulas por processo seletivo e mapeamento da legislação infralegal permanecem em base de conhecimento institucional.
