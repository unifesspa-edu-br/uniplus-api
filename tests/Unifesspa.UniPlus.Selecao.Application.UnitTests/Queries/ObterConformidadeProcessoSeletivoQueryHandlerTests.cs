namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ObterConformidadeProcessoSeletivoQueryHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente retorna null (mapeado a 404 pelo controller)")]
    public async Task Handle_ProcessoInexistente_RetornaNull()
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(Guid.CreateVersion7()), repository, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "Handle com processo sem atendimento nem cronograma devolve os dois pendentes (Story #851 — Etapas não é mais item incondicional)")]
    public async Task Handle_EtapasSemAtendimento_ChecklistParcial()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1)], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        result.Should().NotBeNull();
        // "Etapas" deixou de ser item do checklist (Story #851 §3.5) — o que continua
        // obrigatório e ainda não satisfeito aqui é atendimento, distribuição, classificação
        // e cronograma de fases.
        result!.Itens.Should().NotContain(i => i.Item == "Etapas");
        result.Itens.Should().Contain(i => i.Item == "Atendimento especializado" && !i.Ok);
        result.Itens.Should().Contain(i => i.Item == "Cronograma de fases" && !i.Ok);
    }

    [Fact(DisplayName = "Handle com todos os itens obrigatórios configurados devolve checklist sem pendências")]
    public async Task Handle_TodosOsItens_SemPendencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 3m, ordem: 1)], PrecondicaoIfMatch.Ausente);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base legal", quantidadeDeclarada: 50).Value!;
        ReferenciaRegra regraInstitucional = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 1m, regraInstitucional, regraAjuste: null, referenciaDemografica: null, [ampla]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente);

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('b', 64)).Value!,
            ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('c', 64)).Value!,
            2,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('d', 64)).Value!,
            1,
            []).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente);

        FaseCronograma faseConforme = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Itens.Should().OnlyContain(i => i.Ok);
    }
}
