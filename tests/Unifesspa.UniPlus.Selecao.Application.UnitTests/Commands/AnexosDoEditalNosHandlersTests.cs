namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static ConformidadeLegalGateTests;

/// <summary>
/// Os anexos chegam ao envelope congelado: o handler da publicação entrega ao canonicalizador os
/// anexos pedidos, e o da retificação herda os da versão vigente quando o pedido não traz a lista.
/// O canonicalizador escreve em <c>hashesEdital.anexos</c> exatamente o que recebe em
/// <c>DadosEdital.Anexos</c>, o que a golden fixture e o round-trip do envelope provam.
/// </summary>
public sealed class AnexosDoEditalNosHandlersTests
{
    [Fact(DisplayName = "Publicar com dois anexos válidos entrega os dois, na ordem pedida, ao envelope")]
    public async Task Publicar_ComDoisAnexos_EntregaOsDoisAoCanonicalizador()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DocumentoAPublicar quadro = Anexo(processo.Id, "quadro", TiposDeMidiaDeDocumento.Pdf, 2048);
        DocumentoAPublicar formulario = Anexo(processo.Id, "formulario", TiposDeMidiaDeDocumento.Docx, 4096);
        EntradaCanonicalizacao? entrada = null;

        (Result resposta, _) = await PublicarAsync(
            processo, RepositorioDeDocumento(processo.Id, formulario, quadro), CanonicalizerSubstituto(e => entrada = e),
            [new AnexoSolicitadoDoEdital(quadro.Id, "Quadro de vagas"), new AnexoSolicitadoDoEdital(formulario.Id, "Formulário")]);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entrada.Should().NotBeNull();
        entrada!.Dados.Anexos.Select(static a => (a.DocumentoId, a.NomeArquivo, a.TipoDeMidia, a.TamanhoBytes, a.HashSha256)).Should().Equal(
            (quadro.Id, "Quadro de vagas.pdf", TiposDeMidiaDeDocumento.Pdf, 2048L, quadro.HashSha256!),
            (formulario.Id, "Formulário.docx", TiposDeMidiaDeDocumento.Docx, 4096L, formulario.HashSha256!));
    }

    [Fact(DisplayName = "Publicar sem anexos congela a lista vazia")]
    public async Task Publicar_SemAnexos_CongelaListaVazia()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        EntradaCanonicalizacao? entrada = null;

        (Result resposta, _) = await PublicarAsync(
            processo, RepositorioDeDocumento(processo.Id), CanonicalizerSubstituto(e => entrada = e), anexos: null);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entrada!.Dados.Anexos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Publicar com anexo de outro processo recusa sem canonicalizar")]
    public async Task Publicar_ComAnexoDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        DocumentoAPublicar doOutro = Anexo(Guid.CreateVersion7(), "quadro", TiposDeMidiaDeDocumento.Pdf, 10);
        ISnapshotPublicacaoCanonicalizer canonicalizer = CanonicalizerSubstituto();

        (Result resposta, _) = await PublicarAsync(
            processo, RepositorioDeDocumento(processo.Id), canonicalizer, [new AnexoSolicitadoDoEdital(doOutro.Id, "Quadro")]);

        resposta.Error!.Code.Should().Be("AnexoDoEdital.NaoEncontrado");
        _ = canonicalizer.DidNotReceive().Canonicalizar(Arg.Any<EntradaCanonicalizacao>());
    }

    [Fact(DisplayName = "Retificar sem lista de anexos herda os da versão vigente, com a mesma identidade")]
    public async Task Retificar_SemAnexosNoPedido_HerdaOsDaVigente()
    {
        Guid anexoId = Guid.CreateVersion7();
        Guid documentoId = Guid.CreateVersion7();
        (ProcessoSeletivo processo, VersaoConfiguracao vigente) = ProcessoPublicado(EnvelopeComAnexo(anexoId, documentoId));
        EntradaCanonicalizacao? entrada = null;

        (Result resposta, _) = await RetificarAsync(processo, vigente, CanonicalizerSubstituto(e => entrada = e), anexos: null);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        AnexoDoEdital herdado = entrada!.Dados.Anexos.Should().ContainSingle().Subject;
        herdado.Id.Should().Be(anexoId);
        herdado.DocumentoId.Should().Be(documentoId);
        herdado.NomeArquivo.Should().Be("Cronograma.pdf");
    }

    [Fact(DisplayName = "Retificar com lista vazia retira os anexos da vigente")]
    public async Task Retificar_ComListaVazia_RetiraOsAnexos()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao vigente) = ProcessoPublicado(EnvelopeComAnexo(Guid.CreateVersion7(), Guid.CreateVersion7()));
        EntradaCanonicalizacao? entrada = null;

        (Result resposta, _) = await RetificarAsync(processo, vigente, CanonicalizerSubstituto(e => entrada = e), anexos: []);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entrada!.Dados.Anexos.Should().BeEmpty();
    }

    [Theory(DisplayName = "Retificar com a lista pedida mantém a identidade só do anexo que não mudou")]
    [InlineData("Cronograma", true)]
    [InlineData("Cronograma revisado", false)]
    public async Task Retificar_ComLista_MantemAIdentidadeSoDoQueNaoMudou(string nomePedido, bool mantemAIdentidade)
    {
        Guid anexoId = Guid.CreateVersion7();
        DocumentoAPublicar documento = Anexo(Guid.CreateVersion7(), "Cronograma", TiposDeMidiaDeDocumento.Pdf, 777);
        (ProcessoSeletivo processo, VersaoConfiguracao vigente) = ProcessoPublicado(EnvelopeComAnexo(anexoId, documento.Id));
        EntradaCanonicalizacao? entrada = null;

        (Result resposta, _) = await RetificarAsync(
            processo, vigente, CanonicalizerSubstituto(e => entrada = e), [new AnexoSolicitadoDoEdital(documento.Id, nomePedido)], documento);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        AnexoDoEdital anexo = entrada!.Dados.Anexos.Should().ContainSingle().Subject;
        (anexo.Id == anexoId).Should().Be(mantemAIdentidade);
    }

    [Fact(DisplayName = "Retificar com a lista pedida não depende de a vigente ter anexos legíveis")]
    public async Task Retificar_ComLista_NaoLancaComEnvelopeIlegivelDaVigente()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao vigente) = ProcessoPublicado("{}"u8.ToArray());
        DocumentoAPublicar novo = Anexo(processo.Id, "quadro", TiposDeMidiaDeDocumento.Pdf, 10);
        EntradaCanonicalizacao? entrada = null;

        (Result resposta, _) = await RetificarAsync(
            processo, vigente, CanonicalizerSubstituto(e => entrada = e), [new AnexoSolicitadoDoEdital(novo.Id, "Quadro")], novo);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entrada!.Dados.Anexos.Should().ContainSingle();
    }

    [Fact(DisplayName = "Retificar sem lista, com envelope ilegível na vigente, falha de forma visível")]
    public async Task Retificar_SemLista_ComEnvelopeIlegivel_Lanca()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao vigente) = ProcessoPublicado("{}"u8.ToArray());

        Func<Task> retificar = () => RetificarAsync(processo, vigente, CanonicalizerSubstituto(), anexos: null);

        await retificar.Should().ThrowAsync<InvalidOperationException>();
    }

    private static byte[] EnvelopeComAnexo(Guid anexoId, Guid documentoId)
    {
        JsonObject envelope = new()
        {
            ["hashesEdital"] = new JsonObject
            {
                ["anexos"] = new JsonArray(new JsonObject
                {
                    ["anexoId"] = anexoId,
                    ["documentoId"] = documentoId,
                    ["nomeArquivo"] = "Cronograma.pdf",
                    ["tipoDeMidia"] = TiposDeMidiaDeDocumento.Pdf,
                    ["tamanhoBytes"] = 777,
                    ["hashSha256"] = new string('a', 64),
                }),
            },
        };
        return System.Text.Encoding.UTF8.GetBytes(envelope.ToJsonString());
    }

    private static DocumentoAPublicar Anexo(Guid processoId, string nome, string tipoDeMidia, long tamanho)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.AnexoDoEdital, nome, tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(tamanho, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarAsync(
        ProcessoSeletivo processo,
        IDocumentoAPublicarRepository documentos,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        IReadOnlyList<AnexoSolicitadoDoEdital>? anexos) =>
        PublicarProcessoSeletivoCommandHandler.Handle(
            new PublicarProcessoSeletivoCommand(
                processo.Id, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Anexos: anexos),
            RepositorioDoProcesso(processo),
            documentos,
            Substitute.For<IRascunhoDePublicacaoRepository>(),
            Substitute.For<IAtoDoProcessoRepository>(),
            canonicalizer, new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            RepositorioCom(RegraQueAprova()),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            CadastrosVivos.CatalogoDeFatos(),
            Substitute.For<ICalendarioVigenteReader>(),
            new RelogioFixo(Agora),
            CancellationToken.None);

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync(
        ProcessoSeletivo processo,
        VersaoConfiguracao vigente,
        ISnapshotPublicacaoCanonicalizer canonicalizer,
        IReadOnlyList<AnexoSolicitadoDoEdital>? anexos,
        params DocumentoAPublicar[] documentosDoProcesso)
    {
        IProcessoSeletivoRepository repositorio = RepositorioDoProcesso(processo);
        repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(vigente);

        return RetificarProcessoSeletivoCommandHandler.Handle(
            new RetificarProcessoSeletivoCommand(
                processo.Id, "Correção do prazo", "001/2026-R1", null, null,
                DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Anexos: anexos),
            repositorio,
            RepositorioDeDocumento(processo.Id, documentosDoProcesso),
            Substitute.For<IRascunhoDePublicacaoRepository>(),
            Substitute.For<IAtoDoProcessoRepository>(),
            canonicalizer, new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            TipoDeAtoReader(),
            Substitute.For<IVagaDeLinhagemReader>(),
            RepositorioCom(RegraQueAprova()),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            CadastrosVivos.CatalogoDeFatos(),
            Substitute.For<ICalendarioVigenteReader>(),
            Substitute.For<IRegistroCodecsEnvelope>(),
            new RelogioFixo(Agora),
            CancellationToken.None);
    }
}
