namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.ValueObjects;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// O que a preparação conferiu do ato antes de saber a data dele: a data ausente é recusada à parte, e o ato só é montado
/// com ela, para que nenhuma data de valor zero chegue a existir.
/// </summary>
internal sealed record AtoConferido(
    ProcessoDivulgadoConferido Processo,
    TipoAtoPublicadoView Tipo,
    DocumentosDoAtoConferidos Documentos,
    DateTimeOffset PublicadoEm,
    IdentidadeDoItemDoEdital? ItemDoEdital = null,
    string? PoliticaConfirmadaPorContaId = null)
{
    public AtoAPublicar ComAData(IDadosDoAtoPublicado dados, DateOnly dataDoAto) =>
        new(dados, dataDoAto, Processo, Tipo, Documentos, PublicadoEm, ItemDoEdital, PoliticaConfirmadaPorContaId);
}
