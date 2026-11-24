namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A avaliação da inscrição na forma que o candidato recebe: os documentos agrupados pelas fases do
/// cronograma da versão fixada, só os que se aplicam ou podem se aplicar, com a finalidade de cada um
/// (UNI-REQ-0069, UNI-REQ-0077).
/// </summary>
public sealed class ProjecaoDaAvaliacaoDaInscricaoTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Guid AtoId = Guid.CreateVersion7();
    private static readonly FaseCronograma FaseDaInscricao = Fase(1, "INSCRICAO");
    private static readonly FaseCronograma FaseDaHabilitacao = Fase(3, "HABILITACAO");

    [Fact(DisplayName = "Sem fase pedida, vêm todas as fases na ordem do cronograma, cada exigência na sua, e a não exigida não vem")]
    public void SemFase_TodasAsFasesSemONaoExigido()
    {
        DocumentosDasDuasFases documentos = DocumentosDasDuasFases.Novos();

        AvaliacaoDaInscricaoDto avaliacao = Projetar(documentos.ComAsSituacoesDaAvaliacao(), faseId: null).Value!;

        avaliacao.Documentos.Fases.Select(static f => f.Codigo).Should().Equal("INSCRICAO", "HABILITACAO");
        avaliacao.Documentos.Fases[0].Documentos.Select(static d => (d.ExigenciaId, d.Finalidade, d.Situacao)).Should().Equal(
            [(documentos.DaInscricao.Id, "INSCRICAO", "PENDENTE"), (documentos.DaIsencao.Id, "ISENCAO_TAXA", "PODE_SER_EXIGIDO")],
            "a isenção na mesma fase aparece como da isenção, e não como exigência da inscrição");
        avaliacao.Documentos.Fases[1].Documentos.Select(static d => d.Situacao).Should().Equal("DEPENDE_DE_FASE_POSTERIOR");
    }

    [Fact(DisplayName = "Com a fase pedida, vêm só os documentos dela; fase fora do cronograma da versão é não encontrada")]
    public void ComFase_SoAFase_FaseDesconhecida_NaoEncontrada()
    {
        (DocumentoExigido, SituacaoDoDocumento)[] documentos =
        [
            (Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao), SituacaoDoDocumento.Pendente),
            (Documento(FaseDaHabilitacao, FinalidadeFormulario.Habilitacao), SituacaoDoDocumento.Pendente),
        ];

        Projetar(documentos, FaseDaHabilitacao.Id).Value!.Documentos.Fases.Select(static f => f.Codigo).Should().Equal("HABILITACAO");
        Projetar(documentos, Guid.CreateVersion7()).Error!.Code.Should().Be(InscricaoErrorCodes.FaseNaoEncontrada);
    }

    [Fact(DisplayName = "O modelo da exigência vem com o endereço no acervo do ato que publicou a versão")]
    public void Modelo_ComEnderecoDoAtoDaVersao()
    {
        ModeloDaExigencia modelo = new(Guid.CreateVersion7(), "Autodeclaração.odt", FormatoDeDocumento.Odt, new string('a', 64));

        DocumentoDaInscricaoDto documento = Projetar(
            [(Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao, modelo), SituacaoDoDocumento.Pendente)], faseId: null)
            .Value!.Documentos.Fases[0].Documentos.Single();

        documento.Modelo!.UrlDownload!.ToString().Should().Contain($"/atos/{AtoId:D}/");
        documento.Modelo.NomeArquivo.Should().Be("Autodeclaração.odt");
    }

    [Fact(DisplayName = "Com a fase pedida, a pendência de outra fase não vem: ela não muda nada na fase pedida")]
    public void ComFase_SemPendenciaDeOutraFase()
    {
        (DocumentoExigido, SituacaoDoDocumento)[] documentos =
        [
            (Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao), SituacaoDoDocumento.Pendente),
            (Documento(FaseDaHabilitacao, FinalidadeFormulario.Habilitacao), SituacaoDoDocumento.PodeSerExigido),
        ];
        Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>> pendencias = new()
        {
            [FaseDaHabilitacao.Id] = [new PendenciaDaAvaliacao("PEDIU_FATOR", "Pediu o Fator?", "DADOS")],
        };

        Projetar(documentos, FaseDaInscricao.Id, pendencias).Value!.Documentos.Fases.Single().Pendencias.Should().BeEmpty();
        Projetar(documentos, faseId: null, pendencias).Value!.Documentos.Fases.Single(static f => f.Codigo == "HABILITACAO")
            .Pendencias.Select(static p => p.FatoCodigo).Should().Equal("PEDIU_FATOR");
    }

    [Fact(DisplayName = "O documento traz ao portal os formatos, o limite efetivo, a quantidade mínima e os arquivos enviados da própria exigência e ocorrência")]
    public void Documento_TrazOQueOPortalPrecisaParaEnviar()
    {
        ExigenciasComArquivoEnviado cenario = ExigenciasComArquivoEnviado.Novas();

        IReadOnlyList<DocumentoDaInscricaoDto> documentos = cenario.DocumentosDaFaseDaInscricao();

        DocumentoDaInscricaoDto projetado = documentos.Single(d => d.ExigenciaId == cenario.Laudo.Id);
        projetado.Situacao.Should().Be("SATISFEITA");
        projetado.FormatosAceitos.Should().Equal("PDF", "JPEG");
        projetado.TamanhoMaximoBytes.Should().Be(1_000_000, "o menor limite efetivo entre os formatos aceitos");
        projetado.QuantidadeMinima.Should().Be(1);
        projetado.Arquivos.Should().Equal(cenario.ArquivoDoLaudo);
        documentos.Single(d => d.ExigenciaId == cenario.Outro.Id).FormatosAceitos.Should().Equal("PDF", "JPEG", "PNG");
        documentos.Single(d => d.ExigenciaId == cenario.Outro.Id).TamanhoMaximoBytes.Should().Be(ArquivoDaApresentacao.TetoDoSistemaBytes);
    }

    private static Result<AvaliacaoDaInscricaoDto> Projetar(
        IEnumerable<(DocumentoExigido Documento, SituacaoDoDocumento Situacao)> documentos,
        Guid? faseId,
        IReadOnlyDictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>? pendencias = null,
        IReadOnlyList<ArquivoDaApresentacaoDto>? arquivos = null)
    {
        ResultadoDaAvaliacaoDaInscricao resultado = ResultadoDaAvaliacao(documentos, pendencias);
        DocumentosDoAtoNoAcervo acervo = new(
            ProcessoId, AtoId, static chave => new Uri($"https://acervo.teste/{chave}"), Copiados: true);

        return ProjecaoDaAvaliacaoDaInscricao.Projetar(resultado, ContextoDeTeste(), acervo, arquivos ?? [], faseId);
    }

    private static ResultadoDaAvaliacaoDaInscricao ResultadoDaAvaliacao(
        IEnumerable<(DocumentoExigido Documento, SituacaoDoDocumento Situacao)> documentos,
        IReadOnlyDictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>? pendencias) =>
        FormularioDeTeste.AvaliacaoSemOcultos with
        {
            Documentos = new DocumentosDaInscricao(
                [.. documentos.Select(static d => new DocumentoAvaliado(d.Documento, d.Situacao, null, null, [], null))],
                pendencias ?? new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), []),
        };

    private static ContextoDaVersaoFixada ContextoDeTeste()
    {
        ConfiguracaoAvaliavel configuracao = new(
            new DefinicaoFormulario([], [], []),
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            RegrasDeComprovacaoDaVersao.DaConfiguracaoPublicada([]),
            [], [], [],
            [FaseDaHabilitacao, FaseDaInscricao],
            []);

        return new ContextoDaVersaoFixada(
            [], null, new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal), RespostasAdmitidasVazias(),
            configuracao, AtoId, configuracao, null, []);
    }

    private static RespostasAdmitidas RespostasAdmitidasVazias() =>
        new(
            new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
            new HashSet<Guid>(),
            new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal));

    private static DocumentoExigido Documento(FaseCronograma fase, FinalidadeFormulario finalidade, ModeloDaExigencia? modelo = null) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "COMPROVANTE", "Comprovante", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(fase.Id, null, finalidade))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, modelo, null, null))
            .ComFatos(null)
            .Criar().Value!;

    private static FaseCronograma Fase(int ordem, string codigo) =>
        FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            Guid.CreateVersion7(),
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: ordem == 1, ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([], EmiteParecerIndividual: false, RegraRecurso: null),
            []);

    /// <summary>Os quatro documentos do cenário: dois da inscrição (um deles de isenção), um dispensado e um da habilitação.</summary>
    private sealed record DocumentosDasDuasFases(
        DocumentoExigido DaInscricao, DocumentoExigido DaIsencao, DocumentoExigido Dispensado, DocumentoExigido DaHabilitacao)
    {
        internal static DocumentosDasDuasFases Novos() => new(
            Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao),
            Documento(FaseDaInscricao, FinalidadeFormulario.IsencaoTaxa),
            Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao),
            Documento(FaseDaHabilitacao, FinalidadeFormulario.Habilitacao));

        internal (DocumentoExigido, SituacaoDoDocumento)[] ComAsSituacoesDaAvaliacao() =>
        [
            (DaHabilitacao, SituacaoDoDocumento.DependeDeFasePosterior),
            (DaInscricao, SituacaoDoDocumento.Pendente),
            (DaIsencao, SituacaoDoDocumento.PodeSerExigido),
            (Dispensado, SituacaoDoDocumento.NaoExigido),
        ];
    }

    /// <summary>Uma exigência com formatos e limites próprios e com arquivo enviado, ao lado de outra sem nenhum dos dois.</summary>
    private sealed record ExigenciasComArquivoEnviado(DocumentoExigido Laudo, DocumentoExigido Outro, ArquivoDaApresentacaoDto ArquivoDoLaudo)
    {
        internal static ExigenciasComArquivoEnviado Novas()
        {
            DocumentoExigido laudo = new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "LAUDO", "Laudo", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(FaseDaInscricao.Id, null, FinalidadeFormulario.Inscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(false, [("PDF", 1_000_000), ("JPEG", 3_000_000)]).Value!, 2_000_000, null, null, null))
            .ComFatos(null)
            .Criar().Value!;
            ArquivoDaApresentacaoDto arquivo = new(Guid.CreateVersion7(), Guid.CreateVersion7(), laudo.Id, null, "application/pdf", 10, new string('a', 64), DateTimeOffset.UnixEpoch, SituacaoDoArquivoDoCandidato.Verificado, null, "Arquivo 1", null);

            return new ExigenciasComArquivoEnviado(laudo, Documento(FaseDaInscricao, FinalidadeFormulario.Inscricao), arquivo);
        }

        internal IReadOnlyList<DocumentoDaInscricaoDto> DocumentosDaFaseDaInscricao() =>
            Projetar(
                [(Laudo, SituacaoDoDocumento.Satisfeita), (Outro, SituacaoDoDocumento.Pendente)],
                faseId: null,
                arquivos: [ArquivoDoLaudo, ArquivoDoLaudo with { ArquivoId = Guid.CreateVersion7(), ExigenciaId = Outro.Id }])
                .Value!.Documentos.Fases.Single(static f => f.Codigo == "INSCRICAO").Documentos;
    }
}
