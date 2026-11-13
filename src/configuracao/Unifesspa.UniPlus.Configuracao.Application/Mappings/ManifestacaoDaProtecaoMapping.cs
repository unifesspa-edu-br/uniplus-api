namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

internal static class ManifestacaoDaProtecaoMapping
{
    /// <summary>As manifestações vigentes do ciclo, da mais antiga à mais recente.</summary>
    public static IReadOnlyList<ManifestacaoDaProtecaoDto> ToVigentesDto(this ManifestacoesDoCiclo manifestacoes)
    {
        ArgumentNullException.ThrowIfNull(manifestacoes);
        return [.. manifestacoes.Vigentes.Select(static m => m.ToDto(SituacaoDaManifestacao.Vigente))];
    }

    /// <summary>Todos os eventos do histórico, com a situação de cada um.</summary>
    public static IReadOnlyList<ManifestacaoDaProtecaoDto> ToDto(this HistoricoDasManifestacoes historico)
    {
        ArgumentNullException.ThrowIfNull(historico);
        return [.. historico.Eventos.Select(m => m.ToDto(historico.SituacaoDe(m)))];
    }

    private static ManifestacaoDaProtecaoDto ToDto(this ManifestacaoDaProtecaoDoCatalogo manifestacao, SituacaoDaManifestacao situacao) =>
        new(
            manifestacao.Id,
            manifestacao.Ciclo.Numero,
            PareceresDaManifestacao.ParaTokenCanonico(manifestacao.Parecer),
            manifestacao.Texto?.Valor,
            manifestacao.Autor.Nome,
            manifestacao.Instante,
            manifestacao.SubstituiId,
            Token(situacao));

    private static string Token(SituacaoDaManifestacao situacao) => situacao switch
    {
        SituacaoDaManifestacao.Vigente => "VIGENTE",
        SituacaoDaManifestacao.Substituida => "SUBSTITUIDA",
        SituacaoDaManifestacao.Encerrada => "ENCERRADA",
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, "Situação da manifestação fora do domínio fechado."),
    };
}
