namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Conversores do ciclo de aprovação, do parecer e do texto das manifestações técnicas da proteção do catálogo
/// (ADR-0163), compartilhados pelo circuito na linha do item, pelo ato e pela manifestação.
/// </summary>
internal static class ManifestacaoDaProtecaoConversores
{
    internal const int ParecerMaxLength = 20;

    internal static readonly ValueConverter<CicloDaAprovacao, int> Ciclo = new(ciclo => ciclo.Numero, numero => new CicloDaAprovacao(numero));

    internal static readonly ValueConverter<ParecerDaManifestacao, string> Parecer =
        new(parecer => PareceresDaManifestacao.ParaTokenCanonico(parecer), token => PareceresDaManifestacao.Analisar(token));

    // O EF não chama o conversor para o texto ausente: a coluna nula continua nula.
    internal static readonly ValueConverter<Fundamentacao?, string> Texto = new(texto => texto!.Valor, valor => Fundamentacao.Analisar(valor));
}
