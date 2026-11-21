---
status: "accepted"
date: "2026-11-21"
decision-makers:
  - "Tech Lead"
---

# ADR-0178: O texto público de cada código de erro é escrito na API

## Contexto e enunciado do problema

O registro da API define os códigos de erro, mas os textos do catálogo público eram
escritos em outro repositório. Uma alteração podia deixar o endereço do erro sem
página ou manter pública a descrição de um código já retirado.

## Opções consideradas

- Manter o catálogo escrito no portal, com conferência por revisão.
- Escrever as descrições na API e gerar o catálogo no portal.

## Resultado da decisão

A API versiona o texto público em `contracts/descricoes-de-erros.json`. O mesmo
PR que adiciona, altera ou retira um código atualiza a descrição correspondente.
O teste exige correspondência nos dois sentidos com `codigos-de-erro.json`,
que continua a fornecer título e status HTTP.

O documento tem versão de formato e uma lista em ordem ordinal por código.
Cada entrada contém os requisitos aplicáveis, o que aconteceu, como resolver e,
quando necessário, onde o erro aparece. O texto usa blocos de Markdown simples,
sem dados pessoais: parágrafos, listas, tabelas e blocos cercados por três crases,
com linguagem opcional `json` ou `text`. Links podem apontar para HTTPS ou para o
portal. Expressões entre chaves só são aceitas em código. A proibição de dados
pessoais também vale dentro dos blocos de código. A reformatação conserva os
textos existentes.

O portal gera uma página por código a partir dos arquivos da API. O estado
rascunho deixa de existir. As ADRs do portal definem como copiar e apresentar
os arquivos.

## Consequências

- A descrição é conferida junto com a alteração que pode mudar a resposta.
- O portal documenta o commit sincronizado da `main` da API. A versão em
  produção pode estar em outro commit.

## Confirmação

`DescricoesDeErrosPublicadasTests` confere os pares de códigos, o formato e o
conteúdo de `contracts/descricoes-de-erros.json`.
