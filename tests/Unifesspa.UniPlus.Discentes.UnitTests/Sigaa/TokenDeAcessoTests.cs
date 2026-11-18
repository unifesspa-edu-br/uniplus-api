namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Authentication;

/// <summary>
/// O token é um segredo opaco: só se recusa o que a origem não entregou, nada o expõe em
/// texto, e os instantes que ele declara são lidos do trecho do meio, sem verificar assinatura.
/// </summary>
public sealed class TokenDeAcessoTests
{
    private const long Expiracao = 1_800_000_600;
    private const long Emissao = 1_800_000_000;

    private static readonly string Corpo = RespostasDoSigaa.CodificarCorpo($$"""{"exp":{{Expiracao}},"iat":{{Emissao}}}""");

    public static readonly TheoryData<string> SemInstantesLegiveis = new()
    {
        "sem-nenhum-ponto",
        "cabecalho..assinatura",
        "cabecalho.corpo*invalido.assinatura",
        "cabecalho.bmFvIGVoIGpzb24.assinatura",
        RespostasDoSigaa.TokenComCorpo("{}"),
        RespostasDoSigaa.TokenComCorpo("""{"iat":1800000000}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":99999999999999999999}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":1.5}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":253402300800}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":-62135596801}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":1800000600,"iat":253402300800}"""),
        $"cabecalho.{RespostasDoSigaa.CodificarCorpo("{}")}",
    };

    public static readonly TheoryData<string> ComTipoJsonErrado = new()
    {
        RespostasDoSigaa.TokenComCorpo("""{"exp":"abc"}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":null}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":{}}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":true}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":1800000600,"iat":"abc"}"""),
        RespostasDoSigaa.TokenComCorpo("""{"exp":1800000600,"iat":null}"""),
        RespostasDoSigaa.TokenComCorpo("[1]"),
        RespostasDoSigaa.TokenComCorpo("5"),
        RespostasDoSigaa.TokenComCorpo("\"texto\""),
        RespostasDoSigaa.TokenComCorpo("null"),
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Recusa_a_resposta_sem_token(string? texto)
    {
        TokenDeAcesso.Criar(texto).Error!.Code.Should().Be("TokenDeAcesso.Vazio");
    }

    [Theory]
    [InlineData(" abc ")]
    [InlineData("qualquer coisa, nem JWT precisa ser")]
    public void Aceita_qualquer_texto_nao_vazio_e_o_guarda_como_veio(string texto)
    {
        TokenDeAcesso.Criar(texto).Value!.Segredo.Should().Be(texto);
    }

    [Fact]
    public void Nenhuma_representacao_em_texto_expoe_o_segredo()
    {
        TokenDeAcesso token = Token("segredo-que-nao-pode-vazar");

        $"{token}".Should().NotContain("segredo");
        token.ToString().Should().NotContain("segredo");
        token.Should().Be(Token("segredo-que-nao-pode-vazar"));
    }

    [Theory]
    [MemberData(nameof(SemInstantesLegiveis))]
    public void Instantes_ilegiveis_ou_fora_da_faixa_nao_sao_declarados(string texto)
    {
        bool lido = Token(texto).TentarLerInstantesDeclarados(out DateTimeOffset expiraEm, out DateTimeOffset? emitidoEm);

        lido.Should().BeFalse();
        expiraEm.Should().Be(default);
        emitidoEm.Should().BeNull();
    }

    [Fact]
    public void Le_a_expiracao_e_a_emissao_do_trecho_do_meio()
    {
        bool lido = Token($"cabecalho.{Corpo}.assinatura").TentarLerInstantesDeclarados(
            out DateTimeOffset expiraEm, out DateTimeOffset? emitidoEm);

        lido.Should().BeTrue();
        expiraEm.Should().Be(DateTimeOffset.FromUnixTimeSeconds(Expiracao));
        emitidoEm.Should().Be(DateTimeOffset.FromUnixTimeSeconds(Emissao));
    }

    [Theory]
    [InlineData("cabecalho.", "")]
    [InlineData("cabecalho.", ".assinatura")]
    [InlineData("cabecalho.", ".assinatura.sobra.mais")]
    public void Com_um_ponto_o_corpo_e_o_resto_do_texto_e_com_dois_ou_mais_e_o_trecho_do_meio(string antes, string depois)
    {
        bool lido = Token($"{antes}{Corpo}{depois}").TentarLerInstantesDeclarados(out DateTimeOffset expiraEm, out _);

        lido.Should().BeTrue();
        expiraEm.Should().Be(DateTimeOffset.FromUnixTimeSeconds(Expiracao));
    }

    [Theory]
    [InlineData("""{"exp":1800000600}""")]
    [InlineData("""{"exp":1800000600,"iat":99999999999999999999}""")]
    [InlineData("""{"exp":1800000600,"iat":1.5}""")]
    public void Emissao_ausente_ou_que_nao_cabe_em_64_bits_deixa_so_a_expiracao(string corpoJson)
    {
        bool lido = Token(RespostasDoSigaa.TokenComCorpo(corpoJson))
            .TentarLerInstantesDeclarados(out DateTimeOffset expiraEm, out DateTimeOffset? emitidoEm);

        lido.Should().BeTrue();
        expiraEm.Should().Be(DateTimeOffset.FromUnixTimeSeconds(Expiracao));
        emitidoEm.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(ComTipoJsonErrado))]
    public void Tipo_json_errado_ou_corpo_que_nao_e_objeto_continua_lancando(string texto)
    {
        Func<bool> ler = () => Token(texto).TentarLerInstantesDeclarados(out _, out _);

        ler.Should().ThrowExactly<InvalidOperationException>();
    }

    private static TokenDeAcesso Token(string texto) => TokenDeAcesso.Criar(texto).Value!;
}
