---
status: "accepted"
date: "2026-05-31"
decision-makers:
  - "Tech Lead"
---

# ADR-0072: Correlação apresentação↔exigência pela identidade congelada (`exigencia_id`), não pelo tipo de documento

## Contexto e enunciado do problema

Ao avaliar o snapshot congelado ([ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md)), a validação documental precisa casar cada documento apresentado pelo candidato com a exigência correspondente.

Casar por **tipo de documento** é ambíguo: um mesmo tipo (por exemplo, laudo médico) pode figurar em mais de uma exigência distinta no mesmo processo — atendimento especializado e reserva de vagas, por exemplo — com regras e bases legais diferentes. Além de ambíguo, casar por tipo tenderia a reler a configuração viva para desambiguar, reintroduzindo a dependência que a ADR-0070 eliminou.

A questão a decidir é: **por qual chave o documento apresentado se correlaciona com a exigência congelada**.

## Drivers da decisão

- **Múltiplas exigências do mesmo tipo.** É preciso suportar duas ou mais exigências do mesmo tipo de documento, com regras distintas, no mesmo processo.
- **Não reler o vivo.** A correlação não pode reabrir a leitura da configuração viva no runtime — preservar a imunidade da ADR-0070.
- **Auditabilidade.** A correlação precisa ser estável e reproduzível.

## Opções consideradas

- **A. Congelar a identidade estável da exigência (`exigencia_id`) no snapshot** e correlacionar por ela.
- **B. Correlacionar por tipo de documento** — ambíguo com exigências repetidas e propenso a reler o vivo.

## Resultado da decisão

**Escolhida:** "A — correlação por `exigencia_id` congelado", porque é estável, determinística e dispensa releitura da configuração viva.

O snapshot congela o identificador estável de cada exigência (`exigencia_id`). A apresentação do candidato referencia esse identificador, e a correlação apresentação↔exigência se dá **por `exigencia_id`**. A paridade entre o que foi congelado e o que foi apresentado é avaliada célula a célula por `exigencia_id`.

## Consequências

### Positivas

- Suporta múltiplas exigências do mesmo tipo de documento com regras distintas.
- Correlação estável e auditável sem reler a configuração viva, reforçando a imunidade da ADR-0070.

### Negativas

- Cada apresentação precisa honrar o `exigencia_id` congelado correspondente.

## Confirmação

- **Paridade por `exigencia_id`** entre o snapshot e os documentos apresentados.
- **Determinismo com tipos repetidos:** duas exigências do mesmo tipo resolvem de forma determinística, cada uma pelo fato vivo do candidato que a aciona.

## Mais informações

- Requisito de rastreabilidade: **UNI-REQ-0058**.
- [ADR-0070](0070-validacao-runtime-avalia-snapshot-congelado.md) — a validação avalia o snapshot congelado; a correlação por `exigencia_id` é o que torna esse cruzamento determinístico sem reler o vivo.
- Regra de negócio **RN08** — a identidade da exigência é congelada na publicação.
