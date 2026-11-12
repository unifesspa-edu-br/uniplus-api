namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RevisaoDeAcessosTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string Motivo = "O atendimento do edital terminou.";

    private static readonly DateTimeOffset Agora = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly Identidade Marlene = new(Emissor, "marlene.martins");
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly Identidade Leandro = new(Emissor, "leandro.viana");
    private static readonly FakeCatalogo Catalogo = new();

    [Fact(DisplayName = "A abertura cria um item pendente por concessão, sem fixar quem decide")]
    public void Abrir_NaoFixaQuemDecide()
    {
        Guid repetida = Guid.NewGuid();

        RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(Marlene, [repetida, Guid.NewGuid(), repetida], Agora);

        revisao.Itens.Should().HaveCount(2);
        revisao.Itens.Should().OnlyContain(i => i.Pendente && i.DecididoComo == null);
    }

    [Theory(DisplayName = "O Representante decide os itens comuns, e os gestores, os de quem é Representante no instante da decisão")]
    [InlineData(false, TipoDeGovernanca.RepresentanteDoControlador)]
    [InlineData(true, TipoDeGovernanca.GestorDeAcessos)]
    public void QuemDecide_PelaCondicaoDoBeneficiario(bool beneficiarioERepresentante, TipoDeGovernanca esperado)
    {
        ItemDaRevisao.QuemDecide(beneficiarioERepresentante).Should().Be(esperado);
        new DecisorDoItem(Marlene, beneficiarioERepresentante).Como.Should().Be(esperado);
    }

    [Theory(DisplayName = "A decisão grava a governança com que foi tomada, pela condição do beneficiário naquele instante")]
    [InlineData(false, TipoDeGovernanca.RepresentanteDoControlador)]
    [InlineData(true, TipoDeGovernanca.GestorDeAcessos)]
    public void Decidir_GravaAGovernancaDaDecisao(bool beneficiarioERepresentante, TipoDeGovernanca esperado)
    {
        (RevisaoDeAcessos revisao, Concessao concessao) = RevisaoComUmItem();

        revisao.Confirmar(revisao.Itens[0].Id, concessao, new DecisorDoItem(Marlene, beneficiarioERepresentante), Agora);

        revisao.Itens[0].DecididoComo.Should().Be(esperado);
    }

    [Fact(DisplayName = "A designação concedida durante a revisão aberta passa o item aos gestores a partir dali")]
    public void Decidir_DesignacaoDuranteARevisao_MudaODecisor()
    {
        (RevisaoDeAcessos revisao, Concessao antes) = RevisaoComUmItem();
        Concessao depois = Concedida();
        RevisaoDeAcessos outra = RevisaoDeAcessos.Abrir(Marlene, [depois.Id], Agora);

        revisao.Confirmar(revisao.Itens[0].Id, antes, MarleneDecide(false), Agora);
        outra.Confirmar(outra.Itens[0].Id, depois, MarleneDecide(true), Agora);

        revisao.Itens[0].DecididoComo.Should().Be(TipoDeGovernanca.RepresentanteDoControlador);
        outra.Itens[0].DecididoComo.Should().Be(TipoDeGovernanca.GestorDeAcessos);
    }

    [Fact(DisplayName = "A revisão aberta sem nenhum item já nasce concluída")]
    public void Abrir_SemItens_NasceConcluida()
    {
        RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(Marlene, [], Agora);

        revisao.EstaEmAndamento(new HashSet<Guid>()).Should().BeFalse();
    }

    [Fact(DisplayName = "A revisão está em andamento enquanto há item sem decisão sobre concessão ativa")]
    public void EstaEmAndamento_DerivaDosItensEDaConcessao()
    {
        Concessao decidida = Concedida();
        Concessao encerrada = Concedida();
        Concessao pendente = Concedida();
        RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(
            Marlene,
            new[] { decidida, encerrada, pendente }.Select(c => c.Id),
            Agora);
        revisao.Confirmar(revisao.Itens[0].Id, decidida, MarleneDecide(false), Agora);

        revisao.EstaEmAndamento(new HashSet<Guid> { pendente.Id }).Should().BeTrue();
        revisao.EstaEmAndamento(new HashSet<Guid> { decidida.Id }).Should().BeFalse("o item decidido não conta");
        revisao.EstaEmAndamento(new HashSet<Guid>()).Should().BeFalse("a concessão encerrada fora da revisão não pede decisão");
    }

    [Fact(DisplayName = "Confirmar mantém a concessão ativa e grava quem decidiu")]
    public void Confirmar_MantemAConcessao()
    {
        (RevisaoDeAcessos revisao, Concessao concessao) = RevisaoComUmItem();

        Result resultado = revisao.Confirmar(revisao.Itens[0].Id, concessao, MarleneDecide(false), Agora);

        resultado.IsSuccess.Should().BeTrue();
        concessao.EstaAtiva(Agora).Should().BeTrue();
        revisao.Itens[0].Resultado.Should().Be(ResultadoDoItemDaRevisao.Confirmada);
        revisao.Itens[0].DecididoPor.Should().Be(Marlene);
        revisao.Itens[0].DecididoEm.Should().Be(Agora);
    }

    [Fact(DisplayName = "Revogar encerra a concessão com o motivo e registra a decisão no item")]
    public void Revogar_EncerraAConcessao()
    {
        (RevisaoDeAcessos revisao, Concessao concessao) = RevisaoComUmItem();

        Result resultado = revisao.Revogar(revisao.Itens[0].Id, concessao, MarleneDecide(false), Motivo, Agora);

        resultado.IsSuccess.Should().BeTrue();
        concessao.EstaAtiva(Agora).Should().BeFalse();
        concessao.MotivoRevogacao.Should().Be(Motivo);
        revisao.Itens[0].Resultado.Should().Be(ResultadoDoItemDaRevisao.Revogada);
    }

    [Theory(DisplayName = "Revogar sem motivo é recusado e deixa o item pendente e a concessão ativa")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Revogar_SemMotivo_Recusa(string? motivo)
    {
        (RevisaoDeAcessos revisao, Concessao concessao) = RevisaoComUmItem();

        Result resultado = revisao.Revogar(revisao.Itens[0].Id, concessao, MarleneDecide(false), motivo, Agora);

        CodigosDaRecusa(resultado).Should().Equal(ConcessaoErrorCodes.MotivoInvalido);
        revisao.Itens[0].Pendente.Should().BeTrue();
        concessao.EstaAtiva(Agora).Should().BeTrue();
    }

    [Fact(DisplayName = "Item já decidido não se decide de novo")]
    public void Decidir_ItemJaDecidido_Recusa()
    {
        (RevisaoDeAcessos revisao, Concessao concessao) = RevisaoComUmItem();
        revisao.Confirmar(revisao.Itens[0].Id, concessao, MarleneDecide(false), Agora);

        CodigosDaRecusa(revisao.Revogar(revisao.Itens[0].Id, concessao, MarleneDecide(false), Motivo, Agora))
            .Should().Equal(RevisaoErrorCodes.ItemJaDecidido);
        concessao.EstaAtiva(Agora).Should().BeTrue();
    }

    [Fact(DisplayName = "Concessão que terminou fora da revisão não aceita decisão")]
    public void Decidir_ConcessaoEncerrada_Recusa()
    {
        (RevisaoDeAcessos revisao, Concessao concessao) = RevisaoComUmItem();
        concessao.Revogar(Diogo, Motivo, Agora);

        CodigosDaRecusa(revisao.Confirmar(revisao.Itens[0].Id, concessao, MarleneDecide(false), Agora))
            .Should().Equal(RevisaoErrorCodes.ConcessaoNaoAtiva);
    }

    [Fact(DisplayName = "Ninguém decide o item da própria concessão")]
    public void Decidir_DecisorEOBeneficiario_Recusa()
    {
        (RevisaoDeAcessos revisao, Concessao concessao) = RevisaoComUmItem();

        CodigosDaRecusa(revisao.Confirmar(revisao.Itens[0].Id, concessao, new DecisorDoItem(Leandro, false), Agora))
            .Should().Equal(RevisaoErrorCodes.DecisorEBeneficiario);
    }

    private static (RevisaoDeAcessos Revisao, Concessao Concessao) RevisaoComUmItem()
    {
        Concessao concessao = Concedida();
        return (RevisaoDeAcessos.Abrir(Marlene, [concessao.Id], Agora), concessao);
    }

    private static DecisorDoItem MarleneDecide(bool beneficiarioERepresentante) => new(Marlene, beneficiarioERepresentante);

    private static Concessao Concedida() =>
        Concessao.Conceder(
            Leandro,
            PerfisDeTeste.Vigente("Análise de atendimento", "Descrição válida.", ["selecao:processos:consultar"], Catalogo),
            EscopoDaConcessao.DaUnidade(Guid.NewGuid()),
            new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
            Diogo,
            Agora.AddDays(-30),
            Catalogo).Value!;

    private static string[] CodigosDaRecusa(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private sealed class FakeCatalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("selecao:processos:", StringComparison.Ordinal);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => ClassificacaoDeProtecao.Interno;
    }
}
