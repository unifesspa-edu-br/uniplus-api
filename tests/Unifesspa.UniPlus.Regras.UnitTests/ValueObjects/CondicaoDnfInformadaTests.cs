namespace Unifesspa.UniPlus.Regras.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class CondicaoDnfInformadaTests
{
    private static JsonElement Json(string bruto)
    {
        using JsonDocument documento = JsonDocument.Parse(bruto);
        return documento.RootElement.Clone();
    }

    private static (string? Campo, string Codigo)[] Recusas(Result<CondicaoDnf> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    [Theory(DisplayName = "Cada código de operador declarado cria a condição com o operador dele")]
    [InlineData("IGUAL", Operador.Igual, "true")]
    [InlineData("ALGUM_DE", Operador.AlgumDe, "[\"A\"]")]
    [InlineData("MAIOR_IGUAL", Operador.MaiorIgual, "18")]
    [InlineData("MENOR_IGUAL", Operador.MenorIgual, "60")]
    [InlineData("DIFERENTE", Operador.Diferente, "true")]
    [InlineData("NENHUM_DE", Operador.NenhumDe, "[\"A\"]")]
    public void CriarInformada_CodigoDeclarado_CriaComOOperador(string codigo, Operador esperado, string valor)
    {
        Result<CondicaoDnf> resultado = CondicaoDnf.CriarInformada(" FATO ", codigo, Json(valor));

        resultado.IsSuccess.Should().BeTrue();
        (resultado.Value!.Fato, resultado.Value.Operador).Should().Be(("FATO", esperado));
    }

    [Fact(DisplayName = "O operador desconhecido e o fato em branco saem juntos, cada um no seu campo")]
    public void CriarInformada_FatoEmBrancoEOperadorDesconhecido_AcumulaAsDuasRecusas() =>
        Recusas(CondicaoDnf.CriarInformada("  ", "QUASE_IGUAL", Json("true")))
            .Should().Equal(("fato", "CondicaoDnf.FatoObrigatorio"), ("operador", "CondicaoDnf.OperadorInvalido"));

    [Fact(DisplayName = "O operador ausente é recusado como o desconhecido")]
    public void CriarInformada_OperadorAusente_RecusaOOperador() =>
        Recusas(CondicaoDnf.CriarInformada("FATO", null, Json("true")))
            .Should().Equal(("operador", "CondicaoDnf.OperadorInvalido"));

    [Fact(DisplayName = "A forma do valor só é conferida quando o operador foi lido")]
    public void CriarInformada_OperadorDesconhecidoEValorIncoerente_NaoConfereOValor() =>
        Recusas(CondicaoDnf.CriarInformada("FATO", "QUASE_IGUAL", Json("[1]")))
            .Should().Equal(("operador", "CondicaoDnf.OperadorInvalido"));

    [Fact(DisplayName = "O valor incoerente com o operador lido é recusado no campo do valor")]
    public void CriarInformada_ValorIncoerenteComOOperador_RecusaOValor() =>
        Recusas(CondicaoDnf.CriarInformada("FATO", "ALGUM_DE", Json("\"A\"")))
            .Should().Equal(("valor", "CondicaoDnf.FormaIncoerenteComOperador"));

    [Fact(DisplayName = "O fato em branco e o valor incoerente saem juntos quando o operador é conhecido")]
    public void CriarInformada_FatoEmBrancoEValorIncoerente_AcumulaAsDuasRecusas() =>
        Recusas(CondicaoDnf.CriarInformada("", "IGUAL", Json("[1]")))
            .Should().Equal(("fato", "CondicaoDnf.FatoObrigatorio"), ("valor", "CondicaoDnf.FormaIncoerenteComOperador"));

    [Theory(DisplayName = "O valor ausente é recusado como obrigatório no campo do valor, seja qual for o operador")]
    [InlineData("IGUAL")]
    [InlineData("MAIOR_IGUAL")]
    [InlineData("ALGUM_DE")]
    [InlineData("NENHUM_DE")]
    public void CriarInformada_ValorAusente_RecusaComoObrigatorio(string operador) =>
        Recusas(CondicaoDnf.CriarInformada("FATO", operador, default))
            .Should().Equal(("valor", CodigosDeValidacao.Obrigatorio));

    [Fact(DisplayName = "O valor ausente e o fato em branco saem juntos, cada um no seu campo")]
    public void CriarInformada_ValorAusenteEFatoEmBranco_AcumulaAsDuasRecusas() =>
        Recusas(CondicaoDnf.CriarInformada(" ", "IGUAL", default))
            .Should().Equal(("fato", "CondicaoDnf.FatoObrigatorio"), ("valor", CodigosDeValidacao.Obrigatorio));

    [Theory(DisplayName = "O valor nulo no JSON é recusado como obrigatório, seja qual for o operador")]
    [InlineData("IGUAL")]
    [InlineData("ALGUM_DE")]
    [InlineData("NENHUM_DE")]
    public void CriarInformada_ValorNuloNoJson_RecusaComoObrigatorio(string operador) =>
        Recusas(CondicaoDnf.CriarInformada("FATO", operador, Json("null")))
            .Should().Equal(("valor", CodigosDeValidacao.Obrigatorio));

    [Fact(DisplayName = "O valor ausente acumula com o operador desconhecido, porque a presença não depende do operador")]
    public void CriarInformada_ValorAusenteEOperadorDesconhecido_AcumulaAsDuasRecusas() =>
        Recusas(CondicaoDnf.CriarInformada("FATO", "QUASE_IGUAL", default))
            .Should().Equal(("operador", "CondicaoDnf.OperadorInvalido"), ("valor", CodigosDeValidacao.Obrigatorio));

    [Fact(DisplayName = "Criar recusa o valor nulo no JSON como obrigatório")]
    public void Criar_ValorNuloNoJson_RecusaComoObrigatorio() =>
        CondicaoDnf.Criar("FATO", Operador.Igual, Json("null")).Error!.Code.Should().Be(CodigosDeValidacao.Obrigatorio);

    [Fact(DisplayName = "Criar recusa o valor ausente como obrigatório")]
    public void Criar_ValorAusente_RecusaComoObrigatorio() =>
        CondicaoDnf.Criar("FATO", Operador.Igual, default).Error!.Code.Should().Be(CodigosDeValidacao.Obrigatorio);

    [Fact(DisplayName = "O array vazio de ALGUM_DE continua sendo valor declarado")]
    public void CriarInformada_ArrayVazio_NaoEAusente() =>
        CondicaoDnf.CriarInformada("FATO", "ALGUM_DE", Json("[]")).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "A mensagem do operador desconhecido lista os códigos aceitos e não repete o valor recebido")]
    public void CriarInformada_OperadorDesconhecido_ListaOsAceitosENaoRepeteOValor()
    {
        Result<CondicaoDnf> resultado = CondicaoDnf.CriarInformada("FATO", "QUASE_IGUAL", Json("true"));

        string mensagem = resultado.Error!.Message;
        mensagem.Should().Contain("IGUAL, ALGUM_DE, MAIOR_IGUAL, MENOR_IGUAL, DIFERENTE, NENHUM_DE");
        mensagem.Should().NotContain("QUASE_IGUAL");
    }

    [Theory(DisplayName = "Criar com operador fora do enum lança, em vez de recusar o pedido")]
    [InlineData(0)]
    [InlineData(99)]
    public void Criar_OperadorForaDoEnum_Lanca(int valor)
    {
        Func<Result<CondicaoDnf>> criar = () => CondicaoDnf.Criar("FATO", (Operador)valor, Json("true"));

        criar.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("operador");
    }
}
