namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ProjecaoDasVagasTests
{
    [Theory(DisplayName = "O ramo do quadro fixado sai do código da regra congelada, qualquer que seja a versão que o catálogo tenha hoje")]
    [InlineData("v1", 'a')]
    [InlineData("v7", 'f')]
    public void ProjetarDistribuicao_QuadroFixado_RamoDoCodigoCongelado(string versaoCongelada, char caractereDoHash)
    {
        ConfiguracaoDistribuicaoVagas configuracao = DistribuicaoFixadaNoEdital(
            ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, versaoCongelada, new string(caractereDoHash, 64)));

        ConfiguracaoDistribuicaoVagasDto dto = ProjecaoDasVagas.ProjetarDistribuicao(configuracao);

        dto.RamoDaDistribuicao.Should().Be(RamoDaDistribuicaoDeVagasCodigo.QuadroFixadoNoEdital);
        dto.RegraDistribuicao.Versao.Should().Be(versaoCongelada);
    }

    [Theory(DisplayName = "O ramo da Lei de Cotas sai do código da regra congelada, qualquer que seja a versão que o catálogo tenha hoje")]
    [InlineData(RegraDistribuicaoVagasCodigo.Lei12711, "v1")]
    [InlineData(RegraDistribuicaoVagasCodigo.Lei12711ComAcPcd, "v3")]
    public void ProjetarDistribuicao_LeiDeCotas_RamoDoCodigoCongelado(string codigoCongelado, string versaoCongelada)
    {
        ConfiguracaoDistribuicaoVagas configuracao = DistribuicaoDaLeiDeCotas(
            ReferenciaRegra.De(codigoCongelado, versaoCongelada, new string('c', 64)));

        ConfiguracaoDistribuicaoVagasDto dto = ProjecaoDasVagas.ProjetarDistribuicao(configuracao);

        dto.RamoDaDistribuicao.Should().Be(RamoDaDistribuicaoDeVagasCodigo.LeiDeCotas);
        dto.RegraDistribuicao.Codigo.Should().Be(codigoCongelado);
    }

    private static ConfiguracaoDistribuicaoVagas DistribuicaoFixadaNoEdital(ReferenciaRegra regraDistribuicao) =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 50,
                PercentualDaReservaLegal = 1m,
                RegraDeDistribuicao = regraDistribuicao,
                Modalidades = [NovaModalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, 50)],
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static ConfiguracaoDistribuicaoVagas DistribuicaoDaLeiDeCotas(ReferenciaRegra regraDistribuicao) =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 40,
                PercentualDaReservaLegal = 0.5m,
                RegraDeDistribuicao = regraDistribuicao,
                RegraDeAjuste = ReferenciaRegra.De(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, "v1", new string('d', 64)),
                ReferenciaDemografica = ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 50m, 10m, 10m, "Censo 2022").Value!,
                Modalidades = ModalidadesDaLeiDeCotas(),
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static List<ModalidadeSelecionada> ModalidadesDaLeiDeCotas() =>
    [
        .. ModalidadesFederaisLei12711.Codigos.Select(codigo => NovaModalidade(
            codigo, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal, quantidade: null, RegraRemanejamentoModalidade.SegueCascata)),
        NovaModalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, quantidade: null),
    ];

    private static ModalidadeSelecionada NovaModalidade(
        string codigo,
        NaturezaLegalModalidade natureza,
        ComposicaoVagasModalidade composicao,
        int? quantidade,
        RegraRemanejamentoModalidade remanejamento = RegraRemanejamentoModalidade.Nenhuma) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza, composicao, null, remanejamento, null, null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza), null, "base legal", quantidade).Value!;
}
