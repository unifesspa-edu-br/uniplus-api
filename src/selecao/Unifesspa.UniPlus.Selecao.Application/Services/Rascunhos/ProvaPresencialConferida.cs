namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// A prova presencial do documento lida e o que o Geo respondeu sobre os municípios. Com o Geo respondendo, as causas são as da
/// conferência da publicação. Com o Geo fora do ar, nenhum município fica aprovado por omissão: a causa é uma só, no campo dos
/// municípios, e o rascunho é salvo do mesmo modo.
/// </summary>
/// <param name="Lida">A declaração que a leitura aceitou ou a recusa dela.</param>
/// <param name="NoGeo">Os municípios conferidos, ou a indisponibilidade do Geo.</param>
public sealed record ProvaPresencialConferida(ProvaPresencialLida Lida, Result<MunicipiosConferidosNoGeo> NoGeo)
{
    private static readonly FieldError GeoIndisponivel = new(
        PonteiroNoDocumento.Converter(DeclaracaoDaProvaPresencial.Campo + ".municipios", OrigemNoDocumento.Identidade),
        new DomainError(
            ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel,
            "O Geo não respondeu e os municípios de prova não foram conferidos. O rascunho foi salvo, e a conferência se repete na publicação."));

    /// <summary>As causas que a prova presencial aponta no documento, na ordem da conferência.</summary>
    public IReadOnlyList<FieldError> Causas() => NoGeo.IsSuccess ? Lida.Causas(NoGeo.Value!) : [GeoIndisponivel];
}
