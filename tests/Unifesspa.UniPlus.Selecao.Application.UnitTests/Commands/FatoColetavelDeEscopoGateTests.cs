namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

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
/// Issue #1077: um fato coletável de SELEÇÃO (CONDICAO_ATENDIMENTO/TIPO_DEFICIENCIA) sem
/// nenhum valor ofertado publicaria um seletor sem opção nenhuma para o candidato escolher.
/// Prova, nos <b>três</b> handlers que congelam (ao lado de <see cref="ColetabilidadeDeFatosGateTests"/>
/// e <see cref="ConformidadeLegalGateTests"/>), que o gate recusa ANTES da canonicalização.
/// </summary>
public sealed class FatoColetavelDeEscopoGateTests
{
    private static readonly string HashFixo = ProcessoSeletivoConformeBuilder.HashFixo;
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static FatoColetado FatoCondicaoAtendimentoSemOferta() => FatoColetado.Criar(
        "CONDICAO_ATENDIMENTO", 0, "Você se enquadra em alguma condição de atendimento?",
        TipoRenderizacao.SelecaoMultipla, obrigatorio: false, null).Value!;

    [Fact(DisplayName = "Publicar_ComFatoColetavelSemOferta_RecusaSemCanonicalizar — o gate precede a canonicalização")]
    public async Task Publicar_ComFatoColetavelSemOferta_RecusaSemCanonicalizar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        // ProcessoSeletivoConformeBuilder já oferta atendimento vazio (sem condições) — só falta o fato coletável.
        processo.DefinirFatosColetados([FatoCondicaoAtendimentoSemOferta()], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, IEnumerable<object> eventos) = await PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            RepositorioDoProcesso(processo),
            RepositorioDeDocumento(processo.Id),
            canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            Substitute.For<IFatoCandidatoReader>(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Status.Should().Be(StatusProcesso.Rascunho, "a publicação recusada não transita o processo");
    }

    // RetificarProcessoSeletivoCommandHandler não aceita novo FatoColetado/OfertaAtendimento
    // como input — re-versiona o estado JÁ existente do agregado sob um novo ato, e exige
    // Rascunho nulo (abre e fecha uma sessão efêmera internamente). Como este gate é interno
    // ao agregado (sem leitura cross-módulo, ao contrário da coletabilidade de fatos), não há
    // "drift" possível entre a publicação original e uma chamada de Retificar sem sessão
    // aberta: se o estado violasse o gate, a publicação original já teria recusado. O caminho
    // válido para reabrir e violar o gate é a sessão de retificação explícita — coberto abaixo
    // por FecharRetificacaoCommandHandler, que também fecha por SucederVersao (mesmo gate).

    [Fact(DisplayName = "FecharRetificacao_ComFatoColetavelSemOferta_RecusaESessaoPermaneceAberta — recusa não destrói a sessão editorial")]
    public async Task FecharRetificacao_ComFatoColetavelSemOferta_RecusaESessaoPermaneceAberta()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicadoSemFatoColetado();

        Result<RascunhoRetificacao> rascunho = processo.AbrirRetificacao(
            "Correção do prazo", versaoAtual, "user-sub-1", Agora);
        rascunho.IsSuccess.Should().BeTrue(rascunho.Error?.Message);
        processo.DequeueDomainEvents();

        processo.DefinirFatosColetados([FatoCondicaoAtendimentoSemOferta()], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        IProcessoSeletivoRepository repositorio = RepositorioDoProcesso(processo);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoAtual);

        (Result resposta, IEnumerable<object> eventos) = await FecharRetificacaoCommandHandler.Handle(
            new FecharRetificacaoCommand(
                processo.Id, "001/2026-R1", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga),
            repositorio,
            RepositorioDeDocumento(processo.Id),
            canonicalizer,
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            Substitute.For<IFatoCandidatoReader>(),
            new RelogioFixo(Agora),
            CancellationToken.None);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoColetadoSemValoresOfertados");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Rascunho.Should().NotBeNull(
            "uma recusa de fato coletável sem oferta não destrói a sessão editorial — o administrador corrige e tenta de novo");
    }

    // ══════════════════════════════════════════════════════════════════════════════

    private static ISnapshotPublicacaoCanonicalizer CanonicalizerSubstituto()
    {
        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Any<EntradaCanonicalizacao>())
            .Returns(new SnapshotCanonico("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1"));
        return canonicalizer;
    }

    private static IProcessoSeletivoRepository RepositorioDoProcesso(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repositorio;
    }

    private static IDocumentoEditalRepository RepositorioDeDocumento(Guid processoSeletivoId)
    {
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            processoSeletivoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        IDocumentoEditalRepository repositorio = Substitute.For<IDocumentoEditalRepository>();
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
                EfeitoIrreversivel: false));
        return reader;
    }

    private static IUserContext UsuarioAutenticado()
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.UserId.Returns("user-sub-1");
        return contexto;
    }

    private static DadosDoAto NovoAto() => new(
        Orgao: "CEPS",
        Serie: "EDITAL",
        Ano: 2026,
        DataPublicacao: new DateOnly(2026, 1, 1),
        Assinante: "Diretor do CEPS",
        TipoAtoCodigo: "EDITAL_ABERTURA");

    /// <summary>Processo publicado SEM FatoColetado — o fato problemático é acrescentado depois, na retificação.</summary>
    private static (ProcessoSeletivo Processo, VersaoConfiguracao VersaoAtual) ProcessoPublicadoSemFatoColetado()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        DadosEdital dados = DadosEdital.Criar(
            "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.CreateVersion7()).Value!;

        VersaoConfiguracao versao = processo.Publicar(
            dados, "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "user-sub-1",
            new RelogioFixo(Agora)).Value!;
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
