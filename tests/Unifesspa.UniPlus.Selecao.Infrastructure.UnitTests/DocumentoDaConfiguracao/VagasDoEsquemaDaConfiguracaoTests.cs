namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Kernel.Results;

using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class VagasDoEsquemaDaConfiguracaoTests
{
    private const string Oferta = "ENG_CIVIL_MARABA";
    private static readonly LeituraDoDocumentoDaConfiguracao Leitura = new(new EsquemaDoDocumentoDaConfiguracao());

    private static Result<DocumentoLido> Ler(string distribuicoes, string demais = "") => Leitura.Ler(
        $$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "distribuicaoVagas": [ {{distribuicoes}} ] {{demais}} }""");

    [Theory(DisplayName = "A oferta repetida é recusada no campo de cada ocorrência posterior, mesmo com quantidades distintas")]
    [InlineData(2)]
    [InlineData(3)]
    public void Ler_OfertaRepetida_RecusaTodasAsPosteriores(int quantidade)
    {
        string distribuicoes = string.Join(',', Enumerable.Range(1, quantidade)
            .Select(static numero => $$"""{ "ofertaCursoCodigo": "{{Oferta}}", "vagasOfertadasBase": {{numero}} }"""));

        Result<DocumentoLido> resultado = Ler(distribuicoes);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static causa => causa.Field).Should().Equal(
            Enumerable.Range(1, quantidade - 1).Select(static indice => $"/distribuicaoVagas/{indice}/ofertaCursoCodigo"));
        resultado.Errors.Should().OnlyContain(static causa => causa.Error.Code == DistribuicaoDeVagasErrorCodes.OfertaDeCursoDuplicada);
        resultado.Errors.Should().OnlyContain(static causa => !causa.Error.Message.Contains(Oferta, StringComparison.Ordinal));
    }

    [Theory(DisplayName = "A distribuição vazia, única ou com ofertas distintas passa pela leitura")]
    [InlineData("")]
    [InlineData("""{ "ofertaCursoCodigo": "ENG_CIVIL_MARABA" }""")]
    [InlineData("""{ "ofertaCursoCodigo": "ENG_CIVIL_MARABA" }, { "ofertaCursoCodigo": "DIREITO_MARABA" }""")]
    public void Ler_SemRepeticao_Aceita(string distribuicoes) => Ler(distribuicoes).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "As ofertas repetidas acumulam com recusas de esquema e de chave local na ordem do documento")]
    public void Ler_RepeticaoEOutrasRecusas_AcumulaNaOrdemDoDocumento()
    {
        Result<DocumentoLido> resultado = Ler(
            """{ "ofertaCursoCodigo": "ENG_CIVIL_MARABA", "vagasOfertadasBase": 0 }, { "ofertaCursoCodigo": "ENG_CIVIL_MARABA" }""",
            """, "etapas": [ { "chave": "prova" }, { "chave": "prova" } ], "opcoesDeCurso": { "numero": 3 }""");

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/distribuicaoVagas/0/vagasOfertadasBase", DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            ("/distribuicaoVagas/1/ofertaCursoCodigo", DistribuicaoDeVagasErrorCodes.OfertaDeCursoDuplicada),
            ("/etapas/1/chave", DocumentoDaConfiguracaoErrorCodes.ChaveRepetida),
            ("/opcoesDeCurso/numero", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));
    }

    [Fact(DisplayName = "Duas ofertas diferentes que se repetem são recusadas na ordem de entrada, sem agrupar as causas pelo código")]
    public void Ler_DuasOfertasRepetidas_ConservaAOrdemDasOcorrencias()
    {
        Result<DocumentoLido> resultado = Ler("""
            { "ofertaCursoCodigo": "ENG_CIVIL_MARABA" }, { "ofertaCursoCodigo": "DIREITO_MARABA" },
            { "ofertaCursoCodigo": "DIREITO_MARABA" }, { "ofertaCursoCodigo": "ENG_CIVIL_MARABA" }
            """);

        resultado.Errors.Select(static causa => causa.Field).Should().Equal(
            "/distribuicaoVagas/2/ofertaCursoCodigo", "/distribuicaoVagas/3/ofertaCursoCodigo");
    }

    [Theory(DisplayName = "Os códigos ausentes, nulos ou de tipo errado ficam a cargo do esquema, preservando a posição das ofertas repetidas")]
    [InlineData("{}", "/distribuicaoVagas/0/ofertaCursoCodigo", DocumentoDaConfiguracaoErrorCodes.CampoObrigatorioAusente)]
    [InlineData("""{ "ofertaCursoCodigo": null }""", "/distribuicaoVagas/0/ofertaCursoCodigo", DocumentoDaConfiguracaoErrorCodes.TipoInvalido)]
    [InlineData("""{ "ofertaCursoCodigo": 1 }""", "/distribuicaoVagas/0/ofertaCursoCodigo", DocumentoDaConfiguracaoErrorCodes.TipoInvalido)]
    [InlineData("""{ "ofertaCursoCodigo": {} }""", "/distribuicaoVagas/0/ofertaCursoCodigo", DocumentoDaConfiguracaoErrorCodes.TipoInvalido)]
    [InlineData("null", "/distribuicaoVagas/0", DocumentoDaConfiguracaoErrorCodes.TipoInvalido)]
    public void Ler_OfertaInvalida_NaoImpedeAConferenciaDasDemais(string invalida, string campo, string codigo)
    {
        Result<DocumentoLido> resultado = Ler($$"""{{invalida}}, { "ofertaCursoCodigo": "{{Oferta}}" }, { "ofertaCursoCodigo": "{{Oferta}}" }""");

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            (campo, codigo), ("/distribuicaoVagas/2/ofertaCursoCodigo", DistribuicaoDeVagasErrorCodes.OfertaDeCursoDuplicada));
    }

    [Fact(DisplayName = "O esquema tem precedência sobre a repetição quando recusa o próprio código")]
    public void Ler_CodigoRepetidoForaDoFormato_RecusaSoUmaVezPorCampo()
    {
        Result<DocumentoLido> resultado = Ler("""{ "ofertaCursoCodigo": "invalido" }, { "ofertaCursoCodigo": "invalido" }""");

        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("/distribuicaoVagas/0/ofertaCursoCodigo", DocumentoDaConfiguracaoErrorCodes.ValorInvalido),
            ("/distribuicaoVagas/1/ofertaCursoCodigo", DocumentoDaConfiguracaoErrorCodes.ValorInvalido));
    }
}
