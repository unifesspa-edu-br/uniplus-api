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

/// <summary>
/// Cobertura do congelamento de metadado de fato (Story #919, RN08) em
/// <see cref="PublicarProcessoSeletivoCommandHandler"/>: <see cref="IFatoCandidatoReader"/>
/// só é consultado quando existe ao menos uma condição de gatilho, o resultado alimenta
/// <see cref="EntradaCanonicalizacao.MetadadosFatosCongelados"/>, e um código de fato que não
/// resolve aborta a publicação com um erro nomeado ANTES de canonicalizar.
/// </summary>
public sealed class PublicarProcessoSeletivoCommandHandlerTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static ProcessoSeletivo NovoProcessoConforme(out Guid faseId)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Metadado de Fato", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

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
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo).Value!,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", HashFixo).Value!,
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

        return processo;
    }

    private static DocumentoExigido ExigenciaComGatilhoPorFato(Guid faseId, string fato) =>
        DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "DECLARACAO",
            tipoDocumentoNome: "Declaração",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            grupoSatisfacaoId: null,
            condicoes: [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!],
            basesLegais: [DocumentoExigidoBaseLegal.Criar(
                "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!],
            idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null).Value!;

    private static FatoCandidatoView FatoModalidade() => new(
        Id: Guid.CreateVersion7(),
        Codigo: "MODALIDADE",
        Nome: "Modalidade",
        Descricao: null,
        Dominio: "CATEGORICO",
        Origem: "DERIVADO",
        Cardinalidade: "ESCALAR",
        ValoresDominio: ["AC"],
        PontoResolucao: "INSCRICAO",
        Binding: "OFERTA:MODALIDADE_CODIGO",
        ValoresDominioDeclarados: [new FatoValorDominioViewItem("AC", "Ampla concorrência", 1, true)]);

    private sealed record Mocks(
        IProcessoSeletivoRepository ProcessoRepository,
        IDocumentoEditalRepository DocumentoRepository,
        ISnapshotPublicacaoCanonicalizer Canonicalizer,
        ITipoAtoPublicadoReader TipoDeAtoReader,
        IVagaDeLinhagemReader VagaDeLinhagemReader,
        IObrigatoriedadeLegalRepository ObrigatoriedadeLegalRepository,
        IFatoCandidatoReader FatoCandidatoReader);

    private static (Mocks Mocks, DocumentoEdital Documento) NovosMocks(ProcessoSeletivo processo, Action<EntradaCanonicalizacao>? captura = null)
    {
        IProcessoSeletivoRepository processoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoRepository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoEditalRepository documentoRepository = Substitute.For<IDocumentoEditalRepository>();
        documentoRepository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);

        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Do<EntradaCanonicalizacao>(e => captura?.Invoke(e)))
            .Returns(new SnapshotCanonico("{}"u8.ToArray(), "1.3", "canonical-json/sha256@v1"));

        ITipoAtoPublicadoReader tipoDeAtoReader = Substitute.For<ITipoAtoPublicadoReader>();
        tipoDeAtoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA", Nome: "Edital de abertura",
                CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false));

        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([]);

        return (new Mocks(
            processoRepository,
            documentoRepository,
            canonicalizer,
            tipoDeAtoReader,
            Substitute.For<IVagaDeLinhagemReader>(),
            obrigatoriedadeLegalRepository,
            Substitute.For<IFatoCandidatoReader>()), documento);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> HandleAsync(
        Mocks mocks, ProcessoSeletivo processo, DocumentoEdital documento)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns("user-sub-1");

        return PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                DocumentoEditalId: documento.Id,
                Ato: new DadosDoAto(
                    Orgao: "CEPS", Serie: "EDITAL", Ano: 2026, DataPublicacao: new DateOnly(2026, 1, 1),
                    Assinante: "Diretor do CEPS", TipoAtoCodigo: "EDITAL_ABERTURA")),
            mocks.ProcessoRepository,
            mocks.DocumentoRepository,
            mocks.Canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            userContext,
            mocks.TipoDeAtoReader,
            mocks.VagaDeLinhagemReader,
            mocks.ObrigatoriedadeLegalRepository,
            mocks.FatoCandidatoReader,
            TimeProvider.System,
            CancellationToken.None);
    }

    [Fact(DisplayName = "Sem condição de gatilho, MetadadosFatosCongelados é null e IFatoCandidatoReader não é consultado")]
    public async Task Handle_SemCondicaoDeGatilho_NaoConsultaReaderEMetadadosENulo()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo, e => entradaCapturada = e);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.MetadadosFatosCongelados.Should().BeNull(
            "nenhuma condição de gatilho existe no processo — nada a congelar");
        _ = await mocks.FatoCandidatoReader.DidNotReceive().ObterPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com condição de gatilho resolvida, MetadadosFatosCongelados carrega o metadado do fato citado")]
    public async Task Handle_ComCondicaoDeGatilhoResolvida_ResolveMetadadoDoFato()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirDocumentosExigidos([ExigenciaComGatilhoPorFato(faseId, "MODALIDADE")], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo, e => entradaCapturada = e);
        mocks.FatoCandidatoReader.ObterPorCodigoAsync("MODALIDADE", Arg.Any<CancellationToken>())
            .Returns(FatoModalidade());

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.MetadadosFatosCongelados.Should().NotBeNull();
        entradaCapturada.MetadadosFatosCongelados.Should().ContainKey("MODALIDADE");
        MetadadoFatoCongelado metadado = entradaCapturada.MetadadosFatosCongelados!["MODALIDADE"];
        metadado.Dominio.Should().Be("CATEGORICO");
        metadado.Origem.Should().Be("DERIVADO");
        metadado.Cardinalidade.Should().Be("ESCALAR");
        metadado.PontoResolucao.Should().Be("INSCRICAO");
        metadado.Binding.Should().Be("OFERTA:MODALIDADE_CODIGO");
        metadado.ValoresDominioDeclarados.Should().ContainSingle(v => v.Codigo == "AC" && v.Descricao == "Ampla concorrência");
    }

    [Fact(DisplayName = "Código de fato que não resolve no catálogo aborta a publicação ANTES de canonicalizar")]
    public async Task Handle_CodigoDeFatoNaoResolve_AbortaAntesDeCanonicalizar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirDocumentosExigidos([ExigenciaComGatilhoPorFato(faseId, "FATO_INEXISTENTE")], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo);
        mocks.FatoCandidatoReader.ObterPorCodigoAsync("FATO_INEXISTENTE", Arg.Any<CancellationToken>())
            .Returns((FatoCandidatoView?)null);

        (Result resposta, IEnumerable<object> eventos) = await HandleAsync(mocks, processo, documento);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoCongeladoNaoEncontrado");
        _ = mocks.Canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Status.Should().Be(StatusProcesso.Rascunho, "a publicação recusada não transita o processo");
    }
}
