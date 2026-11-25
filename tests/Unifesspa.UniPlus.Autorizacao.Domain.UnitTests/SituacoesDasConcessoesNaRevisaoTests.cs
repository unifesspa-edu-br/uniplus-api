namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class SituacoesDasConcessoesNaRevisaoTests
{
    [Fact]
    public void Codigo_DasSituacoesDaConcessao_EODeclarado() =>
        Enum.GetValues<SituacaoDaConcessaoNaRevisao>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDaConcessaoNaRevisao, string>
            {
                [SituacaoDaConcessaoNaRevisao.AguardandoDecisoes] = "AGUARDANDO_DECISOES",
                [SituacaoDaConcessaoNaRevisao.Retida] = "RETIDA",
                [SituacaoDaConcessaoNaRevisao.AguardandoProtecaoDeDados] = "AGUARDANDO_PROTECAO_DE_DADOS",
                [SituacaoDaConcessaoNaRevisao.Migrada] = "MIGRADA",
                [SituacaoDaConcessaoNaRevisao.Recusada] = "RECUSADA",
                [SituacaoDaConcessaoNaRevisao.NaoMigrada] = "NAO_MIGRADA",
                [SituacaoDaConcessaoNaRevisao.ForaDaLista] = "FORA_DA_LISTA",
            });

    [Fact]
    public void Codigo_DasSituacoesDaRevisao_EODeclarado() =>
        Enum.GetValues<SituacaoDaRevisaoDasConcessoes>().ToDictionary(static valor => valor, static valor => valor.Codigo())
            .Should().Equal(new Dictionary<SituacaoDaRevisaoDasConcessoes, string>
            {
                [SituacaoDaRevisaoDasConcessoes.Aberta] = "ABERTA",
                [SituacaoDaRevisaoDasConcessoes.Encerrando] = "ENCERRANDO",
                [SituacaoDaRevisaoDasConcessoes.Encerrada] = "ENCERRADA",
            });

    [Theory]
    [InlineData(SituacaoDaMigracao.Retida, SituacaoDaConcessaoNaRevisao.Retida)]
    [InlineData(SituacaoDaMigracao.AguardandoProtecaoDeDados, SituacaoDaConcessaoNaRevisao.AguardandoProtecaoDeDados)]
    [InlineData(SituacaoDaMigracao.Migrada, SituacaoDaConcessaoNaRevisao.Migrada)]
    [InlineData(SituacaoDaMigracao.Recusada, SituacaoDaConcessaoNaRevisao.Recusada)]
    [InlineData(SituacaoDaMigracao.NaoMigrada, SituacaoDaConcessaoNaRevisao.NaoMigrada)]
    [InlineData(SituacaoDaMigracao.ForaDaLista, SituacaoDaConcessaoNaRevisao.ForaDaLista)]
    public void SituacaoDe_ComLinha_DevolveSituacaoCorrespondente(SituacaoDaMigracao situacao, SituacaoDaConcessaoNaRevisao esperada)
    {
        RevisaoDasConcessoesDoPerfil revisao = new CenarioDeMigracao(protegido: false).Abrir();
        Guid concessaoId = Guid.NewGuid();
        revisao.Registrar(concessaoId, situacao, false, CenarioDeMigracao.Abertura);

        revisao.SituacaoDe(concessaoId).Should().Be(esperada);
    }

    [Fact]
    public void SituacaoDe_SemLinha_AguardaDecisoes() =>
        new CenarioDeMigracao(protegido: false).Abrir().SituacaoDe(Guid.NewGuid()).Should().Be(SituacaoDaConcessaoNaRevisao.AguardandoDecisoes);

    [Fact]
    public void SituacaoNaRevisao_ValorDesconhecido_LancaExcecao()
    {
        MigracaoDaConcessao linha = MigracaoDaConcessao.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (SituacaoDaMigracao)999, false, CenarioDeMigracao.Abertura);

        Action consultar = () => _ = linha.SituacaoNaRevisao;
        consultar.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SituacaoEm_ConformePrazoPendenciasEEncerramento_DevolveSituacao()
    {
        CenarioDeMigracao cenario = new(protegido: false);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.SituacaoEm(CenarioDeMigracao.Abertura, 1).Should().Be(SituacaoDaRevisaoDasConcessoes.Aberta);
        revisao.SituacaoEm(CenarioDeMigracao.Abertura, 0).Should().Be(SituacaoDaRevisaoDasConcessoes.Encerrando);
        revisao.SituacaoEm(revisao.TerminaEm, 1).Should().Be(SituacaoDaRevisaoDasConcessoes.Encerrando);
        revisao.Materializar(cenario.Contexto(revisao.TerminaEm), FormaDoEncerramento.Prazo);
        revisao.SituacaoEm(revisao.TerminaEm, 0).Should().Be(SituacaoDaRevisaoDasConcessoes.Encerrada);
    }
}
