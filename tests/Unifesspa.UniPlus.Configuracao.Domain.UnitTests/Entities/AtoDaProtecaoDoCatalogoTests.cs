namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>O registro de um ato do circuito de aprovação da proteção de dados guarda só o que o ato admite (ADR-0153).</summary>
public sealed class AtoDaProtecaoDoCatalogoTests
{
    private static readonly DateTimeOffset Segunda = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly ProtecaoDeDados Protecao = new(
        ClassificacaoDeProtecao.Pessoal, "Aferir a renda familiar.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal);
    private static readonly AtorDoAto Betina = new("https://sso.unifesspa.edu.br/realms/unifesspa", "betina.souza", "Betina Souza");

    private static AtoDaProtecaoDoCatalogo Registrar(
        AtoDaProtecao ato, ClassificacaoDeProtecao? recomendada = null, string? justificativa = null, AtorDoAto? ator = null) =>
        AtoDaProtecaoDoCatalogo.Registrar(
            CadastroDoCatalogo.Fato, Guid.NewGuid(), "RENDA_FAMILIAR_BRUTA", "Renda familiar bruta", ato, Protecao, recomendada, justificativa, ator ?? Betina, Segunda);

    [Fact(DisplayName = "O ato guarda o retrato do item, a proteção e quem agiu")]
    public void Registrar_GuardaORetrato()
    {
        AtoDaProtecaoDoCatalogo ato = Registrar(AtoDaProtecao.Reprovacao, justificativa: "  Dado desnecessário.  ");

        ato.Should().BeEquivalentTo(new
        {
            Cadastro = CadastroDoCatalogo.Fato,
            ItemCodigo = "RENDA_FAMILIAR_BRUTA",
            ItemNome = "Renda familiar bruta",
            Ato = AtoDaProtecao.Reprovacao,
            ClassificacaoProtecao = ClassificacaoDeProtecao.Pessoal,
            HipoteseLegal = HipoteseLegalTratamento.CumprimentoObrigacaoLegal,
            FinalidadeTratamento = "Aferir a renda familiar.",
            Justificativa = "Dado desnecessário.",
            AtorEmissor = Betina.Emissor,
            AtorSub = "betina.souza",
            AtorNome = "Betina Souza",
            Instante = Segunda,
        });
        ato.Id.Should().NotBeEmpty();
    }

    [Theory(DisplayName = "A classificação recomendada só acompanha o ajuste; a justificativa só o ajuste e a reprovação")]
    [InlineData(AtoDaProtecao.Solicitacao, false, false)]
    [InlineData(AtoDaProtecao.Aprovacao, false, false)]
    [InlineData(AtoDaProtecao.AjusteRecomendado, true, true)]
    [InlineData(AtoDaProtecao.Reprovacao, false, true)]
    [InlineData(AtoDaProtecao.Aceite, false, false)]
    public void Registrar_SoGuardaOQueOAtoAdmite(AtoDaProtecao tipo, bool guardaRecomendada, bool guardaJustificativa)
    {
        AtoDaProtecaoDoCatalogo ato = Registrar(tipo, ClassificacaoDeProtecao.Sensivel, "Motivo.");

        (ato.ClassificacaoRecomendada is not null).Should().Be(guardaRecomendada);
        (ato.Justificativa is not null).Should().Be(guardaJustificativa);
    }

    [Fact(DisplayName = "A justificativa em branco do ajuste não é guardada")]
    public void Registrar_JustificativaEmBranco_NaoEGuardada() =>
        Registrar(AtoDaProtecao.AjusteRecomendado, ClassificacaoDeProtecao.Sensivel, "   ").Justificativa.Should().BeNull();

    [Theory(DisplayName = "O nome de quem agiu é aparado; vazio ou com caractere nulo não é guardado; o longo é cortado")]
    [InlineData("  Betina Souza  ", "Betina Souza")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("Betina\0Souza", null)]
    public void Registrar_NormalizaONome(string? nome, string? esperado) =>
        Registrar(AtoDaProtecao.Aprovacao, ator: Betina with { Nome = nome }).AtorNome.Should().Be(esperado);

    [Fact(DisplayName = "O nome acima de 255 caracteres perde o excedente em vez de impedir o ato")]
    public void Registrar_NomeLongo_PerdeOExcedente() =>
        Registrar(AtoDaProtecao.Aprovacao, ator: Betina with { Nome = new string('a', 300) }).AtorNome.Should().HaveLength(AtorDoAto.NomeMaxLength);

    [Fact(DisplayName = "Os atos têm token canônico e as decisões são três")]
    public void Tokens_SaoCanonicos()
    {
        AtosDaProtecao.TokensCanonicos.Should().Equal("SOLICITACAO", "APROVACAO", "AJUSTE_RECOMENDADO", "REPROVACAO", "ACEITE");
        AtosDaProtecao.Decisoes.Should().Equal(AtoDaProtecao.Aprovacao, AtoDaProtecao.AjusteRecomendado, AtoDaProtecao.Reprovacao);
        AtosDaProtecao.TryAnalisar("REPROVACAO", out AtoDaProtecao reprovacao).Should().BeTrue();
        reprovacao.Should().Be(AtoDaProtecao.Reprovacao);
        AtosDaProtecao.TryAnalisar("reprovacao", out _).Should().BeFalse();
        CadastrosDoCatalogo.TokensCanonicos.Should().Equal("TIPO_DOCUMENTO", "FATO");
        CadastrosDoCatalogo.TryAnalisar(" FATO ", out CadastroDoCatalogo fato).Should().BeTrue();
        fato.Should().Be(CadastroDoCatalogo.Fato);
    }

    [Fact(DisplayName = "O token fora do vocabulário falha alto na reidratação")]
    public void Analisar_TokenForaDoVocabulario_Falha()
    {
        Action ato = () => AtosDaProtecao.Analisar("TALVEZ");
        Action cadastro = () => CadastrosDoCatalogo.Analisar("MODALIDADE");

        ato.Should().Throw<ArgumentOutOfRangeException>();
        cadastro.Should().Throw<ArgumentOutOfRangeException>();
    }
}
