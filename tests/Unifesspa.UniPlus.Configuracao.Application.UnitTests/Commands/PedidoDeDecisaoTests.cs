namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>A forma da decisão, conferida antes de qualquer leitura do item, com as recusas num único lote (ADR-0125).</summary>
public sealed class PedidoDeDecisaoTests
{
    [Fact(DisplayName = "A decisão bem formada sai com o item, o ato, os fundamentos e a proteção conferida")]
    public void Ler_DecisaoBemFormada()
    {
        Guid id = Guid.CreateVersion7();

        PedidoDeDecisao pedido = PedidoDeDecisao.Ler(Comando("RECOMENDAR_AJUSTE", id, " FATO ", "SENSIVEL", "  Traz dado de saúde.  ")).Value!;

        (pedido.Item.Cadastro, pedido.Item.ItemId).Should().Be((CadastroDoCatalogo.Fato, id));
        (pedido.Decisao.Ato, pedido.Decisao.ClassificacaoRecomendada, pedido.Decisao.Justificativa!.Valor)
            .Should().Be((AtoDaProtecao.AjusteRecomendado, ClassificacaoDeProtecao.Sensivel, "Traz dado de saúde."));
        pedido.ProtecaoConferida.Should().Be(new Domain.ValueObjects.ProtecaoDeDados(
            ClassificacaoDeProtecao.Interno, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal));
    }

    [Fact(DisplayName = "A aprovação aceita justificativa, que segue para o ato")]
    public void Ler_AprovacaoComJustificativa_Aceita() =>
        PedidoDeDecisao.Ler(Comando("APROVAR", justificativa: "A classificação interna basta.")).Value!
            .Decisao.Justificativa!.Valor.Should().Be("A classificação interna basta.");

    [Theory(DisplayName = "A recomendação que não cabe, ou a reprovação sem justificativa, é recusada no campo")]
    [InlineData("APROVAR", "SENSIVEL", null, "classificacaoRecomendada", "DadoDaDecisaoNaoCabe")]
    [InlineData("REPROVAR", "SENSIVEL", "Motivo.", "classificacaoRecomendada", "DadoDaDecisaoNaoCabe")]
    [InlineData("REPROVAR", null, null, "justificativa", "JustificativaObrigatoria")]
    [InlineData("REPROVAR", null, "   ", "justificativa", "JustificativaObrigatoria")]
    [InlineData("RECOMENDAR_AJUSTE", null, null, "classificacaoRecomendada", "RecomendacaoObrigatoria")]
    [InlineData("RECOMENDAR_AJUSTE", "SECRETISSIMO", null, "classificacaoRecomendada", "RecomendacaoObrigatoria")]
    public void Ler_DadoDaDecisaoInvalido_RecusaNoCampo(string decisao, string? recomendada, string? justificativa, string campo, string codigo) =>
        PedidoDeDecisao.Ler(Comando(decisao, recomendada: recomendada, justificativa: justificativa)).Errors
            .Should().ContainSingle(e => e.Field == campo).Which.Error.Code.Should().Be($"ProtecaoDoCatalogo.{codigo}");

    [Fact(DisplayName = "O ajuste sem a classificação recomendada sai com uma recusa só no lote, a da regra do domínio")]
    public void Ler_AjusteSemRecomendacao_UmaRecusaSo() =>
        PedidoDeDecisao.Ler(Comando("RECOMENDAR_AJUSTE")).Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "classificacaoRecomendada" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria);

    [Theory(DisplayName = "A justificativa de até 2.000 caracteres é aceita; com mais, recusada")]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Ler_LimiteDaJustificativa(int tamanho, bool aceita)
    {
        Result<PedidoDeDecisao> pedido = PedidoDeDecisao.Ler(Comando("APROVAR", justificativa: new string('a', tamanho)));

        pedido.IsSuccess.Should().Be(aceita);
        pedido.Errors.Select(static e => e.Error.Code).Should().Equal(aceita ? [] : new[] { ProtecaoDoCatalogoErrorCodes.JustificativaMuitoLonga });
    }

    [Fact(DisplayName = "O cadastro, a decisão, a proteção conferida e a leitura do circuito inválidos saem juntos, cada um no seu campo")]
    public void Ler_FormaInvalida_AcumulaRecusas()
    {
        Result<PedidoDeDecisao> pedido = PedidoDeDecisao.Ler(
            new DecidirProtecaoDoCatalogoCommand(
                new ItemInformado("MODALIDADE", Guid.NewGuid()), new DecisaoInformada(null), new ProtecaoInformada("SECRETISSIMO", "NAO_EXISTE", " "), new LeituraInformada(0, -1)));

        pedido.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("cadastro", ProtecaoDoCatalogoErrorCodes.CadastroInvalido),
            ("decisao", ProtecaoDoCatalogoErrorCodes.DecisaoInvalida),
            ("classificacaoProtecao", ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida),
            ("hipoteseLegal", ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida),
            ("finalidadeTratamento", ProtecaoDoCatalogoErrorCodes.ProtecaoConferidaInvalida),
            ("ciclo", ProtecaoDoCatalogoErrorCodes.CicloInvalido),
            ("manifestacoesNoCiclo", ProtecaoDoCatalogoErrorCodes.ManifestacoesNoCicloInvalida),
        ]);
    }

    [Fact(DisplayName = "A decisão leva o ciclo e a contagem de manifestações que o Representante do controlador leu")]
    public void Ler_LevaALeituraDoCircuito() =>
        PedidoDeDecisao.Ler(Comando("APROVAR") with { Lida = new LeituraInformada(2, 3) }).Value!.Lida
            .Should().Be(new LeituraDoCircuito(new CicloDaAprovacao(2), 3));

    [Fact(DisplayName = "Sem o ciclo e a contagem de manifestações lidos, a decisão é recusada nos dois campos")]
    public void Ler_SemLeituraDoCircuito_Recusa() =>
        PedidoDeDecisao.Ler(Comando("APROVAR") with { Lida = new LeituraInformada(null, null) }).Errors.Select(static e => e.Field)
            .Should().BeEquivalentTo("ciclo", "manifestacoesNoCiclo");

    private static DecidirProtecaoDoCatalogoCommand Comando(
        string decisao, Guid? id = null, string cadastro = "TIPO_DOCUMENTO", string? recomendada = null, string? justificativa = null) =>
        new(
            new ItemInformado(cadastro, id ?? Guid.CreateVersion7()),
            new DecisaoInformada(decisao, recomendada, justificativa),
            new ProtecaoInformada("INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade),
            new LeituraInformada(1, 0));
}
