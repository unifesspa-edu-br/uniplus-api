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

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.CenarioDaConsultaDeConformidadeLegal;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

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
            ObrigatoriedadeLegal.TipoProcessoUniversal,
            CategoriaObrigatoriedade.Outros,
            regraCodigo,
            predicado,
            new TextoNormativo("Regra de teste", "Lei de teste", null, null),
            new DateOnly(2020, 1, 1),
            null).Value!;

    [Fact(DisplayName = "CA-16: o veredicto da consulta pública bate, item a item, com o do avaliador que também alimenta o gate")]
    public async Task Consulta_ComOMesmoProcessoERegras_BateComOAvaliadorDoGate()
    {
        TipoEtapaSnapshot provaObjetiva = TipoCongelado(CadastrosVivos.IdentidadeDe("PROVA_OBJETIVA"), "PROVA_OBJETIVA", "Prova Objetiva", true, true);
        ProcessoSeletivo processo = ProcessoComAEtapa(EtapaDoProcesso("Prova Objetiva", CaraterEtapa.Classificatoria, 1, provaObjetiva, 1m));
        ObrigatoriedadeLegal regraAprovada = NovaRegra("CONSULTA-APROVA", new EtapaObrigatoria("PROVA_OBJETIVA"));
        ObrigatoriedadeLegal regraReprovada = NovaRegra("CONSULTA-REPROVA", new EtapaObrigatoria("ENTREVISTA"));

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await ConsultarAsync(processo, new DateOnly(2026, 1, 1), regraAprovada, regraReprovada);

        resultadoDaConsulta.IsSuccess.Should().BeTrue();
        ConformidadeLegalProcessoSeletivoDto dto = resultadoDaConsulta.Value!;
        dto.Regras.Should().HaveCount(2);
        ConferirMotivosExpostos(dto);
        ConferirQueBateComOAvaliadorDireto(dto, processo, regraAprovada, regraReprovada);
    }

    /// <summary>
    /// Sem esta conferência, a consulta pública diria que o processo está conforme instantes antes de a publicação
    /// recusá-lo por referência órfã: o avaliador é domínio puro e lê "modalidade não ofertada" — que aprova vazio —
    /// onde na verdade há uma modalidade que não existe.
    /// </summary>
    [Fact(DisplayName = "CA-16: regra que referencia cadastro inexistente aparece reprovada na consulta, não aprovada por vacuidade")]
    public async Task Consulta_ComRegraDeReferenciaOrfa_ReprovaComOMotivoDaOrfandade()
    {
        ProcessoSeletivo processo = ProcessoBase();
        ObrigatoriedadeLegal regraOrfa = NovaRegra("CONSULTA-ORFA", new DocumentoObrigatorioParaModalidade("LB_PPl", "LAUDO_MEDICO"));
        CenarioDaConsultaDeConformidadeLegal cenario = ComOProcesso(processo).ComRegrasVigentesEmQualquerData(regraOrfa).SemModalidadeNoCadastro();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await cenario.ConsultarAsync(
            new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id, new DateOnly(2026, 1, 1)));

        resultadoDaConsulta.IsSuccess.Should().BeTrue();
        RegraAvaliadaDto avaliada = resultadoDaConsulta.Value!.Regras.Should().ContainSingle().Which;
        avaliada.Aprovada.Should().BeFalse(
            "a publicação recusa esta regra — a consulta não pode dizer que ela está cumprida");
        avaliada.Motivo.Should().Contain("LB_PPl", "o motivo tem de dizer qual referência não existe");
    }

    /// <summary>
    /// O certame de importação externa não tem fase de coleta e informa o período no ato. Se a consulta olhasse só o
    /// cronograma, ficaria sem data, devolveria null, e o 422 de conformidade legal desses processos sairia sem
    /// `obrigatoriedadesReprovadas` — em silêncio, que é o modo de falha que o BindRequired do endpoint documenta.
    /// </summary>
    [Fact(DisplayName = "Sem fase que colete inscrição, a data vem do período informado no ato — o 422 não perde as regras reprovadas")]
    public async Task Consulta_SemFaseDeColeta_UsaOPeriodoInformado()
    {
        ProcessoSeletivo processo = ProcessoDeImportacaoExternaSemFaseDeColeta();
        ObrigatoriedadeLegal regra = NovaRegra("CONSULTA-SEM-COLETA", new EtapaObrigatoria("PROVA_OBJETIVA"));
        CenarioDaConsultaDeConformidadeLegal cenario = ComOProcesso(processo).ComRegrasVigentesEmQualquerData(regra);

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await cenario.ConsultarAsync(ConsultaComPeriodoInformado(processo));

        resultadoDaConsulta.IsSuccess.Should().BeTrue(
            "sem a data do ato a consulta recusaria e o 422 perderia o diagnóstico");
        resultadoDaConsulta.Value!.DataReferencia.Should().Be(new DateOnly(2026, 1, 1), "o dia sai do instante informado, no fuso institucional");
        resultadoDaConsulta.Value!.Regras.Should().ContainSingle();
        await cenario.Obrigatoriedades.Received(1).ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), new DateOnly(2026, 1, 1), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O fuso irresolvível é defeito de instalação, mapeado para 500 pelos gates de publicação. Se a consulta o
    /// convertesse em recusa de domínio, o endpoint devolveria 422 — pediria ao operador que informasse algo, e
    /// esconderia a configuração quebrada que ela deveria antecipar. A origem é importada porque é o ramo que chega
    /// a consultar o fuso: a inscrição própria sem fase de coleta é recusada antes, pela verificação que emite o código primeiro.
    /// </summary>
    [Fact(DisplayName = "Fuso irresolvível propaga como defeito de instalação, e não como processo inexistente")]
    public async Task Consulta_ComFusoIrresolvivel_PropagaAFalha()
    {
        ProcessoSeletivo processo = ProcessoDeImportacaoExterna();
        CenarioDaConsultaDeConformidadeLegal cenario = ComOProcesso(processo).ComFusoInstitucionalIndisponivel();

        Func<Task> consulta = () => cenario.ConsultarAsync(ConsultaComPeriodoInformado(processo));

        await consulta.Should().ThrowAsync<InvalidOperationException>(
            "404 diria que o processo não existe, quando o que falta é a base de fusos do ambiente");
    }

    [Fact(DisplayName = "Processo inexistente recusa com NaoEncontrado (404), sem consultar o catálogo de obrigatoriedades")]
    public async Task ProcessoInexistente_RecusaComNaoEncontrado()
    {
        CenarioDaConsultaDeConformidadeLegal cenario = SemNenhumProcesso();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await cenario.ConsultarAsync(
            new ObterConformidadeLegalProcessoSeletivoQuery(Guid.CreateVersion7(), new DateOnly(2026, 1, 1)));

        resultadoDaConsulta.IsFailure.Should().BeTrue();
        resultadoDaConsulta.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado",
            "404 é para o processo que não existe — é a ÚNICA condição que o merece (issue #1456)");
        _ = await cenario.Obrigatoriedades.DidNotReceive().ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O certame de origem importada chega à Revisão sem fase de coleta e com o período do ato ainda em branco: não há
    /// de onde derivar o dia. Traduzir isso em `null` dizia ao endpoint "o processo não existe" (404), e a tela não
    /// tinha como distinguir a pendência de uma falha de rede — o campo de período fica atrás do erro, e o processo
    /// vira impublicável.
    /// </summary>
    [Fact(DisplayName = "#1456 — sem fase de coleta e sem período informado, recusa com o código do gate (422), nunca com 404")]
    public async Task SemFaseDeColetaESemPeriodo_RecusaComOCodigoDoGate()
    {
        ProcessoSeletivo processo = ProcessoDeImportacaoExternaSemFaseDeColeta();
        CenarioDaConsultaDeConformidadeLegal cenario = ComOProcesso(processo);

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await cenario.ConsultarAsync(
            new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id));

        resultadoDaConsulta.IsFailure.Should().BeTrue();
        resultadoDaConsulta.Error!.Code.Should().Be(
            "ProcessoSeletivo.PeriodoInscricaoObrigatorioSemFaseDeColeta",
            "é o MESMO código que ResolucaoDoPeriodoDeInscricao devolve no gate — o preflight espelha a recusa, não inventa a sua");
        _ = await cenario.Obrigatoriedades.DidNotReceive().ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A ordem das recusas é a da verificação, e não é detalhe de apresentação: `PendenciaDoCronograma` recusa este estado
    /// (`ProcessoSeletivo.cs:2106-2110`) ANTES de a resolução do período ser consultada. Devolver o código do período
    /// mandaria o operador preencher um campo no ato, quando o que falta é CRIAR a fase que coleta inscrição no
    /// cronograma. A consulta leva um período informado, que neste ramo a verificação nem chega a olhar.
    /// </summary>
    [Fact(DisplayName = "#1456 — inscrição própria sem fase que colete recusa com InscricaoPropriaSemFaseDeColeta, e não com o do período")]
    public async Task InscricaoPropriaSemFaseDeColeta_RecusaComOCodigoQueOGateEmitePrimeiro()
    {
        ProcessoSeletivo processo = ProcessoDeInscricaoPropriaSemFaseDeColeta();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await ComOProcesso(processo)
            .ConsultarAsync(ConsultaComPeriodoInformado(processo));

        resultadoDaConsulta.IsFailure.Should().BeTrue();
        resultadoDaConsulta.Error!.Code.Should().Be(
            "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta",
            "é o que a publicação recusa primeiro — a recusa que sai é a que orienta o operador");
    }

    private static FaseCronograma FaseDeInscricaoComJanelaParcial(bool temInicio, bool temFim) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            1, Inicio: temInicio ? InstanteEmBelem.Em(2026, 1, 1) : null, Fim: temFim ? InstanteEmBelem.Em(2026, 2, 1) : null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Theory(DisplayName = "#1456 — janela MEIO-ABERTA da fase âncora recusa como o gate, e não responde 200")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task FaseDeColetaComJanelaMeioAberta_RecusaComOCodigoDoGate(bool temInicio, bool temFim)
    {
        // `FaseQueColetaInscricaoSemJanela` é `Inicio is null || Fim is null`. Derivar a data só
        // do `Inicio` deixava a consulta aprovar um rascunho que a publicação recusa — meia
        // janela é estado válido de cadastro, e a divergência apareceria exatamente aí.
        ProcessoSeletivo processo = ProcessoBase();
        FaseCronograma meiaJanela = FaseDeInscricaoComJanelaParcial(temInicio, temFim);
        processo.DefinirCronogramaFases([meiaJanela], [])
            .IsSuccess.Should().BeTrue();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await ConsultarAsync(processo);

        resultadoDaConsulta.IsFailure.Should().BeTrue(
            "o gate recusa a janela meio-aberta, e a consulta responde pelo mesmo veredicto");
        resultadoDaConsulta.Error!.Code.Should().Be("ProcessoSeletivo.FaseQueColetaInscricaoSemJanela");
    }

    private static FaseCronograma FaseDeInscricaoNaOrdem1() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "#1456 — fase que coleta inscrição sem janela recusa com FaseQueColetaInscricaoSemJanela (422), nunca com 404")]
    public async Task FaseDeColetaSemJanela_RecusaComOCodigoDoGate()
    {
        // A fase NA_EXECUCAO pode ficar sem janela no cadastro (§3.2), e o rascunho chega assim à
        // Revisão. O gate recusa a publicação com este código; a consulta tem de dizer o mesmo,
        // para a tela apontar o cronograma em vez de mandar tentar de novo.
        ProcessoSeletivo processo = ProcessoBase();
        FaseCronograma faseSemJanela = FaseDeInscricaoNaOrdem1();
        processo.DefinirCronogramaFases([faseSemJanela], [])
            .IsSuccess.Should().BeTrue();

        Result<ConformidadeLegalProcessoSeletivoDto> resultadoDaConsulta = await ConsultarAsync(processo);

        resultadoDaConsulta.IsFailure.Should().BeTrue();
        resultadoDaConsulta.Error!.Code.Should().Be(
            "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
            "o mesmo código que ProcessoSeletivo.Publicar recusa — a pendência é da fase, não do período do ato");
    }

    private static Task<Result<ConformidadeLegalProcessoSeletivoDto>> ConsultarAsync(ProcessoSeletivo processo) =>
        ComOProcesso(processo).ConsultarAsync(new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id));

    private static ObterConformidadeLegalProcessoSeletivoQuery ConsultaComPeriodoInformado(ProcessoSeletivo processo) =>
        new(processo.Id, PeriodoInscricaoInformado: InstanteEmBelem.Em(2026, 1, 1));

    private static ProcessoSeletivo ProcessoBase() =>
        ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    /// <summary>
    /// O certame cujos candidatos vêm de fora: é o ÚNICO que legitimamente não tem fase de coleta e
    /// informa o período no ato. Com <see cref="OrigemCandidatos.InscricaoPropria"/>, a ausência de
    /// fase de coleta é recusada antes disso, por <c>InscricaoPropriaSemFaseDeColeta</c>.
    /// </summary>
    private static ProcessoSeletivo ProcessoDeImportacaoExterna() =>
        ProcessoSeletivo.Criar(
            "PS 2026 — Transferência", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static ProcessoSeletivo ProcessoDeImportacaoExternaSemFaseDeColeta()
    {
        ProcessoSeletivo processo = ProcessoDeImportacaoExterna();
        processo.CronogramaFases.Should().NotContain(
            f => f.ColetaInscricao, "o cenário exige um processo sem fase de coleta");

        return processo;
    }

    private static ProcessoSeletivo ProcessoDeInscricaoPropriaSemFaseDeColeta()
    {
        ProcessoSeletivo processo = ProcessoBase();
        processo.OrigemCandidatos.Should().Be(OrigemCandidatos.InscricaoPropria);
        processo.CronogramaFases.Should().NotContain(f => f.ColetaInscricao);

        return processo;
    }

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
                processo.AtendimentoEspecializado?.TiposDeficiencia ?? [],
                t => t.TipoDeficienciaCodigo,
                t => t.TipoDeficienciaOrigemId));

    private static Dictionary<string, Guid> MapaDe<T>(
        IEnumerable<T> itens,
        Func<T, string> codigoDe,
        Func<T, Guid> origemDe) =>
        itens
            .GroupBy(codigoDe, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => origemDe(g.First()), StringComparer.Ordinal);


    private static Task<Result<ConformidadeLegalProcessoSeletivoDto>> ConsultarAsync(
        ProcessoSeletivo processo, DateOnly dataDeCorte, params ObrigatoriedadeLegal[] regras) =>
        ComOProcesso(processo).ComRegrasVigentesNaDataDeCorte(dataDeCorte, regras)
            .ConsultarAsync(new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id, dataDeCorte));

    private static void ConferirMotivosExpostos(ConformidadeLegalProcessoSeletivoDto dto)
    {
        RegraAvaliadaDto reprovadaNaConsulta = dto.Regras.Single(r => r.RegraCodigo == "CONSULTA-REPROVA");
        reprovadaNaConsulta.Aprovada.Should().BeFalse();
        reprovadaNaConsulta.Motivo.Should().Contain("ENTREVISTA",
            "a consulta pública tem de expor o motivo nomeado (CA-02), não projetar Motivo: null");
        RegraAvaliadaDto aprovadaNaConsulta = dto.Regras.Single(r => r.RegraCodigo == "CONSULTA-APROVA");
        aprovadaNaConsulta.Aprovada.Should().BeTrue();
        aprovadaNaConsulta.Motivo.Should().BeNull("regra aprovada não carrega motivo de reprovação");
    }

    /// <summary>
    /// Fonte única: (RegraId, Aprovada, Motivo) da consulta bate, item a item, com o que o mesmo
    /// avaliador (Domain, chamado pelo gate na Application) produz para o mesmo processo, regras e
    /// data — a consulta não tem lógica própria de decisão.
    /// </summary>
    private static void ConferirQueBateComOAvaliadorDireto(
        ConformidadeLegalProcessoSeletivoDto dto, ProcessoSeletivo processo, params ObrigatoriedadeLegal[] regras)
    {
        Dictionary<Guid, (bool Aprovada, string? Motivo)> avaliacaoDireta = AvaliadorConformidadeLegal
            .Avaliar(processo, processo.TipoProcesso.ToString(), regras, IdentidadesDe(processo), FatosDeModalidadeDeTeste.DoCatalogo)
            .Regras.ToDictionary(r => r.RegraId, r => (r.Aprovada, r.Motivo));

        foreach (RegraAvaliadaDto regraDaConsulta in dto.Regras)
        {
            avaliacaoDireta.Should().ContainKey(regraDaConsulta.RegraId);
            avaliacaoDireta[regraDaConsulta.RegraId].Aprovada.Should().Be(regraDaConsulta.Aprovada);
            avaliacaoDireta[regraDaConsulta.RegraId].Motivo.Should().Be(regraDaConsulta.Motivo);
        }
    }
}
