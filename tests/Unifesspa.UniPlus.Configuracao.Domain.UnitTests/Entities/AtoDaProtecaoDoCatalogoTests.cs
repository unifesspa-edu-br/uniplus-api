namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O registro de um ato do circuito de aprovação da proteção de dados guarda o retrato do item e só o que o ato admite
/// (ADR-0153); a decisão guarda também a justificativa, o ciclo e se havia manifestação desfavorável vigente (ADR-0163).
/// </summary>
public sealed class AtoDaProtecaoDoCatalogoTests
{
    private static readonly DateTimeOffset Segunda = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly AtorDoAto Betina = new("https://sso.unifesspa.edu.br/realms/unifesspa", "betina.neves", "Betina Marina Valentina das Neves");
    private static readonly Fundamentacao Motivo = Fundamentacao.Analisar("Dado desnecessário.");

    [Fact(DisplayName = "A solicitação guarda o retrato do item: identidade, código, nome e ciclo")]
    public void Registrar_GuardaORetratoDoItem()
    {
        RetratoDoItemDaProtecao item = Retrato(ciclo: 2);

        AtoDaProtecaoDoCatalogo ato = AtoDaProtecaoDoCatalogo.Registrar(item, AtoDaProtecao.Solicitacao, Betina, Segunda);

        (ato.Cadastro, ato.ItemId, ato.ItemCodigo, ato.ItemNome, ato.Ciclo).Should().Be(
            (CadastroDoCatalogo.Fato, item.Item.ItemId, "RENDA_FAMILIAR_BRUTA", "Renda familiar bruta", new CicloDaAprovacao(2)));
    }

    [Fact(DisplayName = "A solicitação guarda a proteção que o item tinha no instante do ato")]
    public void Registrar_GuardaAProtecao()
    {
        AtoDaProtecaoDoCatalogo ato = AtoDaProtecaoDoCatalogo.Registrar(Retrato(), AtoDaProtecao.Solicitacao, Betina, Segunda);

        (ato.ClassificacaoProtecao, ato.HipoteseLegal, ato.FinalidadeTratamento).Should().Be(
            (ClassificacaoDeProtecao.Pessoal, HipoteseLegalTratamento.CumprimentoObrigacaoLegal, "Aferir a renda familiar."));
    }

    [Fact(DisplayName = "A solicitação guarda quem agiu e quando, sem os fundamentos da decisão")]
    public void Registrar_GuardaQuemAgiuSemFundamentos()
    {
        AtoDaProtecaoDoCatalogo ato = AtoDaProtecaoDoCatalogo.Registrar(Retrato(), AtoDaProtecao.Solicitacao, Betina, Segunda);

        (ato.Ato, ato.AtorEmissor, ato.AtorSub, ato.AtorNome, ato.Instante).Should().Be(
            (AtoDaProtecao.Solicitacao, Betina.Emissor, "betina.neves", "Betina Marina Valentina das Neves", Segunda));
        (ato.ClassificacaoRecomendada, ato.Justificativa, ato.DianteDeManifestacaoDesfavoravel).Should().Be(((ClassificacaoDeProtecao?)null, (string?)null, false));
    }

    [Theory(DisplayName = "A decisão não se registra como ato sem fundamentos")]
    [InlineData(AtoDaProtecao.Aprovacao)]
    [InlineData(AtoDaProtecao.AjusteRecomendado)]
    [InlineData(AtoDaProtecao.Reprovacao)]
    [InlineData(AtoDaProtecao.Nenhum)]
    public void Registrar_Decisao_Falha(AtoDaProtecao ato) =>
        new Action(() => AtoDaProtecaoDoCatalogo.Registrar(Retrato(), ato, Betina, Segunda)).Should().Throw<ArgumentException>();

    [Theory(DisplayName = "A classificação recomendada só acompanha o ajuste; a justificativa acompanha as três decisões, inclusive a aprovação")]
    [InlineData(AtoDaProtecao.Aprovacao, false)]
    [InlineData(AtoDaProtecao.AjusteRecomendado, true)]
    [InlineData(AtoDaProtecao.Reprovacao, false)]
    public void RegistrarDecisao_GuardaOsFundamentos(AtoDaProtecao tipo, bool guardaRecomendada)
    {
        AtoDaProtecaoDoCatalogo ato = AtoDaProtecaoDoCatalogo.RegistrarDecisao(
            Retrato(), new DecisaoFundamentada(tipo, ClassificacaoDeProtecao.Sensivel, Motivo), Betina, Segunda);

        (ato.ClassificacaoRecomendada is not null).Should().Be(guardaRecomendada);
        ato.Justificativa.Should().Be("Dado desnecessário.");
        ato.DianteDeManifestacaoDesfavoravel.Should().BeFalse();
    }

    [Fact(DisplayName = "A aprovação diante de manifestação desfavorável guarda a justificativa, o ciclo e o indicador")]
    public void RegistrarDecisao_DianteDeDesfavoravel_GuardaOIndicador()
    {
        AtoDaProtecaoDoCatalogo ato = AtoDaProtecaoDoCatalogo.RegistrarDecisao(
            Retrato(ciclo: 3, desfavoravel: true), new DecisaoFundamentada(AtoDaProtecao.Aprovacao, null, Motivo), Betina, Segunda);

        (ato.Ciclo, ato.Justificativa, ato.DianteDeManifestacaoDesfavoravel).Should().Be((new CicloDaAprovacao(3), "Dado desnecessário.", true));
    }

    [Theory(DisplayName = "Aprovar ou recomendar ajuste diante de manifestação desfavorável sem justificativa falha alto")]
    [InlineData(AtoDaProtecao.Aprovacao)]
    [InlineData(AtoDaProtecao.AjusteRecomendado)]
    public void RegistrarDecisao_DianteDeDesfavoravelSemJustificativa_Falha(AtoDaProtecao tipo) =>
        new Action(() => AtoDaProtecaoDoCatalogo.RegistrarDecisao(
                Retrato(desfavoravel: true), new DecisaoFundamentada(tipo, ClassificacaoDeProtecao.Sensivel, null), Betina, Segunda))
            .Should().Throw<InvalidOperationException>();

    [Theory(DisplayName = "O nome de quem agiu é aparado; vazio ou com caractere nulo não é guardado; o longo é cortado")]
    [InlineData("  Betina Marina  ", "Betina Marina")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("Betina\0Neves", null)]
    public void Registrar_NormalizaONome(string? nome, string? esperado) =>
        AtoDaProtecaoDoCatalogo.Registrar(Retrato(), AtoDaProtecao.Aceite, Betina with { Nome = nome }, Segunda).AtorNome.Should().Be(esperado);

    [Fact(DisplayName = "O nome acima de 255 caracteres perde o excedente em vez de impedir o ato")]
    public void Registrar_NomeLongo_PerdeOExcedente() =>
        AtoDaProtecaoDoCatalogo.Registrar(Retrato(), AtoDaProtecao.Aceite, Betina with { Nome = new string('a', 300) }, Segunda)
            .AtorNome.Should().HaveLength(AtorDoAto.NomeMaxLength);

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

    private static RetratoDoItemDaProtecao Retrato(int ciclo = 1, bool desfavoravel = false) =>
        new(new ItemDaProtecao(CadastroDoCatalogo.Fato, Guid.CreateVersion7()),
            "RENDA_FAMILIAR_BRUTA",
            "Renda familiar bruta",
            new ProtecaoDeDados(ClassificacaoDeProtecao.Pessoal, "Aferir a renda familiar.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal),
            new CicloDaAprovacao(ciclo),
            desfavoravel);
}
