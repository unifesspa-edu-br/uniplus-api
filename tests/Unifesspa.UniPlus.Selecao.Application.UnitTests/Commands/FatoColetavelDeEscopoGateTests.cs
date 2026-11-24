namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um fato coletável de SELEÇÃO (CONDICAO_ATENDIMENTO/TIPO_DEFICIENCIA) sem
/// nenhum valor ofertado publicaria um seletor sem opção nenhuma para o candidato escolher.
/// Prova, nos <b>três</b> handlers que congelam (ao lado de <see cref="ColetabilidadeDeFatosGateTests"/>
/// e <see cref="ConformidadeLegalGateTests"/>), que o gate recusa ANTES da canonicalização.
/// </summary>
public sealed class FatoColetavelDeEscopoGateTests
{
    private static readonly string HashFixo = ProcessoSeletivoConformeBuilder.HashFixo;
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static FatoColetado FatoCondicaoAtendimentoSemOferta() => FatoColetadoDeTeste.Coletando("CONDICAO_ATENDIMENTO", 0)
        .ComRotulo("Você se enquadra em alguma condição de atendimento?")
        .DoTipo(TipoRenderizacao.EscolhaMultipla)
        .Construir();

    [Fact(DisplayName = "Publicar_ComFatoColetavelSemOferta_RecusaSemCanonicalizar — o gate precede a canonicalização")]
    public async Task Publicar_ComFatoColetavelSemOferta_RecusaSemCanonicalizar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        // ProcessoSeletivoConformeBuilder já oferta atendimento vazio (sem condições) — só falta o fato coletável.
        processo.DefinirItens([FatoCondicaoAtendimentoSemOferta()])
            .IsSuccess.Should().BeTrue();
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> eventos) = await ServicosSemOferta(processo, canonicalizer).PublicarAsync(processo.Id);

        AfirmarRecusaDoFatoSemOferta(resposta, eventos, canonicalizer);
        processo.Status.Should().Be(StatusProcesso.Rascunho, "a publicação recusada não transita o processo");
    }

    private static ServicosDoCongelamentoDeTeste ServicosSemOferta(ProcessoSeletivo processo, ISnapshotPublicacaoCanonicalizer canonicalizer) =>
        ServicosDoCongelamentoDeTeste.Para(processo)
            .ComCanonicalizer(canonicalizer)
            .ComCatalogoDeFatos(Substitute.For<IFatoCandidatoReader>())
            .ComCalendario(Substitute.For<ICalendarioVigenteReader>());

    private static void AfirmarRecusaDoFatoSemOferta(Result resposta, IEnumerable<object> eventos, ISnapshotPublicacaoCanonicalizer canonicalizer)
    {
        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════════

    private static ISnapshotPublicacaoCanonicalizer CanonicalizerSubstituto()
    {
        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Any<EntradaCanonicalizacao>())
            .Returns(new SnapshotCanonico(EnvelopeProjetavelDeTeste.Bytes, "1.1", "canonical-json/sha256@v1"));
        return canonicalizer;
    }

    private static IProcessoSeletivoRepository RepositorioDoProcesso(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repositorio;
    }

    private static IDocumentoAPublicarRepository RepositorioDeDocumento(Guid processoSeletivoId)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoSeletivoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoAPublicarRepository repositorio = Substitute.For<IDocumentoAPublicarRepository>();
        repositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(documento);
        return repositorio;
    }

    private static ITipoAtoPublicadoReader TipoDeAtoReader()
    {
        ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>();
        reader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA",
                Nome: "Edital de abertura",
                CongelaConfiguracao: true,
                UnicoPorObjeto: false,
                EfeitoIrreversivel: false,
                EhResultado: false));
        return reader.ComQualquerTipoDeclarado();
    }

    private static IUserContext UsuarioAutenticado()
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.UserId.Returns("user-conta-1");
        return contexto;
    }

    private static DadosDoAto NovoAto() => new(
        Orgao: "CEPS",
        Serie: "EDITAL",
        Ano: 2026,
        DataDoAto: new DateOnly(2026, 1, 1),
        Assinante: "Diretor do CEPS",
        TipoAtoCodigo: "EDITAL_ABERTURA");

    /// <summary>Processo publicado SEM FatoColetado — o fato problemático é acrescentado depois, na retificação.</summary>
    private static (ProcessoSeletivo Processo, VersaoConfiguracao VersaoAtual) ProcessoPublicadoSemFatoColetado()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

        VersaoConfiguracao versao = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(EnvelopeCongeladoDeTeste.SemAnexos(), "1.1", "canonical-json/sha256@v1", HashFixo), AtorUsuarioContaId = "user-conta-1", Relogio = new RelogioFixo(Agora) }).Value!;
        processo.DequeueDomainEvents();

        return (processo, versao);
    }

    private static ProcessoSeletivo NovoProcessoConforme() =>
        ProcessoSeletivoConformeBuilder.Criar("PS Gate Fato Coletável de Escopo");

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
