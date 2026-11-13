namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

/// <summary>Uma inscrição ainda em rascunho, que não é inscrição enviada, e o arquivo verificado que ela guarda.</summary>
internal sealed record RascunhoDeInscricao(Guid Id, Guid ArquivoId);
