---
status: "accepted"
date: "2026-11-06"
decision-makers:
  - "Tech Lead"
consulted: []
informed: []
---

# ADR-0155: Analisadores de qualidade na build

## Contexto e enunciado do problema

Os limites de tamanho de método, complexidade e parâmetros não eram medidos. Sem medição, cumpri-los dependia só da revisão. O projeto precisa medir esses limites na própria build e ter regras de comportamento além das do .NET, sem depender de servidor.

## Drivers da decisão

- Medir como ferramenta de mercado mede, e não com contador próprio.
- Cada regra tem um único dono, para não haver dois avisos do mesmo problema.
- A API compila com `TreatWarningsAsErrors`: a adoção não pode parar a build enquanto o código é refatorado.

## Opções consideradas

- SonarAnalyzer.CSharp, Meziantou.Analyzer e Roslynator.Analyzers na build, sem servidor.
- Só Meziantou.Analyzer, que mede tamanho de método pela MA0051 e tem licença MIT.
- Servidor SonarQube.

## Resultado da decisão

**Escolhida:** os três analisadores na build, sem servidor, porque a complexidade cognitiva e a ciclomática só existem no Sonar, e o servidor não acrescenta nada à medição agora.

- **Sonar** é dono das métricas: S138 (linhas por método), S3776 (cognitiva), S1541 (ciclomática) e S107 (parâmetros). Os limites ficam em `SonarLint.xml`. As demais regras ficam desligadas em `.globalconfig` e entram uma a uma, sempre corrigindo o código.
- **Meziantou** é dono das regras de comportamento que não têm dono no .NET. A regra que repete uma do .NET fica desligada. As que têm ocorrências ficam como sugestão até serem corrigidas.
- **Roslynator** é dono do estilo, como sugestão na IDE, e nunca barra a build. A regra dele que repete uma de comportamento fica desligada.

As quatro métricas são avisos (`WarningsNotAsErrors`) até as ocorrências acabarem, conforme a [ADR-0156](0156-limites-de-tamanho-e-complexidade-do-codigo.md), e depois passam a erro. Até lá, esta ADR emenda a [ADR-0117](0117-politica-de-analise-estatica-e-supressao.md): a build sem avisos admite esses quatro. Cada build grava relatório SARIF em `artifacts/analise/`.

A licença SSAL v1.0 do Sonar não é aprovada pela OSI. A adoção é uma exceção deliberada à preferência do projeto por licenças OSI, porque o uso é só de análise do próprio código.

> **Emenda (2026-11-11) — a S138 é sinal auxiliar.** A S138 continua na build como aviso, mas não é a medida do limite de linhas por método, e a consequência "toda build mede linhas por método" passa a ler-se "toda build avisa pela S138, que é parcial": a [ADR-0156](0156-limites-de-tamanho-e-complexidade-do-codigo.md), emendada na mesma data, conta linhas físicas e inclui os testes. A emenda à ADR-0117 feita aqui admite os avisos das quatro métricas na build; não admite suprimi-los, conforme a [ADR-0160](0160-supressao-de-aviso-do-analisador-proibida.md).
>
> **Emenda (2026-11-12) — a MA0051 mede o tamanho de método e a S104, o de arquivo.** Pela [ADR-0161](0161-metricas-medidas-pelos-analisadores.md), o dono do tamanho de método passa a ser a MA0051, que roda também em projeto de teste, e a S138 fica desligada; o limite de arquivo passa à S104, e o gate próprio de tamanho de arquivo sai. O número que vale é o do aviso. As métricas em aviso passam a ser MA0051, S104, S3776, S1541 e S107. O que esta ADR e a emenda anterior dizem sobre a S138 deixa de valer para o tamanho de método, que passa à MA0051, inclusive em projeto de teste.

## Consequências

### Positivas

- Toda build mede linhas por método (até 15), complexidade cognitiva, complexidade ciclomática e parâmetros.

### Negativas

- Atualizar o Sonar pode trazer regra nova ligada: `.globalconfig` precisa acompanhar a versão.
- A S138 aceita um único limite por compilação, então o limite de 15 linhas por método vale para toda a API. Mapeamentos do EF ficam fora dela, por serem listas declarativas; o longo se divide por assunto, cobrado pela revisão e pelo limite de linhas por arquivo.
- A S138 e a S1541 não medem projetos de teste; a S3776 e a S107 medem.

### Neutras

- Migrations do EF ficam fora das métricas, porque são geradas pela ferramenta.
- O limite de linhas por arquivo continua no gate próprio, `tools/tamanho-de-arquivo/`.

## Confirmação

`dotnet build UniPlus.slnx -c Release` termina sem erro, e os únicos avisos são S138, S3776, S1541 e S107.

## Mais informações

- [ADR-0117](0117-politica-de-analise-estatica-e-supressao.md): política de análise e supressão, emendada aqui quanto às métricas em modo aviso.
