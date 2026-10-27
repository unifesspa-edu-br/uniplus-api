# Consultas de acompanhamento de processos seletivos

As consultas de acompanhamento (`GET /api/selecao/acompanhamento/processos-seletivos` e
`GET /api/selecao/acompanhamento/processos-seletivos/{id}`) só ficam disponíveis
temporariamente quando `Selecao:Acompanhamento:ValidacaoHabilitada` é `true`.
O padrão é `false`. Sem token válido, a autenticação responde `401`. Com a configuração
desligada, todo usuário autenticado recebe `403`, inclusive quando informa parâmetros
inválidos: a recusa acontece na autorização, antes da validação de cursor, limite e
media type.

Para uma validação controlada, configure no servidor:

```text
Selecao__Acompanhamento__ValidacaoHabilitada=true
```

O suporte é aceito apenas no ambiente `Development`, isto é, na stack local com dados
fictícios. Os ambientes implantados (HML e produção) rodam como `Production`
([ADR-0053](adrs/0053-zero-test-environment-branches-in-production-code.md)), e neles a
aplicação falha na inicialização se a opção estiver ligada. A configuração deve ser
injetada no processo da API; nenhuma configuração enviada pela Web concede autorização.
Não habilite a opção nem exponha dados reais sem autorização específica do CEPS
(issue #2104).

O acesso temporário protege somente as duas leituras acima. Ele não concede
roles nem altera as políticas de `ProcessoSeletivoController` ou de outros
endpoints.
