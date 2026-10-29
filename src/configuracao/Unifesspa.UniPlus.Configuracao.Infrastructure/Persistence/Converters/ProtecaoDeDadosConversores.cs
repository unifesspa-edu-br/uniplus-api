namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Conversor da hipótese legal para o token textual (UPPER_SNAKE), compartilhado pelo fato do
/// candidato e pelo tipo de documento. A classificação usa o conversor do Infrastructure.Core.
/// </summary>
internal static class ProtecaoDeDadosConversores
{
    internal const int HipoteseLegalMaxLength = 40;

    internal static readonly ValueConverter<HipoteseLegalTratamento, string> HipoteseLegal =
        new(hipotese => HipotesesLegaisTratamento.ParaTokenCanonico(hipotese), token => HipotesesLegaisTratamento.Analisar(token));
}
