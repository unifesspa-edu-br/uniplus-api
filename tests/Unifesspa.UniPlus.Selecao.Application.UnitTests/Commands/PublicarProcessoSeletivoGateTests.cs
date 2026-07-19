namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// <b>Contraprova do CA-10 (ADR-0109 D5):</b> o gate precede a canonicalização.
/// </summary>
/// <remarks>
/// <para>
/// Um processo não conforme <b>não chega a ser projetado</b>. O canonicalizador
/// substituto registra a invocação — se ele for chamado, o teste falha.
/// </para>
/// <para>
/// Por que importa: sem o gate antecipado, a projeção de uma dimensão obrigatória
/// ausente <b>lançaria</b> (ADR-0109 D8) em vez de devolver o <c>DomainError</c> que
/// o contrato HTTP promete. O 422 viraria 500.
/// </para>
/// </remarks>
public sealed class PublicarProcessoSeletivoGateTests
{
    /// <summary>Canonicalizador espião — registra se foi invocado.</summary>
    private sealed class CanonicalizerEspiao : ISnapshotPublicacaoCanonicalizer
    {
        public bool FoiInvocado { get; private set; }

        public SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada)
        {
            FoiInvocado = true;
            return new SnapshotCanonico("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1");
        }
    }

    [Fact(DisplayName = "Publicar_ProcessoNaoConforme_NaoCanonicaliza — o gate precede a projeção (CA-10)")]
    public async Task Publicar_ProcessoNaoConforme_NaoCanonicaliza()
    {
        // Processo em rascunho, SEM nenhuma dimensão configurada — não conforme.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Vazio", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);

        IDocumentoEditalRepository documentoRepository = Substitute.For<IDocumentoEditalRepository>();
        documentoRepository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(documento);

        CanonicalizerEspiao canonicalizer = new();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns("teste");

        // As conferências que precedem o gate no handler (tipo de ato, vaga de
        // linhagem) têm de PASSAR — senão o teste provaria a recusa errada e o
        // canonicalizador ficaria sem ser chamado por outro motivo.
        ITipoAtoPublicadoReader tipoDeAtoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoDeAtoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA",
                Nome: "Edital de abertura",
                CongelaConfiguracao: true,
                UnicoPorObjeto: true,
                EfeitoIrreversivel: false));

        IVagaDeLinhagemReader vagaDeLinhagemReader = Substitute.For<IVagaDeLinhagemReader>();

        (Result resposta, IEnumerable<object> eventos) = await PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                ProcessoSeletivoId: processo.Id,
                Numero: "001/2026",
                PeriodoInscricaoInicio: new DateOnly(2026, 1, 1),
                PeriodoInscricaoFim: new DateOnly(2026, 1, 31),
                DocumentoEditalId: documento.Id,
                Ato: new DadosDoAto(
                    Orgao: "CEPS",
                    Serie: "EDITAL",
                    Ano: 2026,
                    DataPublicacao: new DateOnly(2026, 1, 1),
                    Assinante: "Diretor do CEPS",
                    TipoAtoCodigo: "EDITAL_ABERTURA")),
            processoRepository,
            documentoRepository,
            canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            userContext,
            tipoDeAtoReader,
            vagaDeLinhagemReader,
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            Substitute.For<IFatoCandidatoReader>(),
            TimeProvider.System,
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            "o contrato HTTP não muda — continua sendo o mesmo 422 de sempre");

        canonicalizer.FoiInvocado.Should().BeFalse(
            "um processo não conforme NÃO chega a ser canonicalizado (ADR-0109 D5). Sem o gate antecipado, a " +
            "projeção de uma dimensão obrigatória ausente lançaria (D8) e o 422 viraria 500.");

        eventos.Should().BeEmpty();
    }

    /// <summary>
    /// Canonicalizador espião que reproduz o efeito colateral real do
    /// <c>SnapshotPublicacaoCanonicalizer</c> ao serializar <c>documentosExigidos</c>:
    /// invoca <see cref="ProcessoSeletivo.ResolverDataReferenciaFatos"/>. Um processo com
    /// gatilho FAIXA_ETARIA sem <see cref="ReferenciaTemporalFatos"/> configurada faz esse
    /// método LANÇAR — se o handler chamar este canonicalizador antes do guard de domínio,
    /// o teste falha com <see cref="InvalidOperationException"/> não tratada em vez de
    /// receber o <c>DomainError</c> nomeado.
    /// </summary>
    private sealed class CanonicalizerQueResolveReferenciaTemporalFatos : ISnapshotPublicacaoCanonicalizer
    {
        public bool FoiInvocado { get; private set; }

        public SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada)
        {
            FoiInvocado = true;
            entrada.Processo.ResolverDataReferenciaFatos();
            return new SnapshotCanonico("{}"u8.ToArray(), "1.2", "canonical-json/sha256@v1");
        }
    }

    /// <summary>Processo conforme (etapas, oferta, distribuição, classificação, cronograma) — o mínimo para passar pelos dois primeiros gates do handler.</summary>
    private static ProcessoSeletivo NovoProcessoConformeComGatilhoPorFaixaEtariaSemReferencia(out Guid faseId)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Gate D5", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        string hashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", hashFixo).Value!,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", hashFixo).Value!,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", hashFixo).Value!,
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
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
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        faseId = fase.Id;

        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "DECLARACAO_MAIORIDADE",
            tipoDocumentoNome: "Declaração de maioridade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            grupoSatisfacaoId: null,
            condicoes: [condicao], basesLegais: [baseLegal], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        // Nenhuma ReferenciaTemporalFatos configurada — o gatilho FAIXA_ETARIA a exige.

        return processo;
    }

    [Fact(DisplayName = "Publicar_ExigenciaComGatilhoPorFaixaEtariaSemReferenciaTemporal_NaoCanonicalizaAntesDoGuard — achado Codex (PR #903): o gate B-03 também precede a canonicalização (ADR-0109 D5)")]
    public async Task Publicar_ExigenciaComGatilhoPorFaixaEtariaSemReferenciaTemporal_NaoCanonicalizaAntesDoGuard()
    {
        ProcessoSeletivo processo = NovoProcessoConformeComGatilhoPorFaixaEtariaSemReferencia(out _);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();

        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);

        IDocumentoEditalRepository documentoRepository = Substitute.For<IDocumentoEditalRepository>();
        documentoRepository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(documento);

        CanonicalizerQueResolveReferenciaTemporalFatos canonicalizer = new();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns("teste");

        ITipoAtoPublicadoReader tipoDeAtoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoDeAtoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA",
                Nome: "Edital de abertura",
                CongelaConfiguracao: true,
                UnicoPorObjeto: true,
                EfeitoIrreversivel: false));

        IVagaDeLinhagemReader vagaDeLinhagemReader = Substitute.For<IVagaDeLinhagemReader>();

        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);

        (Result resposta, IEnumerable<object> eventos) = await PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                ProcessoSeletivoId: processo.Id,
                Numero: "001/2026",
                PeriodoInscricaoInicio: new DateOnly(2026, 1, 1),
                PeriodoInscricaoFim: new DateOnly(2026, 1, 31),
                DocumentoEditalId: documento.Id,
                Ato: new DadosDoAto(
                    Orgao: "CEPS",
                    Serie: "EDITAL",
                    Ano: 2026,
                    DataPublicacao: new DateOnly(2026, 1, 1),
                    Assinante: "Diretor do CEPS",
                    TipoAtoCodigo: "EDITAL_ABERTURA")),
            processoRepository,
            documentoRepository,
            canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            userContext,
            tipoDeAtoReader,
            vagaDeLinhagemReader,
            obrigatoriedadeLegalRepository,
            Substitute.For<IFatoCandidatoReader>(),
            TimeProvider.System,
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosAusente");

        canonicalizer.FoiInvocado.Should().BeFalse(
            "o guard B-03 (PendenciaPreCanonicalizacao) precede a canonicalização — sem ele, " +
            "ResolverDataReferenciaFatos() lançaria dentro do canonicalizador e a publicação " +
            "devolveria 500 em vez do 422 nomeado.");

        eventos.Should().BeEmpty();
    }
}
