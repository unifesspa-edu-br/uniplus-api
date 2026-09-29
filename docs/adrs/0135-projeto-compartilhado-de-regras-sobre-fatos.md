---
status: "accepted"
date: "2026-09-29"
decision-makers:
  - "Tech Lead"
consulted: []
informed: []
---

# ADR-0135: As regras sobre fatos do candidato vivem num projeto compartilhado fora do Kernel

## Contexto e enunciado do problema

Hoje só o módulo Seleção avalia regras sobre fatos do candidato. O predicado em forma normal disjuntiva, com operadores, cláusulas, validador e códigos de erro, sustenta a pré-condição de campo, o gatilho de exigência documental, a regra de derivação e o desempate por predicado. A avaliação ternária e o motor de derivação também moram em `Selecao.Domain`.

O módulo Configuração passa a precisar da mesma lógica. O modelo de formulário por tipo de processo, cadastro novo da Configuração, valida exibição e obrigatoriedade condicionadas a fatos e oferece pré-visualização com respostas simuladas. Essa validação não pode esperar a Seleção: a recusa precisa sair no momento em que o administrador grava o modelo.

A governança do catálogo de fatos não muda por esta ADR. Hoje ele é semeado por código, sem cadastro administrativo (ADR-0111). Torná-lo administrável, inclusive com fatos derivados e suas regras, é decisão de ADR própria, que supera essa parte da ADR-0111. Esta decisão vale com qualquer das duas governanças: mesmo com o catálogo semeado, o modelo de formulário precisa validar predicados sobre fatos na Configuração. Se o catálogo se tornar administrável, as regras de derivação cadastradas usam o mesmo projeto.

Três restrições delimitam onde esse código pode morar:

- **A ADR-0001** reserva o SharedKernel a objetos de valor, erros de domínio e contratos de evento base.
- **A ADR-0055** reforça que o Kernel é de primitivos sem ciclo de vida nem comportamento de domínio.
- **A ADR-0061** proíbe que um módulo leia os dados de outro por referência direta. Ela não trata de código compartilhado, mas um projeto de módulo referenciar outro módulo é a violação que ela e a ADR-0001 querem evitar.

## Drivers da decisão

- Uma única implementação da semântica do predicado e da derivação. Duas cópias divergem, e a divergência só aparece quando a Configuração aceita algo que a Seleção recusa na publicação.
- A Configuração não pode depender, em tempo de compilação nem de execução, do módulo Seleção.
- O Kernel continua restrito a primitivos.
- A mudança de lugar não pode alterar o formato do envelope congelado nem o modelo de persistência.

## Opções consideradas

- A. Mover as regras para o Kernel.
- B. Copiar as regras para `Configuracao.Domain`.
- C. `Configuracao.Domain` referenciar `Selecao.Domain`.
- D. A Configuração pedir a avaliação à Seleção em tempo de execução, por reader ou HTTP.
- E. Projeto compartilhado próprio, `Unifesspa.UniPlus.Regras`, em `src/shared/`, dependente só do Kernel.

## Resultado da decisão

**Escolhida:** "E. Projeto compartilhado próprio", porque dá uma só implementação às duas pontas sem inflar o Kernel e sem criar dependência entre módulos.

`Unifesspa.UniPlus.Regras` vive em `src/shared/`, no mesmo molde de `Unifesspa.UniPlus.Governance.Contracts`: referencia apenas o Kernel, para `Result`, e nunca referencia projeto de módulo nem outro projeto compartilhado. Os domínios da Configuração e da Seleção o referenciam, e as demais camadas desses módulos o alcançam por meio deles. `Infrastructure.Core` o referencia para registrar o mapeamento dos erros.

**Movem para o projeto**, sem mudança de comportamento, 20 tipos em 18 arquivos, o fechamento das dependências entre eles:

- o predicado e suas partes (`PredicadoDnf`, `ClausulaDnf`, `CondicaoDnf`, `Operador`, `EstadoAtomo`), com o mapeamento entre o operador e o código que viaja no contrato (`OperadorCodigo`). A Configuração recebe condições com o operador em texto e precisa da mesma conversão;
- a validação do predicado (`PredicadoDnfValidador`, `PredicadoDnfErrorCodes`) e a descrição do fato de que ela depende (`DescritorFatoCandidato`, `TipoDominioFato`, com o mapeamento do tipo de domínio para o código, `TipoDominioFatoCodigo`);
- a derivação (`MotorDerivacao`, `RegraDerivacao`, `RegrasDerivacaoFato`, `ResultadoDerivacao`), com os códigos de erro declarados nos mesmos arquivos (`RegraDerivacaoErrorCodes`, `RegrasDerivacaoFatoErrorCodes`);
- o estado resolvido do fato (`FatoResolvido`, `EstadoFato`, `Ternario`).

**É extraída para o projeto** a detecção de ciclo entre dependências de fatos, como função sobre códigos de fato. Hoje ela é um método privado do agregado do processo seletivo, sobre `FatoColetado`. **É criada no projeto** a ordenação topológica de que o avaliador de formulário precisa. A única ordenação que existe hoje é a do grafo conjunto do congelamento (`GrafoDependenciaConjunta`), que continua na Seleção, porque inclui exigências e arestas que só existem ali.

**Nasce no projeto** o avaliador de formulário. Ele trabalha sobre a descrição do item (fato, ordem, exibição, obrigatoriedade, restrição), e não sobre entidade de módulo.

**Fica em cada módulo o que depende de entidade do módulo:**

- `ResolvedorEstadoFatos`, que hoje percorre a entidade `FatoColetado`, passa a delegar ao avaliador.
- A avaliação da árvore de exigências documentais (`ResolvedorArvoreSatisfacao`) continua na Seleção, dona das exigências.

O projeto não tem entidade, repositório, persistência nem acesso a I/O. É lógica pura sobre valores.

**Os códigos de erro emitidos pelos tipos que mudam de lugar são mapeados para HTTP uma única vez**, num registro compartilhado em `Infrastructure.Core`, no mesmo molde do registro do Kernel, com código de wire neutro de módulo. Hoje são seis grupos: `clausula_dnf`, `condicao_dnf`, `descritor_fato_candidato`, `predicado_dnf`, `regra_derivacao` e `regras_derivacao_fato`. Nenhum módulo registra esses códigos. O registro de mapeamentos é global e o último registro vence, então um mapeamento por módulo faria as duas pontas se sobrescreverem sem aviso. O código de wire desses grupos perde o prefixo `uniplus.selecao.` (por exemplo, `uniplus.predicado_dnf.*`); os erros da regra de derivação configurada no processo (`uniplus.selecao.regra_derivacao_configurada.*`) continuam na Seleção. Como não há produção, a troca é feita de uma vez, no mesmo PR da mudança de lugar, junto com tudo o que confere esses códigos: a coleção Postman da Seleção e o README dela, e os testes que afirmam o código de wire.

## Consequências

### Positivas

- A Configuração recusa no cadastro exatamente o que a Seleção recusaria na publicação, porque é o mesmo código.
- O Kernel continua restrito a primitivos.
- A pré-visualização de formulário não precisa de chamada entre módulos.

### Negativas

- A mudança de lugar muda o namespace de 20 tipos e toca os projetos da Seleção que os usam (Application, Infrastructure e API) e os projetos de teste da Seleção (unitários, de integração e de arquitetura). É uma mudança mecânica, mas conflita com branches abertas que editam os mesmos arquivos. Ela entra num PR isolado e curto.
- Evoluir a semântica do predicado passa a afetar dois módulos de uma vez, o que exige teste nas duas pontas.
- O código de wire dos erros do predicado e da derivação muda de prefixo.

### Neutras

- O formato do envelope não muda: a serialização canônica não usa o nome nem o namespace dos tipos.
- O modelo do EF não muda: nenhum snapshot de migration cita esses tipos pelo nome qualificado.

## Confirmação

- Teste de arquitetura: `Unifesspa.UniPlus.Regras` depende só do Kernel e do .NET, e não depende de módulo nem de outro projeto compartilhado. Podem depender dele as camadas dos módulos e o `Infrastructure.Core`; os demais projetos compartilhados (Kernel, `Governance.Contracts`, `Application.Abstractions`, `Authorization`) não dependem dele.
- Teste de arquitetura: o projeto não declara entidade (`EntityBase`) nem depende de EF Core, Wolverine ou ASP.NET.
- Teste de mapeamento de erros equivalente ao do Kernel: todo código de erro declarado em `Unifesspa.UniPlus.Regras` tem mapeamento no registro compartilhado. O teste por módulo ignora `src/shared/`, e sem esse teste um código novo sem mapeamento viraria erro genérico.
- A suíte da Seleção passa sem mudança de expectativa no PR que faz a mudança de lugar, com uma única exceção: as asserções de código de wire dos seis grupos de erro, que trocam de prefixo.

## Prós e contras das opções

### A. Mover as regras para o Kernel

- Bom, porque todo projeto já referencia o Kernel.
- Ruim, porque contraria a ADR-0001 e a ADR-0055: o Kernel passaria a carregar comportamento de domínio, e a disciplina "só primitivos" se perde aos poucos.

### B. Copiar as regras para `Configuracao.Domain`

- Bom, porque não mexe na Seleção.
- Ruim, porque cria duas implementações da mesma semântica, que divergem com o tempo. Uma regra aceita no cadastro e recusada na publicação é exatamente o defeito que a decisão quer evitar.

### C. `Configuracao.Domain` referenciar `Selecao.Domain`

- Bom, porque não move nada.
- Ruim, porque acopla dois módulos em tempo de compilação, o que a ADR-0001 veda. Também arrasta para a Configuração as entidades e o agregado do processo seletivo.

### D. Pedir a avaliação à Seleção em tempo de execução

- Bom, porque mantém o código num só lugar.
- Ruim, porque o cadastro da Configuração passa a depender da disponibilidade da Seleção para validar uma gravação, e a validação deixa de ser do domínio (ADR-0125). A Seleção também não tem o que avaliar: o modelo de formulário e o catálogo são dados da Configuração.

### E. Projeto compartilhado próprio

- Bom, porque uma implementação serve às duas pontas sem acoplar módulos e sem inflar o Kernel, e a fronteira é verificável por teste de arquitetura.
- Ruim, porque exige uma mudança de lugar que toca muitos arquivos de uma vez.

## Mais informações

- ADR-0001: estrutura modular e o papel do SharedKernel.
- ADR-0055: Kernel restrito a primitivos; precedente de projeto em `src/shared/` fora do Kernel.
- ADR-0061: referência entre módulos por cópia de valor.
- ADR-0111 e ADR-0116: vocabulário de fatos e mecanismos de produção de valor.
- ADR-0125: o domínio é a fonte única de validação.
