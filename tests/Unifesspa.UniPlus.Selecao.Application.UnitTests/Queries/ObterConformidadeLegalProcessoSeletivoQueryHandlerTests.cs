namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Story #853, CA-16 (fonte única): a consulta pública usa exatamente a mesma dupla de
/// chamadas do gate que bloqueia a transição —
/// <c>ObterVigentesParaTipoProcessoAsync</c> + <c>AvaliadorConformidadeLegal.Avaliar</c>
/// (ver <see cref="ObterConformidadeLegalProcessoSeletivoQueryHandler"/> e
/// <c>ConferenciaDeConformidadeLegal</c>, interno à Application). Este teste prova que, para
/// o MESMO processo/regras/data de corte, a consulta produz item a item o mesmo veredicto
/// que uma chamada direta ao avaliador (Domain, público) — nunca duas leituras em paralelo.
/// </summary>
public sealed class ObterConformidadeLegalProcessoSeletivoQueryHandlerTests
{
    private static ObrigatoriedadeLegal NovaRegra(string regraCodigo, PredicadoObrigatoriedade predicado) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: predicado,
            descricaoHumana: "Regra de teste",
            baseLegal: "Lei de teste",
            vigenciaInicio: new DateOnly(2020, 1, 1)).Value!;

    [Fact(DisplayName = "CA-16: o veredicto da consulta pública bate, item a item, com o do avaliador que também alimenta o gate")]
    public async Task Consulta_ComOMesmoProcessoERegras_BateComOAvaliadorDoGate()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(CadastrosVivos.IdentidadeDe("PROVA_OBJETIVA"), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regraAprovada = NovaRegra("CONSULTA-APROVA", new EtapaObrigatoria("PROVA_OBJETIVA"));
        ObrigatoriedadeLegal regraReprovada = NovaRegra("CONSULTA-REPROVA", new EtapaObrigatoria("ENTREVISTA"));
        DateOnly dataDeCorte = new(2026, 1, 1);

        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), dataDeCorte, Arg.Any<CancellationToken>())
            .Returns([regraAprovada, regraReprovada]);

        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id, dataDeCorte),
            processoSeletivoRepository,
            obrigatoriedadeLegalRepository,
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            new ResolvedorFusoDeTeste(),
            CancellationToken.None);

        resultadoDaConsulta.IsSuccess.Should().BeTrue();
        ConformidadeLegalProcessoSeletivoDto dto = resultadoDaConsulta.Value!;
        dto.Regras.Should().HaveCount(2);

        RegraAvaliadaDto reprovadaNaConsulta = dto.Regras.Single(r => r.RegraCodigo == "CONSULTA-REPROVA");
        reprovadaNaConsulta.Aprovada.Should().BeFalse();
        reprovadaNaConsulta.Motivo.Should().Contain("ENTREVISTA",
            "a consulta pública tem de expor o motivo nomeado (CA-02), não projetar Motivo: null");
        RegraAvaliadaDto aprovadaNaConsulta = dto.Regras.Single(r => r.RegraCodigo == "CONSULTA-APROVA");
        aprovadaNaConsulta.Aprovada.Should().BeTrue();
        aprovadaNaConsulta.Motivo.Should().BeNull("regra aprovada não carrega motivo de reprovação");

        // Fonte única: (RegraId, Aprovada, Motivo) da consulta bate, item a item, com o que o
        // MESMO avaliador (Domain, chamado pelo gate na Application) produz para o mesmo
        // processo/regras/data — a consulta não tem lógica própria de decisão.
        Dictionary<Guid, (bool Aprovada, string? Motivo)> avaliacaoDireta = AvaliadorConformidadeLegal
            .Avaliar(processo, processo.Tipo.ToString(), [regraAprovada, regraReprovada], IdentidadesDe(processo))
            .Regras.ToDictionary(r => r.RegraId, r => (r.Aprovada, r.Motivo));

        foreach (RegraAvaliadaDto regraDaConsulta in dto.Regras)
        {
            avaliacaoDireta.Should().ContainKey(regraDaConsulta.RegraId);
            avaliacaoDireta[regraDaConsulta.RegraId].Aprovada.Should().Be(regraDaConsulta.Aprovada);
            avaliacaoDireta[regraDaConsulta.RegraId].Motivo.Should().Be(regraDaConsulta.Motivo);
        }
    }

    [Fact(DisplayName = "CA-16: regra que referencia cadastro inexistente aparece reprovada na consulta, não aprovada por vacuidade")]
    public async Task Consulta_ComRegraDeReferenciaOrfa_ReprovaComOMotivoDaOrfandade()
    {
        // Sem esta conferência, a consulta pública diria que o processo está conforme
        // instantes antes de a publicação recusá-lo por referência órfã: o avaliador é
        // domínio puro e lê "modalidade não ofertada" — que aprova vazio — onde na verdade
        // há uma modalidade que não existe.
        ProcessoSeletivo processo = ProcessoBase();
        ObrigatoriedadeLegal regraOrfa = NovaRegra(
            "CONSULTA-ORFA", new DocumentoObrigatorioParaModalidade("LB_PPl", "LAUDO_MEDICO"));

        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([regraOrfa]);

        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);

        IModalidadeReader modalidadeReader = Substitute.For<IModalidadeReader>();
        modalidadeReader.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ModalidadeView?)null);

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id, new DateOnly(2026, 1, 1)),
            processoSeletivoRepository,
            obrigatoriedadeLegalRepository,
            modalidadeReader,
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            new ResolvedorFusoDeTeste(),
            CancellationToken.None);

        resultadoDaConsulta.IsSuccess.Should().BeTrue();
        RegraAvaliadaDto avaliada = resultadoDaConsulta.Value!.Regras.Should().ContainSingle().Which;
        avaliada.Aprovada.Should().BeFalse(
            "a publicação recusa esta regra — a consulta não pode dizer que ela está cumprida");
        avaliada.Motivo.Should().Contain("LB_PPl", "o motivo tem de dizer qual referência não existe");
    }

    [Fact(DisplayName = "Sem fase que colete inscrição, a data vem do período informado no ato — o 422 não perde as regras reprovadas")]
    public async Task Consulta_SemFaseDeColeta_UsaOPeriodoInformado()
    {
        // O certame de importação externa não tem fase de coleta e informa o período no ato. Se a
        // consulta olhasse só o cronograma, ficaria sem data, devolveria null, e o 422 de
        // conformidade legal desses processos sairia sem `obrigatoriedadesReprovadas` — em
        // silêncio, que é o modo de falha que o BindRequired do endpoint documenta.
        ProcessoSeletivo processo = ProcessoDeImportacaoExterna();
        processo.CronogramaFases.Should().NotContain(
            f => f.ColetaInscricao, "o cenário exige um processo sem fase de coleta");

        ObrigatoriedadeLegal regra = NovaRegra("CONSULTA-SEM-COLETA", new EtapaObrigatoria("PROVA_OBJETIVA"));

        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([regra]);

        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeLegalProcessoSeletivoQuery(
                processo.Id,
                PeriodoInscricaoInformado: InstanteEmBelem.Em(2026, 1, 1)),
            processoSeletivoRepository,
            obrigatoriedadeLegalRepository,
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            new ResolvedorFusoDeTeste(),
            CancellationToken.None);

        resultadoDaConsulta.IsSuccess.Should().BeTrue(
            "sem a data do ato a consulta recusaria e o 422 perderia o diagnóstico");
        resultadoDaConsulta.Value!.DataReferencia.Should().Be(new DateOnly(2026, 1, 1), "o dia sai do instante informado, no fuso institucional");
        resultadoDaConsulta.Value!.Regras.Should().ContainSingle();

        await obrigatoriedadeLegalRepository.Received(1).ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), new DateOnly(2026, 1, 1), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fuso irresolvível propaga como defeito de instalação, e não como processo inexistente")]
    public async Task Consulta_ComFusoIrresolvivel_PropagaAFalha()
    {
        // O fuso irresolvível é defeito de instalação, mapeado para 500 pelos gates de publicação.
        // Se a consulta o convertesse em recusa de domínio, o endpoint devolveria 422 — pediria ao
        // operador que informasse algo, e esconderia a configuração quebrada que ela deveria antecipar.
        // Origem importada: é o ramo que chega a consultar o fuso, porque a inscrição própria sem
        // fase de coleta é recusada antes, pelo código que o gate emite primeiro.
        ProcessoSeletivo processo = ProcessoDeImportacaoExterna();

        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);

        Func<Task> consulta = () => ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeLegalProcessoSeletivoQuery(
                processo.Id,
                PeriodoInscricaoInformado: InstanteEmBelem.Em(2026, 1, 1)),
            processoSeletivoRepository,
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            new ResolvedorFusoIndisponivelDeTeste(),
            CancellationToken.None);

        await consulta.Should().ThrowAsync<InvalidOperationException>(
            "404 diria que o processo não existe, quando o que falta é a base de fusos do ambiente");
    }

    [Fact(DisplayName = "Processo inexistente recusa com NaoEncontrado (404), sem consultar o catálogo de obrigatoriedades")]
    public async Task ProcessoInexistente_RecusaComNaoEncontrado()
    {
        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeLegalProcessoSeletivoQuery(Guid.CreateVersion7(), new DateOnly(2026, 1, 1)),
            processoSeletivoRepository,
            obrigatoriedadeLegalRepository,
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            new ResolvedorFusoDeTeste(),
            CancellationToken.None);

        resultadoDaConsulta.IsFailure.Should().BeTrue();
        resultadoDaConsulta.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado",
            "404 é para o processo que não existe — é a ÚNICA condição que o merece (issue #1456)");
        _ = await obrigatoriedadeLegalRepository.DidNotReceive().ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "#1456 — sem fase de coleta e sem período informado, recusa com o código do gate (422), nunca com 404")]
    public async Task SemFaseDeColetaESemPeriodo_RecusaComOCodigoDoGate()
    {
        // O certame de origem importada chega à Revisão sem fase de coleta e com o período do ato
        // ainda em branco: não há de onde derivar o dia. Traduzir isso em `null` dizia ao endpoint
        // "o processo não existe" (404), e a tela não tinha como distinguir a pendência de uma
        // falha de rede — o campo de período fica atrás do erro, e o processo vira impublicável.
        ProcessoSeletivo processo = ProcessoDeImportacaoExterna();
        processo.CronogramaFases.Should().NotContain(
            f => f.ColetaInscricao, "o cenário exige um processo sem fase de coleta");

        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta =
            await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
                new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id),
                processoSeletivoRepository,
                obrigatoriedadeLegalRepository,
                CadastrosVivos.Modalidades(),
                CadastrosVivos.TiposDocumento(),
                CadastrosVivos.TiposEtapa(),
                CadastrosVivos.TiposDeficiencia(),
                CadastrosVivos.RegrasDesempate(),
                new ResolvedorFusoDeTeste(),
                CancellationToken.None);

        resultadoDaConsulta.IsFailure.Should().BeTrue();
        resultadoDaConsulta.Error!.Code.Should().Be(
            "ProcessoSeletivo.PeriodoInscricaoObrigatorioSemFaseDeColeta",
            "é o MESMO código que ResolucaoDoPeriodoDeInscricao devolve no gate — o preflight espelha a recusa, não inventa a sua");

        _ = await obrigatoriedadeLegalRepository.DidNotReceive().ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "#1456 — inscrição própria sem fase que colete recusa com InscricaoPropriaSemFaseDeColeta, e não com o do período")]
    public async Task InscricaoPropriaSemFaseDeColeta_RecusaComOCodigoQueOGateEmitePrimeiro()
    {
        // A ordem das recusas é a do gate, e não é detalhe de apresentação: `PendenciaDoCronograma`
        // recusa este estado (`ProcessoSeletivo.cs:2106-2110`) ANTES de a resolução do período ser
        // consultada. Devolver o código do período mandaria o operador preencher um campo no ato,
        // quando o que falta é CRIAR a fase que coleta inscrição no cronograma.
        ProcessoSeletivo processo = ProcessoBase();
        processo.OrigemCandidatos.Should().Be(OrigemCandidatos.InscricaoPropria);
        processo.CronogramaFases.Should().NotContain(f => f.ColetaInscricao);

        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta =
            await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
                // Mesmo com um período informado — que neste ramo o gate nem chega a olhar.
                new ObterConformidadeLegalProcessoSeletivoQuery(
                    processo.Id,
                    PeriodoInscricaoInformado: InstanteEmBelem.Em(2026, 1, 1)),
                processoSeletivoRepository,
                obrigatoriedadeLegalRepository,
                CadastrosVivos.Modalidades(),
                CadastrosVivos.TiposDocumento(),
                CadastrosVivos.TiposEtapa(),
                CadastrosVivos.TiposDeficiencia(),
                CadastrosVivos.RegrasDesempate(),
                new ResolvedorFusoDeTeste(),
                CancellationToken.None);

        resultadoDaConsulta.IsFailure.Should().BeTrue();
        resultadoDaConsulta.Error!.Code.Should().Be(
            "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
            "é o que a publicação recusa primeiro — a recusa que sai é a que orienta o operador");
    }

    [Theory(DisplayName = "#1456 — janela MEIO-ABERTA da fase âncora recusa como o gate, e não responde 200")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task FaseDeColetaComJanelaMeioAberta_RecusaComOCodigoDoGate(bool temInicio, bool temFim)
    {
        // `FaseQueColetaInscricaoSemJanela` é `Inicio is null || Fim is null`. Derivar a data só
        // do `Inicio` deixava a consulta aprovar um rascunho que a publicação recusa — meia
        // janela é estado válido de cadastro, e a divergência apareceria exatamente aí.
        ProcessoSeletivo processo = ProcessoBase();
        FaseCronograma meiaJanela = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: true,
            coletaSolicitacaoIsencao: false,
            inicio: temInicio ? InstanteEmBelem.Em(2026, 1, 1) : null,
            fim: temFim ? InstanteEmBelem.Em(2026, 2, 1) : null,
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([meiaJanela], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta =
            await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
                new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id),
                processoSeletivoRepository,
                obrigatoriedadeLegalRepository,
                CadastrosVivos.Modalidades(),
                CadastrosVivos.TiposDocumento(),
                CadastrosVivos.TiposEtapa(),
                CadastrosVivos.TiposDeficiencia(),
                CadastrosVivos.RegrasDesempate(),
                new ResolvedorFusoDeTeste(),
                CancellationToken.None);

        resultadoDaConsulta.IsFailure.Should().BeTrue(
            "o gate recusa a janela meio-aberta, e a consulta responde pelo mesmo veredicto");
        resultadoDaConsulta.Error!.Code.Should().Be("ProcessoSeletivo.FaseQueColetaInscricaoSemJanela");
    }

    [Fact(DisplayName = "#1456 — fase que coleta inscrição sem janela recusa com FaseQueColetaInscricaoSemJanela (422), nunca com 404")]
    public async Task FaseDeColetaSemJanela_RecusaComOCodigoDoGate()
    {
        // A fase DELEGADA pode ficar sem janela no cadastro (§3.2), e o rascunho chega assim à
        // Revisão. O gate recusa a publicação com este código; a consulta tem de dizer o mesmo,
        // para a tela apontar o cronograma em vez de mandar tentar de novo.
        ProcessoSeletivo processo = ProcessoBase();
        FaseCronograma faseSemJanela = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: true,
            coletaSolicitacaoIsencao: false, inicio: null, fim: null,
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseSemJanela], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta =
            await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
                new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id),
                processoSeletivoRepository,
                obrigatoriedadeLegalRepository,
                CadastrosVivos.Modalidades(),
                CadastrosVivos.TiposDocumento(),
                CadastrosVivos.TiposEtapa(),
                CadastrosVivos.TiposDeficiencia(),
                CadastrosVivos.RegrasDesempate(),
                new ResolvedorFusoDeTeste(),
                CancellationToken.None);

        resultadoDaConsulta.IsFailure.Should().BeTrue();
        resultadoDaConsulta.Error!.Code.Should().Be(
            "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
            "o mesmo código que ProcessoSeletivo.Publicar recusa — a pendência é da fase, não do período do ato");
    }

    private static ProcessoSeletivo ProcessoBase() =>
        ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    /// <summary>
    /// O certame cujos candidatos vêm de fora: é o ÚNICO que legitimamente não tem fase de coleta e
    /// informa o período no ato. Com <see cref="OrigemCandidatos.InscricaoPropria"/>, a ausência de
    /// fase de coleta é recusada antes disso, por <c>InscricaoPropriaSemFaseDeColeta</c>.
    /// </summary>
    private static ProcessoSeletivo ProcessoDeImportacaoExterna() =>
        ProcessoSeletivo.Criar(
            "PS 2026 — Transferência", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    /// <summary>Identidades derivadas das próprias exigências — estado sem renomeação nem reciclagem.</summary>
    /// <summary>
    /// Cadastro vivo deduzido do próprio processo: cada código resolve para a identidade
    /// que o processo congelou — o caminho feliz, em que regra e processo apontam para o
    /// mesmo item de catálogo.
    /// </summary>
    private static IdentidadesDeCadastro IdentidadesDe(ProcessoSeletivo processo) =>
        new(
            MapaDe(processo.DocumentosExigidos, e => e.TipoDocumentoCodigo, e => e.TipoDocumentoOrigemId),
            MapaDe(
                processo.DistribuicaoVagas.SelectMany(d => d.Modalidades),
                m => m.Codigo,
                m => m.ModalidadeOrigemId),
            MapaDe(processo.Etapas, e => e.TipoEtapa.Codigo, e => e.TipoEtapa.OrigemId),
            MapaDe(
                processo.OfertaAtendimento?.TiposDeficiencia ?? [],
                t => t.TipoDeficienciaCodigo,
                t => t.TipoDeficienciaOrigemId));

    private static Dictionary<string, Guid> MapaDe<T>(
        IEnumerable<T> itens,
        Func<T, string> codigoDe,
        Func<T, Guid> origemDe) =>
        itens
            .GroupBy(codigoDe, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => origemDe(g.First()), StringComparer.Ordinal);

}
