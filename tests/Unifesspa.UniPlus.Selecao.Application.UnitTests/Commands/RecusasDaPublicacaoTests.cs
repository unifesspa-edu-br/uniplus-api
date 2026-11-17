namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// <b>Contraprova do CA-10 (ADR-0109 D5):</b> a conferência da configuração precede a canonicalização.
/// </summary>
/// <remarks>
/// <para>
/// Um processo não conforme <b>não chega a ser projetado</b>. O canonicalizador
/// substituto registra a invocação — se ele for chamado, o teste falha.
/// </para>
/// <para>
/// Por que importa: sem a conferência antecipada, a projeção de uma dimensão obrigatória
/// ausente <b>lançaria</b> (ADR-0109 D8) em vez de devolver o <c>DomainError</c> que
/// o contrato HTTP promete. O 422 viraria 500.
/// </para>
/// </remarks>
public sealed class RecusasDaPublicacaoTests
{
    /// <summary>Canonicalizador espião — registra se foi invocado.</summary>
    private sealed class CanonicalizerEspiao : ISnapshotPublicacaoCanonicalizer
    {
        public bool FoiInvocado { get; private set; }

        public SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada)
        {
            FoiInvocado = true;
            return new SnapshotCanonico(EnvelopeProjetavelDeTeste.Bytes, "1.1", "canonical-json/sha256@v1");
        }
    }

    [Fact(DisplayName = "Publicar_ProcessoNaoConforme_NaoCanonicaliza — a conferência precede a projeção (CA-10)")]
    public async Task Publicar_ProcessoNaoConforme_NaoCanonicaliza()
    {
        CanonicalizerEspiao canonicalizer = new();
        PublicacaoSobConferencia conferencia = new(ProcessoNaoConformeQueCobraTaxaSemMotivo(), canonicalizer, TipoDeAberturaDoEdital());
        (Result resposta, IEnumerable<object> eventos) = await conferencia.PublicarAsync("EDITAL_ABERTURA", PeriodoDoAtoInicio, PeriodoDoAtoFim);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            "o contrato HTTP não muda — continua sendo o mesmo 422 de sempre");
        resposta.Errors.Select(static e => e.Error.Code).Should().ContainInOrder(
            "ProcessoSeletivo.ConformidadeInsuficiente", MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado);
        canonicalizer.FoiInvocado.Should().BeFalse(
            "um processo não conforme NÃO chega a ser canonicalizado (ADR-0109 D5). Sem a conferência antecipada, a " +
            "projeção de uma dimensão obrigatória ausente lançaria e o 422 viraria 500.");
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
            ReferenciaTemporalDosFatos.ResolverDataReferenciaFatos(entrada.Processo.Declaracao(), TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));
            return new SnapshotCanonico(EnvelopeProjetavelDeTeste.Bytes, "1.2", "canonical-json/sha256@v1");
        }
    }

    private static DocumentoExigido ExigenciaPorFaixaEtaria(Guid faseId) => DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: CadastrosVivos.IdentidadeDe("LAUDO_MEDICO"),
            tipoDocumentoCodigo: "DECLARACAO_MAIORIDADE",
            tipoDocumentoNome: "Declaração de maioridade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [CondicaoDeMaioridade()], basesLegais: [BaseLegalDaLeiDeCotas()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    private static CondicaoGatilho CondicaoDeMaioridade() => CondicaoGatilho.Criar(
        0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;

    private static DocumentoExigidoBaseLegal BaseLegalDaLeiDeCotas() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    /// <summary>Processo conforme (etapas, oferta, distribuição, classificação, cronograma) — o mínimo para passar pela conferência do estado e pelo ato.</summary>
    private static ProcessoSeletivo NovoProcessoConformeComGatilhoPorFaixaEtariaSemReferencia(out Guid faseId)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS referência temporal", out faseId);

        DocumentoExigido exigencia = ExigenciaPorFaixaEtaria(faseId);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!]).IsSuccess.Should().BeTrue();

        // Nenhuma ReferenciaTemporalFatos configurada — o gatilho FAIXA_ETARIA a exige.

        return processo;
    }

    [Fact(DisplayName = "Publicar_AtoDaPublicacaoDeTipoUnicoDeclaradoNoCronograma_RecusaAntesDeCanonicalizar")]
    public async Task Publicar_AtoDaPublicacaoDeTipoUnicoDeclaradoNoCronograma_RecusaAntesDeCanonicalizar()
    {
        // O cronograma do builder declara RESULTADO_CLASSIFICACAO como produto; o ato desta publicação é do
        // mesmo tipo, que o catálogo declara único: duas declarações para uma única vaga.
        CanonicalizerEspiao canonicalizer = new();
        PublicacaoSobConferencia conferencia = new(ProcessoSeletivoConformeBuilder.Criar("PS Tipo único"), canonicalizer, TipoUnicoDeResultadoDaClassificacao());

        (Result resposta, IEnumerable<object> eventos) = await conferencia.PublicarAsync("RESULTADO_CLASSIFICACAO");

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez");
        canonicalizer.FoiInvocado.Should().BeFalse("a recusa sai antes da canonicalização, como as demais pendências do cronograma");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Publicar_ExigenciaComGatilhoPorFaixaEtariaSemReferenciaTemporal_NaoCanonicalizaAntesDoGuard — a conferência da referência temporal também precede a canonicalização (ADR-0109 D5)")]
    public async Task Publicar_ExigenciaComGatilhoPorFaixaEtariaSemReferenciaTemporal_NaoCanonicalizaAntesDoGuard()
    {
        CanonicalizerQueResolveReferenciaTemporalFatos canonicalizer = new();
        PublicacaoSobConferencia conferencia = new(NovoProcessoConformeComGatilhoPorFaixaEtariaSemReferencia(out _), canonicalizer, TipoDeAberturaDoEdital());

        (Result resposta, IEnumerable<object> eventos) = await conferencia.PublicarAsync("EDITAL_ABERTURA");

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.ReferenciaTemporalFatosAusente");
        canonicalizer.FoiInvocado.Should().BeFalse(
            "a conferência da referência temporal precede a canonicalização — sem ele, " +
            "ResolverDataReferenciaFatos(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)) lançaria dentro do canonicalizador e a publicação " +
            "devolveria 500 em vez do 422 nomeado.");
        eventos.Should().BeEmpty();
    }

    private static readonly DateTimeOffset PeriodoDoAtoInicio = new(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset PeriodoDoAtoFim = new(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3));

    // Cobra a taxa com um fundamento e não escolheu motivo: a recusa estrutural da conferência da configuração vem antes da
    // dos motivos, na mesma resposta: nenhuma fase declara o tipo de isenção. Sem cronograma, não há fase que colete inscrição:
    // o período continua vindo do ato.
    private static ProcessoSeletivo ProcessoNaoConformeQueCobraTaxaSemMotivo()
    {
        // Processo em rascunho, SEM nenhuma dimensão configurada — não conforme.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Vazio", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(true, 100m, [FundamentoIsencaoCodigo.CadastroUnico]).Value!);

        return processo;
    }

    // O ato e o documento, que precedem a conferência da configuração, têm de PASSAR — senão o teste
    // provaria a recusa errada e o canonicalizador ficaria sem ser chamado por outro motivo.
    private static ITipoAtoPublicadoReader TipoDeAberturaDoEdital()
    {
        ITipoAtoPublicadoReader tipoDeAtoReader = Substitute.For<ITipoAtoPublicadoReader>().ComQualquerTipoDeclarado();
        tipoDeAtoReader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA",
                Nome: "Edital de abertura",
                CongelaConfiguracao: true,
                UnicoPorObjeto: true,
                EfeitoIrreversivel: false,
                EhResultado: false));

        return tipoDeAtoReader;
    }

    private static ITipoAtoPublicadoReader TipoUnicoDeResultadoDaClassificacao()
    {
        TipoAtoPublicadoView tipo = new("RESULTADO_CLASSIFICACAO", "Resultado da classificação", CongelaConfiguracao: true, UnicoPorObjeto: true, EfeitoIrreversivel: false, EhResultado: true);
        ITipoAtoPublicadoReader tipoDeAtoReader = Substitute.For<ITipoAtoPublicadoReader>().ComQualquerTipoDeclarado();
        tipoDeAtoReader.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(tipo);
        tipoDeAtoReader.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([tipo]);

        return tipoDeAtoReader;
    }

    /// <summary>O processo, o documento do edital confirmado e as portas que a publicação lê, para o ato de abertura.</summary>
    private sealed class PublicacaoSobConferencia
    {
        private readonly ProcessoSeletivo _processo;
        private readonly DocumentoAPublicar _documento;

        public PublicacaoSobConferencia(ProcessoSeletivo processo, ISnapshotPublicacaoCanonicalizer canonicalizer, ITipoAtoPublicadoReader tipoDeAtoReader)
        {
            _processo = processo;
            _documento = DocumentoAPublicar.IniciarPendente(
                processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
            _documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
            Servicos = new ServicosDoCongelamentoDeTeste()
                .ComRepositorio(RepositorioQueDevolve(processo))
                .ComDocumentos(RepositorioDoDocumento(_documento))
                .ComCanonicalizer(canonicalizer)
                .ComUsuario(UsuarioDeTeste())
                .ComTiposDeAto(tipoDeAtoReader)
                .ComObrigatoriedadesLegais(ObrigatoriedadesSemRegraVigente())
                .ComCatalogoDeFatos(Substitute.For<IFatoCandidatoReader>())
                .ComCalendario(Substitute.For<ICalendarioVigenteReader>())
                .ComRelogio(TimeProvider.System);
        }

        public ServicosDoCongelamentoDeTeste Servicos { get; }

        public Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarAsync(
            string tipoAtoCodigo, DateTimeOffset? periodoInicio = null, DateTimeOffset? periodoFim = null) =>
            Servicos.PublicarAsync(new PublicarProcessoSeletivoCommand(
                ProcessoSeletivoId: _processo.Id,
                Numero: "001/2026",
                PeriodoInscricaoInicio: periodoInicio,
                PeriodoInscricaoFim: periodoFim,
                DocumentoEditalId: _documento.Id,
                Ato: new DadosDoAto(
                    Orgao: "CEPS",
                    Serie: "EDITAL",
                    Ano: 2026,
                    DataDoAto: new DateOnly(2026, 1, 1),
                    Assinante: "Diretor do CEPS",
                    TipoAtoCodigo: tipoAtoCodigo)));

        private static IProcessoSeletivoRepository RepositorioQueDevolve(ProcessoSeletivo processo)
        {
            IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
            repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

            return repositorio;
        }

        private static IDocumentoAPublicarRepository RepositorioDoDocumento(DocumentoAPublicar documento)
        {
            IDocumentoAPublicarRepository repositorio = Substitute.For<IDocumentoAPublicarRepository>();
            repositorio.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);

            return repositorio;
        }

        private static IUserContext UsuarioDeTeste()
        {
            IUserContext usuario = Substitute.For<IUserContext>();
            usuario.UserId.Returns("teste");

            return usuario;
        }

        private static IObrigatoriedadeLegalRepository ObrigatoriedadesSemRegraVigente()
        {
            IObrigatoriedadeLegalRepository repositorio = Substitute.For<IObrigatoriedadeLegalRepository>();
            repositorio.ObterVigentesParaTipoProcessoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);

            return repositorio;
        }
    }
}
