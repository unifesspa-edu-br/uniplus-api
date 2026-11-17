---
status: "proposed"
date: "2026-11-16"
decision-makers:
  - "Tech Lead"
consulted:
  - "CEPS (dono do processo)"
informed:
  - "Equipe Seleção"
  - "Equipe Configuração"
---

# ADR-0172: O documento de configuração do processo é descrito por um esquema JSON versionado

## Contexto e enunciado do problema

A configuração de um processo seletivo precisa sair de um processo e entrar em outro, e de uma versão do sistema para outra, por um contrato público, estável e validável fora da API: quem importa, quem exporta e quem escreve um rascunho à mão precisam saber o que é válido sem ler o código. O envelope canônico da publicação ([ADR-0109](0109-envelope-canonico-v2-do-congelamento.md)) serve ao congelamento e à prova de integridade, e não a esse papel: ele carrega identificadores internos e a forma dele é decidida pela publicação.

A [ADR-0051](0051-apicurio-schema-registry-avro-wolverine.md) define o registro de esquemas, que também guarda esquema JSON 2020-12, e a [ADR-0066](0066-ofertacurso-modelo-tres-niveis-emec-por-campus.md) trata a oferta de curso como cadastro citado pelo processo. Falta decidir como o documento de configuração é descrito, referenciado e evoluído.

## Drivers da decisão

- O documento circula entre processos e versões; o que o descreve não pode depender de identificador do banco.
- Editar o texto de um item (rótulo, nome) não pode virar retirada e inclusão do item nas retificações.
- O contrato precisa poder crescer sem quebrar quem já exporta, e o que quebra tem de ser detectado por teste, não por revisão.
- Dado pessoal e dado sensível não entram no documento.

## Opções consideradas

- Esquema JSON 2020-12 versionado no repositório, dividido por assunto, com referências por código e por chave local.
- Reaproveitar o envelope canônico como contrato de troca.
- Esquema único, em um só arquivo, sem versionamento por pasta.

## Resultado da decisão

**Escolhida:** "Esquema JSON 2020-12 versionado no repositório, dividido por assunto, com referências por código e por chave local", porque separa o contrato de troca do envelope de publicação e o torna verificável por teste.

- **Forma:** os esquemas ficam em `contracts/selecao/configuracao/vN/`, um arquivo por assunto, com `$id` absoluto e `$ref` relativo e só local. O arquivo `documento` é a raiz; as definições compartilhadas ficam em arquivos próprios. O esquema descreve a forma (tipo, formato, enumeração, tamanho, obrigatoriedade e restrições estruturais entre campos vizinhos); a regra de negócio é do domínio, e por isso um rascunho incompleto é um documento válido.
- **Identidade:** catálogos e unidades são citados pelo código estável, nunca por identificador do banco, e os campos de código de catálogo recusam a forma de identificador único (UUID). As partes que o usuário edita (fase, etapa, item de avaliação, banca, exigência) têm chave local única no documento, que é o que as outras partes citam; a parte identificada por códigos de catálogo mantém a identidade natural. A pessoa é citada pelo identificador da conta dela no Uni+, texto opaco, nunca por dado pessoal; o esquema não distingue um identificador de conta de outro texto, e a recusa do código que não é conta do cadastro é do domínio. O nome vem do cadastro na emissão.
- **Só leitura:** o descritivo que a exportação traz ao lado do código (nomes, siglas) e o que é derivado ficam marcados `readOnly`, e a importação os ignora: a referência é o código, e o servidor recalcula o derivado.
- **Resolução:** a importação resolve cada código citado uma vez, contra o cadastro vivo, e dali decide o identificador do snapshot. Código que não existe é recusado apontando a posição no documento. Na retificação, código igual ao já congelado herda a cópia congelada.
- **Evolução:** a compatibilidade é para trás dentro da versão: documento válido na `v1` continua válido. Entram na versão a propriedade opcional nova, o valor novo de enumeração e o limite relaxado. Criam a versão seguinte (outra pasta, outro `$id`, outro `versaoFormato`) remover ou renomear propriedade, tornar propriedade obrigatória, estreitar tipo, formato, limite ou enumeração e mudar o significado de um campo.
- **Congelamento:** a versão congela na primeira tag que a publica, e a cópia dos arquivos passa a `publicadas/vN/`. Dali em diante um teste compara o esquema atual com cada cópia publicada e recusa a mudança incompatível que o comparador reconhece; o que ele não examina fica para a revisão.
- **Registro:** o esquema é registrado no registro de esquemas com regra de compatibilidade desligada, porque a versão atual do registro recusa a segunda versão de um esquema 2020-12 sob regra de compatibilidade; a garantia vive no teste acima.

> **Emenda (2026-11-17) — a ordem de uma lista é a posição do item.**
>
> A ordem de várias listas do documento era um número declarado ao lado da posição do item: duas fontes para a mesma informação, e recusas de ordem repetida que só existiam por causa disso. Quando a ordem de uma lista é a sequência dos itens, **ela é a posição do item na lista enviada, e o documento não traz número de ordem**. Quem lê atribui a ordem pela posição de origem, começando em 1, sem renumerar quando alguma parte é recusada; a recusa que depende da ordem aponta o item.
>
> Reordenar é mover o item, e a chave dele não muda. Deixam de existir as recusas de ordem repetida vindas do documento. Cada lista adota o critério na entrega que a lê; a das fases do cronograma já o adota.
>
> **Emenda (2026-11-17) — a publicação do esquema no contrato REST.**
>
> A OpenAPI do módulo publica o esquema como componentes, derivados na geração dos mesmos arquivos, sem cópia. A raiz é `DocumentoDaConfiguracao`; cada arquivo e cada definição alcançados viram um componente, com nome fixado por regra (prefixo, arquivo, definição). As referências relativas passam a apontar o componente, e `$id` e `$schema` saem. Os arquivos continuam sendo a fonte e o que a API usa para validar; os clientes geram tipos a partir da OpenAPI.
>
> **Confirmação:** teste de contrato em que cada componente do documento na OpenAPI publicada é o trecho correspondente dos arquivos, com as referências trocadas pela regra, sem componente sobrando ou faltando.
>
> **Consequências:** a OpenAPI do módulo cresce o tamanho do esquema. Os geradores de tipo ignoram as condicionais (`if`/`then`/`else`), então o tipo gerado é mais largo que o esquema, e a validação da forma é a da API. A versão seguinte do formato decide o nome dos componentes dela.

## Consequências

### Positivas

- Quem importa, exporta ou escreve o documento valida contra um contrato público, sem a API.
- Mudança incompatível é detectada por teste antes de chegar à publicação.
- O documento não carrega identificador interno do banco nem dado pessoal; o único identificador dele é o do próprio processo, só de leitura e ignorado na importação.

### Negativas

- Todo catálogo citado precisa de código único entre os itens vivos e de leitura por código; o que ainda não tem passa a ser entrega prévia.
- O esquema conta caracteres Unicode e o domínio conta unidades UTF-16: em casos extremos o esquema aceita o que o domínio recusa, e a recusa final é do domínio.

### Neutras

- O envelope canônico de publicação continua como está; esquema e envelope têm funções distintas e nomes que devem coincidir nos conceitos que compartilham.

## Confirmação

- Teste de contrato: os exemplos publicados validam contra o esquema, o rascunho incompleto é aceito, e tipo errado, propriedade desconhecida e identificador interno no lugar do código são recusados com o caminho.
- Teste de compatibilidade: o esquema atual contra cada cópia presente em `publicadas/`, com pares sintéticos para cada regra do comparador. A presença das cópias depois da primeira tag é conferida na revisão dessa tag.
- Testes de paridade: cada enumeração do esquema tem os valores do vocabulário do domínio, e os limites de tamanho listados batem com os que o codec aplica (iguais onde vêm de coluna do domínio, nunca maiores onde o esquema é mais estreito).

## Prós e contras das opções

### Esquema por assunto, versionado, com referência por código e chave local

- Bom, porque separa o contrato de troca do envelope, evita identificador interno e se verifica por teste.
- Ruim, porque exige código estável e leitura por código em cada catálogo citado.

### Reaproveitar o envelope canônico

- Bom, porque não cria um segundo formato.
- Ruim, porque o envelope carrega identificador interno e muda conforme a publicação; vincular a importação a ele travaria os dois.

### Esquema único sem versionamento por pasta

- Bom, porque é um arquivo só.
- Ruim, porque passaria de mil linhas, e uma mudança incompatível não teria onde nascer sem quebrar quem já exporta.

## Mais informações

- [ADR-0051](0051-apicurio-schema-registry-avro-wolverine.md), [ADR-0066](0066-ofertacurso-modelo-tres-niveis-emec-por-campus.md) e [ADR-0109](0109-envelope-canonico-v2-do-congelamento.md).
- Identidade e proteção de dados: [ADR-0078](0078-modelo-de-autorizacao-pbac-abac.md), [ADR-0121](0121-criptografia-de-dados-sensiveis-em-repouso.md) e [ADR-0153](0153-projecao-por-protecao.md).
