namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// A leitura única do catálogo de fatos e dos tipos de documento vivos, compartilhada pelos gates e
/// pelos resolvedores que congelam vocabulário: duas leituras abririam a janela para uma conferência
/// aprovar sobre um catálogo e um resolvedor congelar sobre outro.
/// </summary>
internal sealed record CatalogoConferido(
    IReadOnlyList<FatoCandidatoView> Fatos,
    IReadOnlyDictionary<string, FatoCandidatoView> PorCodigo,
    FatosDeModalidade FatosDeModalidade,
    IReadOnlyDictionary<Guid, TipoDocumentoView> TiposVivos);

internal static class ConferenciaDoCatalogoEDaProtecao
{
    /// <summary>
    /// Lê o catálogo e confere que a proteção que o envelope congela está aprovada pela proteção de
    /// dados (ADR-0153). A leitura dos tipos de documento que a confere é a mesma que a proteção
    /// congelada usa depois.
    /// </summary>
    internal static async Task<Result<CatalogoConferido>> ConferirAsync(
        ProcessoSeletivo processo,
        IFatoCandidatoReader fatos,
        ITipoDocumentoReader tiposDeDocumento,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos = await fatos
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos = await ProtecaoVigenteDasExigenciasDoProcesso
            .LerTiposVivosAsync(processo, tiposDeDocumento, cancellationToken)
            .ConfigureAwait(false);

        Result protecaoAprovada = ConferenciaDaAprovacaoDaProtecao.Conferir(processo, tiposVivos, catalogoDeFatos);
        if (protecaoAprovada.IsFailure)
        {
            return Result<CatalogoConferido>.ValidationFailure(protecaoAprovada.Errors);
        }

        return Result<CatalogoConferido>.Success(new CatalogoConferido(
            catalogoDeFatos,
            catalogoDeFatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal),
            VocabularioDeFatos.ComValoresDeModalidade(catalogoDeFatos),
            tiposVivos));
    }
}
