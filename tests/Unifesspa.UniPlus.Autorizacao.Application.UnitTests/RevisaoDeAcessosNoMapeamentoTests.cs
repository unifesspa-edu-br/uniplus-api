namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>
/// Como a revisão periódica dos acessos sai na resposta: situação da revisão, situação do item, governança que decide e
/// escopo da concessão, cada um com o código e o rótulo. Leandro Viana é o beneficiário; Marlene Martins conduz a revisão.
/// </summary>
public sealed class RevisaoDeAcessosNoMapeamentoTests
{
    private static readonly DateTimeOffset Abertura = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Identidade Leandro = new(Emissor, "leandro.viana", "Leandro Viana");
    private static readonly Identidade Marlene = new(Emissor, "marlene.martins", "Marlene Martins");

    [Fact(DisplayName = "O item sem decisão sobre concessão ativa sai como pendente")]
    public void ToDto_ItemSemDecisaoComConcessaoAtiva_SaiPendente()
    {
        ItemDaRevisaoDto item = ItemDe(ComUmItem(concessaoAtiva: true));

        item.Situacao.EmPar().Should().Be(("PENDENTE", "Pendente"));
    }

    [Fact(DisplayName = "O item sem decisão sobre concessão que terminou fora da revisão sai como concessão encerrada")]
    public void ToDto_ItemSemDecisaoComConcessaoInativa_SaiConcessaoEncerrada()
    {
        ItemDaRevisaoDto item = ItemDe(ComUmItem(concessaoAtiva: false));

        item.Situacao.EmPar().Should().Be(("CONCESSAO_ENCERRADA", "Concessão encerrada"));
    }

    [Fact(DisplayName = "O item confirmado sai como confirmado")]
    public void ToDto_ItemConfirmado_SaiConfirmado()
    {
        RevisaoDetalhada detalhada = ComUmItem(concessaoAtiva: true);
        detalhada.Revisao.Confirmar(detalhada.Itens[0].Item.Id, detalhada.Itens[0].Concessao, new DecisorDoItem(Marlene, false), Abertura.AddHours(1))
            .IsSuccess.Should().BeTrue();

        ItemDe(detalhada).Situacao.EmPar().Should().Be(("CONFIRMADA", "Confirmada"));
    }

    [Fact(DisplayName = "O item revogado sai como revogado")]
    public void ToDto_ItemRevogado_SaiRevogado()
    {
        RevisaoDetalhada detalhada = ComUmItem(concessaoAtiva: true);
        detalhada.Revisao.Revogar(detalhada.Itens[0].Item.Id, detalhada.Itens[0].Concessao, new DecisorDoItem(Marlene, false), "Acesso sem uso.", Abertura.AddHours(1))
            .IsSuccess.Should().BeTrue();

        ItemDe(detalhada).Situacao.EmPar().Should().Be(("REVOGADA", "Revogada"));
    }

    [Fact(DisplayName = "Quem decide o item de beneficiário comum é o Representante do controlador")]
    public void ToDto_BeneficiarioComum_DecideORepresentante()
    {
        ItemDaRevisaoDto item = ItemDe(ComUmItem(concessaoAtiva: true));

        item.Decide.EmPar().Should().Be(("REPRESENTANTE_DO_CONTROLADOR", "Representante do controlador"));
    }

    [Fact(DisplayName = "Quem decide o item do próprio Representante do controlador é a gestão de acessos")]
    public void ToDto_BeneficiarioRepresentante_DecideOGestorDeAcessos()
    {
        RevisaoDetalhada detalhada = ComUmItem(concessaoAtiva: true);
        HashSet<Identidade> representantes = [Leandro];

        ItemDaRevisaoDto item = RevisaoMapping.ToDto(detalhada, representantes, null).Itens.Single();

        item.Decide.EmPar().Should().Be(("GESTOR_DE_ACESSOS", "Gestor de acessos"));
    }

    [Fact(DisplayName = "O escopo da concessão do item sai com o código e o rótulo")]
    public void ToDto_ConcessaoDaInstituicao_EscopoSaiComCodigoERotulo()
    {
        ItemDaRevisaoDto item = ItemDe(ComUmItem(concessaoAtiva: true));

        item.Escopo.Tipo.EmPar().Should().Be(("INSTITUICAO", "Instituição"));
    }

    [Fact(DisplayName = "A revisão com item pendente sobre concessão ativa está em andamento")]
    public void ToDto_ComPendencia_EstaEmAndamento()
    {
        RevisaoDeAcessosDto dto = RevisaoMapping.ToDto(ComUmItem(concessaoAtiva: true), new HashSet<Identidade>(), null);

        dto.Situacao.EmPar().Should().Be(("EM_ANDAMENTO", "Em andamento"));
    }

    [Fact(DisplayName = "A revisão sem pendência sobre concessão ativa está concluída")]
    public void ToDto_SemPendencia_EstaConcluida()
    {
        RevisaoDeAcessosDto dto = RevisaoMapping.ToDto(ComUmItem(concessaoAtiva: false), new HashSet<Identidade>(), null);

        dto.Situacao.EmPar().Should().Be(("CONCLUIDA", "Concluída"));
    }

    [Theory(DisplayName = "A listagem resume a situação pela contagem de pendentes")]
    [InlineData(1, "EM_ANDAMENTO", "Em andamento")]
    [InlineData(0, "CONCLUIDA", "Concluída")]
    public void ToResumo_ConformeOsPendentes_SaiASituacao(int pendentes, string codigo, string rotulo)
    {
        RevisaoDeAcessos revisao = ComUmItem(concessaoAtiva: true).Revisao;

        RevisaoResumoDto resumo = RevisaoMapping.ToResumo(new ResumoDaRevisao(revisao, 1, pendentes));

        resumo.Situacao.EmPar().Should().Be((codigo, rotulo));
    }

    private static ItemDaRevisaoDto ItemDe(RevisaoDetalhada detalhada) =>
        RevisaoMapping.ToDto(detalhada, new HashSet<Identidade>(), null).Itens.Single();

    private static RevisaoDetalhada ComUmItem(bool concessaoAtiva)
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["geral:pedidos:consultar"], new CatalogoInstitucional());
        Concessao concessao = ConcessaoBuilder.DoPerfil(perfil, new CatalogoInstitucional())
            .Para(Leandro, "leandro-viana84@uniplus-test.local").NaInstituicao().ConcedidaPor(Marlene).Em(Abertura).Construir();
        RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(Marlene, [concessao.Id], Abertura);

        return new RevisaoDetalhada(revisao, [new ItemDaRevisaoComConcessao(revisao.Itens[0], concessao, NomeDoPerfil.Reidratar(perfil.Nome), concessaoAtiva)]);
    }
}
