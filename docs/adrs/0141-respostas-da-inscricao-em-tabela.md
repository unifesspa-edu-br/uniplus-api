---
status: "proposed"
date: "2026-10-10"
decision-makers:
  - "Tech Lead"
consulted: []
informed: []
---

# ADR-0141: Respostas da inscrição em tabela, com tipo e origem de cada valor

## Contexto e enunciado do problema

A inscrição do candidato grava o que ele respondeu ao formulário de uma versão fixada (UNI-REQ-0084). A primeira implementação guardava as respostas, as ocorrências de grupo e os aceites de termo em colunas `jsonb` de `selecao.inscricoes`, como um dicionário do código do fato para o valor.

A resposta da inscrição é dado vivo. A classificação e a habilitação filtram candidatos por fato: quem concorre em cota de renda, quem se declarou indígena, em que município reside. Sobre um dicionário em `jsonb`, cada filtro vira operador JSON sem tipo e sem índice por fato. O dicionário também não registra como o valor foi obtido: o que o candidato digitou ou selecionou fica igual ao que o sistema derivou de uma resposta anterior, como o município e a UF localizados pelo CEP.

No módulo Seleção, `jsonb` existe para o que é congelado: a versão publicada do processo e o comprovante de inscrição são instantâneos imutáveis com hash, lidos inteiros e nunca filtrados por fato.

## Drivers da decisão

- Consulta entre candidatos por fato e valor, com índice.
- Origem de cada valor gravada junto dele: informado pelo candidato ou derivado de resposta.
- Valor gravado no tipo do fato, não como JSON sem tipo.
- Número de documento cifrado em repouso ([ADR-0121](0121-criptografia-de-dados-sensiveis-em-repouso.md)).

## Opções consideradas

- Dicionário em `jsonb` com índice GIN.
- Tabela larga, uma coluna por fato.
- Tabela de respostas, uma linha por valor, com uma coluna de valor por tipo.

## Resultado da decisão

**Escolhida:** "tabela de respostas, uma linha por valor", porque é a única das três que atende a consulta tipada sem fixar no esquema quais fatos existem. O formulário é configurado por processo e os fatos variam.

Cada linha registra a inscrição, o código do fato, a ocorrência do grupo repetível a que pertence (nenhuma, no nível superior), a posição do valor num fato multivalorado, a origem e o tipo. O valor fica na coluna do tipo: booleano, número, código, texto livre ou data. Código é o valor de vocabulário fechado (opção categórica, município, UF), que a classificação filtra; só ele tem índice. Texto livre, como um nome, não é filtro. Uma restrição de banco garante que só a coluna do tipo esteja preenchida. O tipo de cada fato vem da definição da resposta do item na versão fixada do formulário. O número de documento fica cifrado na própria linha, sem valor em claro. O endereço é um valor estruturado: tem tabela própria, com uma coluna por componente. As ocorrências de grupo e os aceites de termo também têm tabelas próprias.

`jsonb` continua reservado ao que é congelado. O comprovante de inscrição segue como documento, com as respostas informadas separadas das derivadas.

## Consequências

### Positivas

- A classificação filtra candidatos por fato e valor com índice.
- A origem de cada valor fica no banco.
- Uma resposta cujo valor não corresponde ao tipo do fato é recusada antes de gravar.

### Negativas

- Ler uma inscrição remonta as respostas a partir de várias linhas.
- Toda gravação substitui as linhas da inscrição, em vez de reescrever uma coluna.

## Mais informações

- Issue #1870.
- [ADR-0121](0121-criptografia-de-dados-sensiveis-em-repouso.md): cifra de dado pessoal na fronteira do repositório.
