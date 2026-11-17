namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// O catálogo de fatos e os tipos de documento vivos, lidos uma só vez: os gates e os resolvedores que congelam
/// vocabulário compartilham esta leitura, porque duas abririam a janela para uma conferência aprovar sobre um
/// catálogo e um resolvedor congelar sobre outro.
/// </summary>
internal sealed record CatalogoVivo(
    IReadOnlyList<FatoCandidatoView> Fatos,
    IReadOnlyDictionary<string, FatoCandidatoView> PorCodigo,
    FatosDeModalidade FatosDeModalidade,
    IReadOnlyDictionary<Guid, TipoDocumentoView> TiposVivos);

/// <summary>Lê o catálogo de fatos e os tipos de documento vivos, uma só vez por operação.</summary>
public sealed class ConferenciaDoCatalogoEDaProtecao(IFatoCandidatoReader fatos, ITipoDocumentoReader tiposDeDocumento)
{
    internal async Task<CatalogoVivo> LerAsync(ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> catalogoDeFatos = await fatos.ListarAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, TipoDocumentoView> tiposVivos = await ProtecaoVigenteDasExigenciasDoProcesso
            .LerTiposVivosAsync(processo, tiposDeDocumento, cancellationToken)
            .ConfigureAwait(false);

        return new CatalogoVivo(
            catalogoDeFatos,
            catalogoDeFatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal),
            VocabularioDeFatos.ComValoresDeModalidade(catalogoDeFatos),
            tiposVivos);
    }
}
