# Registro de Integração com o Gov.br

Este documento detalha o histórico técnico e administrativo para a obtenção das credenciais de acesso ao serviço gov.br para a aplicação UNI+.

## 1. Histórico de Solicitação

O processo de obtenção das credenciais foi iniciado formalmente em **24/04/2026**, sob o número de processo **308803.7099450/2026**, realizado através do portal de serviços do governo federal.

### Informações do Registro Inicial

- **Data de Início:** 24/04/2026
- **Local de Requerimento:** Solicitação de Processos Gov.br
- **Responsável pelos Dados da Aplicação:** Carlos Bruno Freitas Sardinha
- **Responsável pelo Setor (Chefia):** Rogério Rômulo da Silva

## 2. Cronograma de Homologação e Produção

Abaixo estão descritos os marcos temporais e as ações técnicas realizadas para a viabilização do ambiente:

| Data       | Evento                                                                 | Responsável     |
|------------|------------------------------------------------------------------------|-----------------|
| 24/04/2026 | Solicitação das credenciais.                                           | Carlos Bruno Freitas Sardinha |
| 27/04/2026 | Disponibilização das credenciais de homologação.                      | Jeferson Ferreira da Silva |
| 27/04/2026 | Testes de integração do Keycloak com o ambiente de homologação gov.br.| Jeferson Ferreira da Silva |
| 05/05/2026 | Disponibilização das credenciais de produção após envio de vídeo demonstrativo. | Jeferson Ferreira da Silva |
| 28/05/2026 | Configuração final e disponibilização para a comunidade universitária.      | Equipe Técnica  |

## 3. Procedimentos Técnicos

Para a transição entre os ambientes de homologação e produção, foi seguido o protocolo exigido pelo provedor de identidade, que incluiu a gravação e o envio de um vídeo demonstrativo da aplicação em funcionamento para o processo seletivo de credenciamento.

As credenciais de produção foram validadas pelo líder técnico Jeferson Ferreira da Silva, garantindo a integridade do fluxo de autenticação.

## 4. Status Atual do Projeto

A etapa de implementação do login via gov.br foi oficialmente concluída em **28/05/2026**.

Atualmente, o ambiente de **produção** está configurado com as credenciais gov.br definitivas, gerenciadas via Vault/secrets conforme ADR-0026, e a aplicação UNI+ segue em seu ciclo normal de desenvolvimento.

## Referência cruzada

- ADR-0026 — ```0026-paginacao-cursor-opaco-cifrado.md``` Paginação via cursor opaco cifrado e propagação por [`Link`](https://github.com/unifesspa-edu-br/uniplus-api/blob/c43a0df8ab3fc4069d072cd4719b949dbb8373df/docs/adrs/0026-paginacao-cursor-opaco-cifrado.md) header