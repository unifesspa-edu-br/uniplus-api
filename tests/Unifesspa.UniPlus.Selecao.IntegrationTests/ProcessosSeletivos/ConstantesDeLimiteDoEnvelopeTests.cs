namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Toda constante de <c>LimitesDoEnvelope</c> é confrontada com o schema por algum teste. Uma constante que ninguém confronta
/// é uma que pode divergir da coluna em silêncio, e a proteção passaria a valer menos do que diz valer.
/// </summary>
public sealed class ConstantesDeLimiteDoEnvelopeTests
{
    // Exercidas em LimitesDosTiposEmbutidosBatemComOSchemaTests e em PrazoDoRecursoBateComOSchemaTests.
    private static readonly string[] ConfrontadasNosTiposEmbutidos =
    [
        "RegraCodigo", "RegraVersao", "CensoReferencia", "PrecisaoPercentual", "PrecisaoPrazo",
        "UnidadeAdministradoraSigla", "UnidadeAdministradoraCodigo", "UnidadeAdministradoraSlug", "UnidadeAdministradoraNome", "UnidadeAdministradoraTipo",
        "UnidadeAdministradoraCidadeCodigoIbge", "UnidadeAdministradoraCidadeNome", "UnidadeAdministradoraCidadeUf",
        "TipoEtapaCodigo", "TipoEtapaNome",
        "GrupoAreaEnemCodigo", "GrupoAreaEnemRotulo",
    ];

    // Limites que não são coluna do agregado, cada um com o motivo de ficar fora do confronto com o schema.
    private static readonly Dictionary<string, string> SemColunaDoAgregado = new(StringComparer.Ordinal)
    {
        // Os DadosEdital são do ato, não da configuração. O limite vem dos validators de publicar e de retificar (60), e é deles que
        // o decoder tem de ser tão estrito quanto.
        ["NumeroDoAto"] = "dado do ato, conferido pelos validators de publicar e de retificar",

        // A coluna da escala da nota final é definida pela mesma constante (a da NotaFinal); o limite do envelope a espelha, e não
        // há segunda cópia que possa divergir.
        ["PrecisaoEscalaNotaFinal"] = "definida pela mesma constante da nota final",

        // O impedimento é gravado em jsonb. O limite é o da forma do item, que o domínio confere ao criar o campo, e a constante é
        // definida pela mesma constante da forma.
        ["MensagemDoImpedimento"] = "jsonb, limite da forma do item",

        // Os níveis da escala de atendimento também vivem em jsonb: o teto do rótulo é o do domínio, e a fração cabe em
        // numeric(5,4) por construção, já que o domínio a limita ao intervalo de 0 a 1.
        ["NivelDeAtendimentoRotulo"] = "jsonb, teto do domínio",
        ["PrecisaoFracaoDeAtendimento"] = "jsonb, intervalo de 0 a 1 limitado pelo domínio",
    };

    [Fact(DisplayName = "Nenhuma constante de LimitesDoEnvelope fica sem confronto com o schema")]
    public void TodaConstante_EConfrontada()
    {
        HashSet<string> confrontadas =
        [
            .. LimitesDoEnvelopeBatemComOSchemaTests.Comprimentos.Select(static c => c.Nome),
            .. LimitesDoEnvelopeBatemComOSchemaTests.Precisoes.Select(static p => p.Nome),
            .. ConfrontadasNosTiposEmbutidos,
            .. SemColunaDoAgregado.Keys,
        ];

        ConstantesDeLimite().Except(confrontadas).Should().BeEmpty(
            "toda constante de LimitesDoEnvelope tem de ser confrontada com a coluna que ela espelha; senão ela pode divergir do "
            + "schema sem que nada acuse, e o decoder volta a aceitar o que o banco recusa");
    }

    private static IEnumerable<string> ConstantesDeLimite() => typeof(LimitesDoEnvelope)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(static f => f.IsLiteral)
        .Select(static f => f.Name);
}
