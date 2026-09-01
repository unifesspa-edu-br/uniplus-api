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
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura do congelamento de metadado de fato (Story #919, RN08) em
/// <see cref="PublicarProcessoSeletivoCommandHandler"/>: o resultado da resolução alimenta
/// <see cref="EntradaCanonicalizacao.MetadadosFatosCongelados"/>, e um código de fato que não
/// resolve no catálogo aborta a publicação com um erro nomeado ANTES de canonicalizar.
/// </summary>
/// <remarks>
/// Desde a issue #1059 (D4-bis), <see cref="IFatoCandidatoReader.ListarAsync"/> é consultado
/// UMA vez por congelamento, sempre — mesmo sem condição de gatilho — porque o gate de valor
/// inativo do domínio precisa do catálogo inteiro independente de o processo ter fato coletado
/// ou gatilho. É esse catálogo compartilhado, não mais <c>ObterPorCodigoAsync</c> por código,
/// que <see cref="ResolvedorMetadadosFatosCongelados"/> agora consulta.
/// </remarks>
public sealed class PublicarProcessoSeletivoCommandHandlerTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static ProcessoSeletivo NovoProcessoConforme(out Guid faseId) =>
        ProcessoSeletivoConformeBuilder.Criar("PS Metadado de Fato", out faseId);

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
            condicoes: [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!],
            basesLegais: [DocumentoExigidoBaseLegal.Criar(
                "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!],
            idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
            tamanhoMaximoBytes: null).Value!;

    private static FatoCandidatoView FatoModalidade() => new(
        Id: Guid.CreateVersion7(),
        Codigo: "MODALIDADE",
        Nome: "Modalidade de concorrência",
        Descricao: null,
        Dominio: "CATEGORICO",
        Origem: "DERIVADO",
        Cardinalidade: "MULTIVALORADO",
        ValoresDominio: null,
        PontoResolucao: "INSCRICAO",
        Binding: "REGRA_DERIVACAO:MODALIDADE",
        ValoresDominioDeclarados: null);

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

    /// <summary>
    /// A leitura do calendário vigente é <b>uma por handler</b>: a mesma resposta alimenta o gate
    /// da raiz e o bloco congelado do envelope. Duas leituras abririam a janela em que o dataset
    /// muda entre validar e congelar, e a versão publicada carregaria um calendário que o gate
    /// não aprovou — defeito que nenhuma asserção sobre o resultado pegaria, porque num teste
    /// comum as duas leituras devolveriam o mesmo valor.
    /// </summary>
    [Fact(DisplayName = "O handler lê o calendário vigente uma única vez")]
    public async Task Handle_LeCalendarioUmaUnicaVez()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo);
        var reader = CalendarioVigenteReaderDeTeste.ComVigente();

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento, reader);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        reader.Leituras.Should().Be(1);
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> HandleAsync(
        Mocks mocks, ProcessoSeletivo processo, DocumentoEdital documento,
        ICalendarioVigenteReader? calendarioReader = null)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns("user-sub-1");

        return PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", null, null,
                DocumentoEditalId: documento.Id,
                Ato: new DadosDoAto(
                    Orgao: "CEPS", Serie: "EDITAL", Ano: 2026, DataPublicacao: new DateOnly(2026, 1, 1),
                    Assinante: "Diretor do CEPS", TipoAtoCodigo: "EDITAL_ABERTURA")),
            mocks.ProcessoRepository,
            mocks.DocumentoRepository,
            mocks.Canonicalizer, new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            userContext,
            mocks.TipoDeAtoReader,
            mocks.VagaDeLinhagemReader,
            mocks.ObrigatoriedadeLegalRepository,
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            mocks.FatoCandidatoReader,
            calendarioReader ?? CalendarioVigenteReaderDeTeste.SemVigente(),
            TimeProvider.System,
            CancellationToken.None);
    }

    [Fact(DisplayName = "Sem condição de gatilho, MetadadosFatosCongelados é null mesmo com o catálogo consultado")]
    public async Task Handle_SemCondicaoDeGatilho_MetadadosENulo()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo, e => entradaCapturada = e);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.MetadadosFatosCongelados.Should().BeNull(
            "nenhuma condição de gatilho existe no processo — nada a congelar, mesmo o catálogo tendo sido lido " +
            "para o gate de valor inativo (D4-bis)");
        _ = await mocks.FatoCandidatoReader.Received(1).ListarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com condição de gatilho resolvida, MetadadosFatosCongelados carrega o metadado do fato citado")]
    public async Task Handle_ComCondicaoDeGatilhoResolvida_ResolveMetadadoDoFato()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, "MODALIDADE"), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        EntradaCanonicalizacao? entradaCapturada = null;
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo, e => entradaCapturada = e);
        mocks.FatoCandidatoReader.ListarAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<FatoCandidatoView>)[FatoModalidade()]);

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entradaCapturada.Should().NotBeNull();
        entradaCapturada!.MetadadosFatosCongelados.Should().NotBeNull();
        entradaCapturada.MetadadosFatosCongelados.Should().ContainKey("MODALIDADE");
        MetadadoFatoCongelado metadado = entradaCapturada.MetadadosFatosCongelados!["MODALIDADE"];
        metadado.Dominio.Should().Be("CATEGORICO");
        metadado.Origem.Should().Be("DERIVADO");
        metadado.Cardinalidade.Should().Be("MULTIVALORADO");
        metadado.PontoResolucao.Should().Be("INSCRICAO");
        metadado.Binding.Should().Be("REGRA_DERIVACAO:MODALIDADE");
        metadado.ValoresDominioDeclarados.Should().BeNull("MODALIDADE é escopo-processo — os valores vêm da oferta do processo, não de FatoValorDominio");
    }

    [Fact(DisplayName = "Código de fato que não resolve no catálogo aborta a publicação ANTES de canonicalizar")]
    public async Task Handle_CodigoDeFatoNaoResolve_AbortaAntesDeCanonicalizar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out Guid faseId);
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilhoPorFato(faseId, "FATO_INEXISTENTE"), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        // O catálogo (D4-bis) não conhece "FATO_INEXISTENTE" — a mesma lista vazia que
        // NovosMocks já produz por padrão (IFatoCandidatoReader substituto sem ListarAsync
        // configurado devolve coleção vazia).
        (Mocks mocks, DocumentoEdital documento) = NovosMocks(processo);

        (Result resposta, IEnumerable<object> eventos) = await HandleAsync(mocks, processo, documento);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be("ProcessoSeletivo.FatoCongeladoNaoEncontrado");
        _ = mocks.Canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
        eventos.Should().BeEmpty();
        processo.Status.Should().Be(StatusProcesso.Rascunho, "a publicação recusada não transita o processo");
    }
}
