using Wolverine.Attributes;

using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

// Wolverine descobre IWolverineExtension via [assembly: WolverineModule<T>] e
// aplica Configure() automaticamente quando o assembly é carregado pelo host.
// Em produção este assembly de testes não é referenciado — o atributo é
// inerte fora do contexto de testes integrados.
[assembly: WolverineModule<CascadingTestDiscoveryExtension>]
