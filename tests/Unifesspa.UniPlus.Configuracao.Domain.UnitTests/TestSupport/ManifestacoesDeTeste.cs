namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem se manifesta nos cenários: Kevin e Lúcia, Encarregados de dados, e os conteúdos de manifestação que eles
/// registram.
/// </summary>
internal static class ManifestacoesDeTeste
{
    public const string Emissor = "https://sso.unifesspa.edu.br/realms/unifesspa";

    public static readonly AtorDoAto Kevin = new(Emissor, "kevin.carvalho", "Kevin Anderson Carvalho");
    public static readonly AtorDoAto Lucia = new(Emissor, "lucia.almeida", "Lúcia Bruna Maya Almeida");

    public static ConteudoDaManifestacao Adequada() => Conteudo("ADEQUADA", null);

    public static ConteudoDaManifestacao Inadequada(string texto = "O laudo revela condição de saúde: a classificação deve ser sensível.") =>
        Conteudo("INADEQUADA", texto);

    public static ConteudoDaManifestacao RecomendaAjuste(string texto = "Recomendo a classificação sensível.") =>
        Conteudo("RECOMENDA_AJUSTE", texto);

    public static ConteudoDaManifestacao Conteudo(string parecer, string? texto)
    {
        List<FieldError> erros = [];
        return ConteudoDaManifestacao.Ler(parecer, texto, erros)
            ?? throw new InvalidOperationException(string.Join("; ", erros.Select(static e => e.Error.Message)));
    }

    public static ItemDaProtecao ItemDe(TipoDocumento tipo) => new(CadastroDoCatalogo.TipoDocumento, tipo.Id);

    /// <summary>As manifestações já gravadas do tipo, no ciclo em que ele está.</summary>
    public static ManifestacoesDoCiclo Do(TipoDocumento tipo, params ManifestacaoDaProtecaoDoCatalogo[] eventos) =>
        ManifestacoesDoCiclo.De(ItemDe(tipo), tipo.Aprovacao.Ciclo, eventos);
}
