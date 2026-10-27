# Consultas de acompanhamento de processos seletivos

As consultas de acompanhamento (`GET /api/selecao/acompanhamento/processos-seletivos` e
`GET /api/selecao/acompanhamento/processos-seletivos/{id}`) só ficam disponíveis
temporariamente quando `Selecao:Acompanhamento:ValidacaoHabilitada` é `true`.
O padrão é `false`. Com a configuração desligada, somente usuários autenticados
chegam à verificação e recebem `403`; sem token válido, a autenticação responde `401`.

Para uma validação controlada, configure no servidor:

```text
Selecao__Acompanhamento__ValidacaoHabilitada=true
```

O suporte é aceito apenas nos ambientes `Development`, `Testing` e `Staging`.
Se ativado em `Production` ou em qualquer ambiente não previsto, a aplicação
falha na inicialização. A configuração deve ser injetada no processo da API;
nenhuma configuração enviada pela Web concede autorização. Use exclusivamente
dados fictícios nesse ambiente de validação. Não habilite a opção nem exponha
dados reais sem autorização específica do CEPS (issue #2104).

O acesso temporário protege somente as duas leituras acima. Ele não concede
roles nem altera as políticas de `ProcessoSeletivoController` ou de outros
endpoints.
