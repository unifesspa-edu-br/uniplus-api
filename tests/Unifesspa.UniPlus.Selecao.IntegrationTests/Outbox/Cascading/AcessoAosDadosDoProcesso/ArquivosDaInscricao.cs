namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

/// <summary>
/// Os arquivos verificados de uma inscrição, um por classificação de proteção da exigência, mais um cuja exigência está
/// fora da versão congelada e por isso não tem classificação resolvida.
/// </summary>
internal sealed record ArquivosDaInscricao(
    Guid Publico, Guid Interno, Guid Pessoal, Guid Identificador, Guid Sensivel, Guid NaoResolvido);
