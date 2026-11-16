namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ImportacaoDoResultado;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>Leiautes fictícios do catálogo de leiautes: o domínio do processo não tem lista fixa de leiaute nem de fato.</summary>
internal static class LeiauteDeTeste
{
    public const string Versao = "v1";

    public static DadoDoArquivo EmTodaLinha(string fato) => new(fato, PresencaDoDadoNoArquivo.EmTodaLinha);

    public static DadoDoArquivo Opcional(string fato) => new(fato, PresencaDoDadoNoArquivo.Opcional);

    public static LeiauteDoRelatorio Criar(string codigo, params DadoDoArquivo[] dados) => new(codigo, Versao, $"hash-{codigo}", dados);

    public static DeclaracaoDaImportacaoDoResultado Declarar(Guid? primeiraFaseId, params string[] codigosDosLeiautes) =>
        DeclaracaoDaImportacaoDoResultado
            .Declarar([.. codigosDosLeiautes.Select(static codigo => new RelatorioInformado(codigo, Versao))], primeiraFaseId)
            .Value!;
}
