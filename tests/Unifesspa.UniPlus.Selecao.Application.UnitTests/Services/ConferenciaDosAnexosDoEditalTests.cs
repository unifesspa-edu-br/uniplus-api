namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os anexos da versão que nasce: o que o administrador pede é conferido contra os documentos do
/// processo, e a identidade do anexo que continua o mesmo atravessa a retificação.
/// </summary>
public sealed class ConferenciaDosAnexosDoEditalTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    [Fact(DisplayName = "Sem anexos pedidos, a versão nova herda os da vigente sem consultar documento")]
    public async Task SemPedido_HerdaOsDaVigente()
    {
        AnexoDoEdital vigente = AnexoVigente(Documento("Quadro", TiposDeMidiaDeDocumento.Pdf));
        IDocumentoAPublicarRepository repositorio = Substitute.For<IDocumentoAPublicarRepository>();

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await new ConferenciaDosAnexosDoEdital(repositorio)
            .ConferirAsync(ProcessoId, null, [vigente], CancellationToken.None);

        resultado.Value.Should().Equal(vigente);
        await repositorio.DidNotReceiveWithAnyArgs().ListarDoProcessoAsync(default, default!, default);
    }

    [Fact(DisplayName = "Lista vazia retira todos os anexos da vigente")]
    public async Task ListaVazia_RetiraTodos()
    {
        AnexoDoEdital vigente = AnexoVigente(Documento("Quadro", TiposDeMidiaDeDocumento.Pdf));

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync([], [vigente]);

        resultado.Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "Anexo com o mesmo documento e o mesmo nome mantém a identidade; trocado ou renomeado ganha outra")]
    public async Task Identidade_MantidaSoQuandoNadaMuda()
    {
        (DocumentoAPublicar quadro, AnexoDoEdital vigenteQuadro) = DocumentoEAnexoVigente("Quadro");
        (DocumentoAPublicar cronograma, AnexoDoEdital vigenteCronograma) = DocumentoEAnexoVigente("Cronograma");
        DocumentoAPublicar quadroNovo = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            AnexosSolicitadosNaRetificacao(quadro, cronograma, quadroNovo),
            [vigenteQuadro, vigenteCronograma],
            quadro, cronograma, quadroNovo);

        IReadOnlyList<AnexoDoEdital> anexos = resultado.Value!;
        anexos[0].Id.Should().Be(vigenteQuadro.Id, "mesmo documento e mesmo nome: o endereço divulgado continua o mesmo anexo");
        anexos[1].Id.Should().NotBe(vigenteCronograma.Id, "renomeado é outro anexo");
        anexos[2].Id.Should().NotBe(vigenteQuadro.Id, "documento trocado é outro anexo");
        anexos.Select(static a => a.NomeArquivo).Should().Equal("Quadro.pdf", "Cronograma revisado.pdf", "Quadro novo.pdf");
    }

    [Fact(DisplayName = "Documento inexistente ou de outro processo é recusado com erro nomeado")]
    public async Task DocumentoInexistente_Recusa()
    {
        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [new AnexoSolicitadoDoEdital(Guid.CreateVersion7(), "Quadro")], []);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("anexos[0].documentoId");
        recusa.Error.Code.Should().Be(AnexoDoEditalErrorCodes.NaoEncontrado);
        recusa.Error.Message.Should().Be("O documento do anexo deve ser um dos documentos deste processo.");
    }

    [Fact(DisplayName = "Cada anexo recusado traz a sua recusa, apontando a posição dele no pedido")]
    public async Task DocumentosInexistentes_AcumulamUmaRecusaPorAnexo()
    {
        DocumentoAPublicar existente = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [
                new AnexoSolicitadoDoEdital(Guid.CreateVersion7(), "Primeiro"),
                new AnexoSolicitadoDoEdital(existente.Id, "Quadro"),
                new AnexoSolicitadoDoEdital(Guid.CreateVersion7(), "Terceiro"),
            ],
            [],
            existente);

        resultado.Errors.Select(static e => e.Field).Should().Equal("anexos[0].documentoId", "anexos[2].documentoId");
        resultado.Errors.Should().OnlyContain(static e => e.Error.Code == AnexoDoEditalErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "Documento ainda pendente é recusado com erro nomeado")]
    public async Task DocumentoPendente_Recusa()
    {
        DocumentoAPublicar pendente = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf, confirmado: false);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [new AnexoSolicitadoDoEdital(pendente.Id, "Quadro")], [], pendente);

        resultado.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.NaoConfirmado);
    }

    [Theory(DisplayName = "Documento enviado como edital ou como documento de ato não é aceito como anexo")]
    [InlineData(FinalidadeDoDocumento.DocumentoDoEdital)]
    [InlineData(FinalidadeDoDocumento.DocumentoDeAto)]
    public async Task OutraFinalidade_Recusa(FinalidadeDoDocumento finalidade)
    {
        DocumentoAPublicar documento = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf, finalidade: finalidade);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [new AnexoSolicitadoDoEdital(documento.Id, "Quadro")], [], documento);

        resultado.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.FinalidadeIncompativel);
    }

    [Fact(DisplayName = "Finalidade incompatível prevalece sobre documento não confirmado e nome com substituto isolado")]
    public void Criar_OutraFinalidadeENomeComSubstitutoIsolado_RecusaFinalidade()
    {
        DocumentoAPublicar documento = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf, confirmado: false, finalidade: FinalidadeDoDocumento.DocumentoDoEdital);

        Result<AnexoDoEdital> resultado = AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, "Quadro\uD800");

        resultado.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.FinalidadeIncompativel);
    }

    [Fact(DisplayName = "Documento não confirmado prevalece sobre nome com substituto isolado")]
    public void Criar_NaoConfirmadoENomeComSubstitutoIsolado_RecusaDocumentoNaoConfirmado()
    {
        DocumentoAPublicar documento = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf, confirmado: false);

        Result<AnexoDoEdital> resultado = AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, "Quadro\uD800");

        resultado.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.NaoConfirmado);
    }

    [Fact]
    public void Criar_ConfirmadoENomeComSubstitutoIsolado_RecusaNome()
    {
        DocumentoAPublicar documento = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf);

        Result<AnexoDoEdital> resultado = AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, "Quadro\uD800");

        resultado.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.NomeInvalido);
    }

    [Fact(DisplayName = "Nome que o download não aceita é recusado com erro nomeado")]
    public async Task NomeInvalido_Recusa()
    {
        DocumentoAPublicar documento = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [new AnexoSolicitadoDoEdital(documento.Id, "a/b")], [], documento);

        resultado.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.NomeInvalido);
    }

    [Fact(DisplayName = "As recusas de nome e de documento apontam o campo do anexo na posição em que ele veio no pedido")]
    public async Task RecusasDeNomeEDeDocumento_ApontamOCampoDoItem()
    {
        DocumentoAPublicar primeiro = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf);
        DocumentoAPublicar segundo = Documento("Cronograma", TiposDeMidiaDeDocumento.Pdf);
        DocumentoAPublicar pendente = Documento("Pendente", TiposDeMidiaDeDocumento.Pdf, confirmado: false);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [
                new AnexoSolicitadoDoEdital(primeiro.Id, "a/b"),
                new AnexoSolicitadoDoEdital(pendente.Id, "Pendente"),
                new AnexoSolicitadoDoEdital(segundo.Id, "c/d"),
            ],
            [],
            primeiro,
            segundo,
            pendente);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("anexos[0].nome", AnexoDoEditalErrorCodes.NomeInvalido),
            ("anexos[1].documentoId", AnexoDoEditalErrorCodes.NaoConfirmado),
            ("anexos[2].nome", AnexoDoEditalErrorCodes.NomeInvalido));
    }

    [Fact(DisplayName = "O anexo sem documento não é consultado nem recusado aqui, e os demais mantêm a posição que têm no pedido")]
    public async Task AnexoSemDocumento_NaoEscondeAsRecusasDosDemais()
    {
        DocumentoAPublicar segundo = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [new AnexoSolicitadoDoEdital(null, "Primeiro"), new AnexoSolicitadoDoEdital(segundo.Id, "a/b")],
            [],
            segundo);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("anexos[1].nome", AnexoDoEditalErrorCodes.NomeInvalido));
    }

    [Fact(DisplayName = "O documento recusado e o nome inválido do mesmo anexo saem juntos, cada um no seu campo")]
    public async Task DocumentoNaoConfirmadoENomeInvalido_SomamAsDuasRecusas()
    {
        DocumentoAPublicar pendente = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf, confirmado: false);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [new AnexoSolicitadoDoEdital(pendente.Id, "a/b")], [], pendente);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("anexos[0].documentoId", AnexoDoEditalErrorCodes.NaoConfirmado),
            ("anexos[0].nome", AnexoDoEditalErrorCodes.NomeInvalido));
    }

    [Fact(DisplayName = "Mais anexos que o limite são recusados antes de consultar documento")]
    public async Task AcimaDoLimite_Recusa()
    {
        AnexoSolicitadoDoEdital[] pedidos = [.. Enumerable.Range(0, AnexoDoEdital.QuantidadeMaxima + 1)
            .Select(static _ => new AnexoSolicitadoDoEdital(Guid.CreateVersion7(), "Anexo"))];

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(pedidos, []);

        resultado.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.QuantidadeExcedida);
    }

    private static Task<Result<IReadOnlyList<AnexoDoEdital>>> ConferirAsync(
        IReadOnlyList<AnexoSolicitadoDoEdital> pedidos,
        IReadOnlyList<AnexoDoEdital> vigentes,
        params DocumentoAPublicar[] armazenados)
    {
        IDocumentoAPublicarRepository repositorio = Substitute.For<IDocumentoAPublicarRepository>();
        repositorio.ListarDoProcessoAsync(ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(armazenados);

        return new ConferenciaDosAnexosDoEdital(repositorio).ConferirAsync(ProcessoId, pedidos, vigentes, CancellationToken.None);
    }

    private static AnexoDoEdital AnexoVigente(DocumentoAPublicar documento) =>
        AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, documento.NomeArquivo).Value!;

    private static (DocumentoAPublicar Documento, AnexoDoEdital Anexo) DocumentoEAnexoVigente(string nome)
    {
        DocumentoAPublicar documento = Documento(nome, TiposDeMidiaDeDocumento.Pdf);
        return (documento, AnexoVigente(documento));
    }

    private static AnexoSolicitadoDoEdital[] AnexosSolicitadosNaRetificacao(
        DocumentoAPublicar quadro, DocumentoAPublicar cronograma, DocumentoAPublicar quadroNovo) =>
        [
            new(quadro.Id, "Quadro"),
            new(cronograma.Id, "Cronograma revisado"),
            new(quadroNovo.Id, "Quadro novo"),
        ];

    private static DocumentoAPublicar Documento(
        string nome,
        string tipoDeMidia,
        bool confirmado = true,
        FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.AnexoDoEdital)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            ProcessoId, finalidade, nome, tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        if (confirmado)
        {
            documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        }

        return documento;
    }
}
