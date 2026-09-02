namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
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
            new ObterConformidadeProcessoSeletivoQuery(Guid.CreateVersion7()), repository, CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "Handle com processo sem atendimento nem cronograma devolve os dois pendentes (Story #851 — Etapas não é mais item incondicional)")]
    public async Task Handle_EtapasSemAtendimento_ChecklistParcial()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 3m, ordem: 1).Value!], PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CancellationToken.None);

        result.Should().NotBeNull();
        // Etapa deixou de ser item do checklist (Story #851 §3.5): um processo sem prova
        // publica sem etapa quando o cronograma é coerente. A asserção é por prefixo do
        // código, e não pela frase que o item tinha, para continuar pegando a reintrodução
        // sob qualquer redação nova. O que continua obrigatório e ainda não satisfeito aqui
        // é atendimento, distribuição, classificação e cronograma de fases.
        result!.Itens.Should().NotContain(i => i.Codigo.StartsWith("etapa", StringComparison.Ordinal));
        result.Itens.Should().Contain(i => i.Codigo == "atendimento_especializado_ausente" && !i.Ok);
        result.Itens.Should().Contain(i => i.Codigo == "cronograma_fases_ausente" && !i.Ok);
    }

    [Fact(DisplayName = "Handle com InscricaoPropria sem fase de coleta NÃO declara o checklist inteiramente verde (issue #1092 — regressão do falso verde)")]
    public async Task Handle_InscricaoPropriaSemFaseDeColeta_NaoDeclaraChecklistTodoVerde()
    {
        // Cenário da issue #1092: PendenciaDoCronograma tem quatro razões, mas antes da correção
        // só a de "inscrição própria sem fase de coleta" (via item genérico incondicional) nunca
        // aparecia no checklist — GET /conformidade devolvia tudo Ok enquanto POST /publicacao
        // recusava com 422. Todas as outras dimensões estão presentes; só falta a fase de coleta.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — Falso Verde", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirEtapas([], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "Story #851 §3.5: lista vazia é estado válido sob CLASSIFICACAO-IMPORTADA — sem etapa, não precisamos de fase que agrupe etapas");
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "base legal", quantidadeDeclarada: 50).Value!;
        ReferenciaRegra regraInstitucional = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), voBase: 50, pr: 1m, regraInstitucional, regraAjuste: null, referenciaDemografica: null, [ampla]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!,
            null, null,
            ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('d', 64)).Value!,
            1, [], baseadoEmEnem: false).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Fase que PRODUZ RESULTADO (satisfaz "vagas ofertadas"), mas NÃO agrupa etapas (não
        // precisa — sem etapa) e, sobretudo, NÃO coleta inscrição, embora a origem seja própria.
        FaseCronograma faseSemColeta = FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS", origemData: OrigemDataFase.Propria,
            agrupaEtapas: false, permiteComplementacao: false, produzResultado: true, resultadoDefinitivo: true,
            coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL", atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseSemColeta], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Issue #1112: declarada para isolar a pendência de cronograma como a ÚNICA vermelha.
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Itens.Should().Contain(
            i => i.Codigo == "cronograma_inscricao_propria_sem_fase_de_coleta" && !i.Ok,
            "a origem é InscricaoPropria e nenhuma fase coleta inscrição — antes da correção este item nem existia, e o checklist devolvia tudo Ok");
        result.Itens.Should().ContainSingle(i => !i.Ok,
            "isolar a asserção: só esta razão deveria estar vermelha neste estado — as outras três razões do cronograma e os demais gates estão satisfeitos");

        // A mesma pendência que o checklist agora denuncia é a que Publicar já recusava — as
        // duas superfícies concordam depois da correção (a prova da bicondicional).
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            DadosEdital.Criar("001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!,
            "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", new string('a', 64), "teste", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        publicar.IsFailure.Should().BeTrue();
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta");
    }

    [Fact(DisplayName = "Handle com todos os itens obrigatórios configurados devolve checklist sem pendências, e a publicação aceita (bicondicional, issue #1092)")]
    public async Task Handle_TodosOsItens_SemPendencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirEtapas([EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 3m, ordem: 1).Value!], PrecondicaoIfMatch.Ausente);
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
            [], baseadoEmEnem: false).Value!;
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
            coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente);

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ConformidadeProcessoSeletivoDto? result = await ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeProcessoSeletivoQuery(processo.Id), repository, CalendarioVigenteReaderDeTeste.SemVigente(), new ResolvedorFusoDeTeste(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Itens.Should().OnlyContain(i => i.Ok);

        // issue #1092 — a bicondicional exige mais do que "todas as dimensões presentes": o
        // checklist inteiramente verde TEM de significar que Publicar aceita. Fora do escopo
        // estrutural (não afetado aqui): conformidade legal, documento confirmado, tipo de ato.
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            DadosEdital.Criar("001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!,
            "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", new string('a', 64), "teste", TimeProvider.System, ContextoDeContagemDePrazos.SemCalendario);

        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
    }
}
