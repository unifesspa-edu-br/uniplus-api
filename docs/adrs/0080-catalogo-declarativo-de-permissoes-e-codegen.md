---
status: "proposed"
date: "2026-06-02"
decision-makers:
  - "Tech Lead (CTIC)"
consulted: []
informed:
  - "Equipe Uni+"
---

# ADR-0080: Catálogo declarativo de permissões como fonte única e geração de artefatos

## Contexto e enunciado do problema

O modelo de autorização da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md) decide o acesso a partir de **permissões** (o que pode ser feito) combinadas com atributos de contexto. Essas permissões são referenciadas em vários pontos do sistema, em repositórios diferentes:

- no backend .NET, para exigir a permissão na decisão de acesso;
- no frontend, para habilitar ou esconder ações na interface;
- no provisionamento de grupos do provedor de identidade, para empacotar permissões em perfis;
- no inventário de proteção de dados (LGPD), para saber qual a sensibilidade e a base legal de cada operação;
- na documentação para desenvolvedores.

Se cada um desses pontos declara as permissões por conta própria, eles divergem: uma permissão renomeada no backend não se reflete no frontend nem no provisionamento; uma permissão sensível pode ficar sem base legal registrada; o conjunto efetivo de permissões de um perfil deixa de ser conhecido. O problema é manter **uma única fonte de verdade** para o catálogo de permissões e **derivar dela**, de forma verificável, todos os artefatos que dependem dele.

## Drivers da decisão

- **Fonte única de verdade** — evitar divergência entre backend, frontend e provedor de identidade.
- **Metadados ricos por permissão** — sensibilidade, base legal, escopo de contexto exigido, verificações obrigatórias, quem pode receber a permissão.
- **Artefatos derivados e consistentes** — constantes de código, manifesto de provisionamento, inventário LGPD, relatório de permissões efetivas.
- **Travas contra deriva** — o desvio entre a fonte e os artefatos gerados deve falhar a integração contínua.

## Opções consideradas

- **A**: Permissões declaradas em código .NET (constantes), espalhadas e referenciadas onde necessário.
- **B**: **Catálogo declarativo único de permissões** (um arquivo versionado), do qual um gerador produz os artefatos, com testes de conformidade travando a deriva.
- **C**: Catálogo de permissões mantido no banco de dados, lido em runtime.

## Resultado da decisão

**Escolhida:** "B — catálogo declarativo único de permissões com geração de artefatos", porque centraliza a verdade num único arquivo versionado, carrega os metadados ricos que a decisão de acesso e a LGPD exigem, e permite gerar artefatos consistentes com travas automáticas contra deriva.

O catálogo de permissões é um arquivo declarativo único (`permissions.yml`) no `uniplus-api`. Cada permissão declara, além do código (`{módulo}:{recurso}:{ação}`), os metadados: sensibilidade, base legal padrão, se exige autenticação multifator ou dupla aprovação, o escopo de contexto obrigatório, quem pode recebê-la e quais verificações de contexto são exigidas na decisão.

Um gerador (codegen) produz, a partir desse arquivo:

- as **constantes .NET** consumidas pelo backend;
- as **constantes de frontend**, para consumo pelo `uniplus-web` (a forma de publicação e o versionamento desse contrato entre repositórios são decididos em ADR própria desta frente, não aqui);
- o **manifesto de provisionamento** dos grupos no provedor de identidade;
- o **inventário LGPD** (classificação e base legal por operação);
- o **relatório de permissões efetivas** (o que cada perfil concede).

Testes de conformidade (fitness tests) garantem que: toda permissão referenciada no código existe no catálogo de permissões; os artefatos gerados estão sincronizados com a fonte (sem deriva); toda permissão sensível tem base legal; os nomes de verificação de contexto declarados existem no backend. O catálogo de permissões é a fonte; o código não declara permissões fora dele.

## Consequências

### Positivas

- Uma única fonte de verdade; os artefatos para backend, frontend e provedor de identidade ficam consistentes por construção.
- Metadados de sensibilidade e base legal ficam ao lado da permissão — o inventário LGPD é derivado, não mantido à parte.
- A deriva entre fonte e artefatos é detectada pela integração contínua, não em produção.
- A geração das constantes de frontend a partir da mesma fonte elimina a divergência manual entre backend e frontend.

### Negativas

- O gerador e os testes de conformidade são infraestrutura a manter.
- Toda mudança de permissão exige regenerar os artefatos e revisar o resultado.

### Neutras

- A forma concreta do arquivo (estrutura dos campos) evolui com a frente; esta ADR fixa que **existe uma fonte declarativa única e artefatos gerados**, não o detalhe de cada campo.

## Confirmação

- **Fitness test**: nenhuma permissão é referenciada no código sem existir no catálogo de permissões; os artefatos gerados conferem com a fonte (regenerar não produz diferença); permissões sensíveis têm base legal.
- **Regeneração determinística**: rodar o gerador sobre o catálogo de permissões não produz diferença em relação aos artefatos versionados (qualquer divergência falha a integração contínua).

## Prós e contras das opções

### A — Permissões em constantes de código espalhadas

- Bom, porque não exige gerador nem arquivo novo.
- Ruim, porque diverge entre backend, frontend e provedor de identidade; os metadados (sensibilidade, base legal) não têm lugar único; o conjunto efetivo de um perfil deixa de ser conhecido.

### B — Catálogo declarativo único de permissões com codegen (escolhida)

- Bom, porque há uma fonte de verdade, metadados ricos centralizados e artefatos consistentes com travas contra deriva.
- Ruim, porque adiciona um gerador e testes de conformidade a manter.

### C — Catálogo de permissões no banco em runtime

- Bom, porque permite alterar permissões sem novo build.
- Ruim, porque a referência em tempo de compilação (constantes tipadas) se perde e a geração determinística de artefatos para outros repositórios deixa de ser possível.

## Mais informações

- Ancora no modelo de decisão da [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md): o catálogo de permissões é o vocabulário que o ponto de decisão exige.
- O manifesto de provisionamento liga-se à estratégia de grupos do provedor de identidade e o inventário LGPD liga-se à proteção de dado por permissão — ambos detalhados em ADRs próprias desta frente.
- **A publicação e o versionamento do contrato gerado para o frontend (entre repositórios) são uma decisão à parte, objeto de ADR própria desta frente** — esta ADR decide apenas a existência da fonte única e do gerador.
- A forma de roteamento e a separação por módulo seguem a [ADR-0064](0064-convencao-roteamento-path-based-com-prefixo-modulo.md); os códigos de permissão usam o mesmo prefixo de módulo.
