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

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferenciaDosAnexosDoEdital
            .ConferirAsync(repositorio, ProcessoId, null, [vigente], CancellationToken.None);

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
        DocumentoAPublicar quadro = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf);
        DocumentoAPublicar cronograma = Documento("Cronograma", TiposDeMidiaDeDocumento.Pdf);
        DocumentoAPublicar quadroNovo = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf);
        AnexoDoEdital vigenteQuadro = AnexoVigente(quadro);
        AnexoDoEdital vigenteCronograma = AnexoVigente(cronograma);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [
                new AnexoSolicitadoDoEdital(quadro.Id, "Quadro"),
                new AnexoSolicitadoDoEdital(cronograma.Id, "Cronograma revisado"),
                new AnexoSolicitadoDoEdital(quadroNovo.Id, "Quadro novo"),
            ],
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

        resultado.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.NaoEncontrado);
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

    [Fact(DisplayName = "Nome que o download não aceita é recusado com erro nomeado")]
    public async Task NomeInvalido_Recusa()
    {
        DocumentoAPublicar documento = Documento("Quadro", TiposDeMidiaDeDocumento.Pdf);

        Result<IReadOnlyList<AnexoDoEdital>> resultado = await ConferirAsync(
            [new AnexoSolicitadoDoEdital(documento.Id, "a/b")], [], documento);

        resultado.Error!.Code.Should().Be(AnexoDoEditalErrorCodes.NomeInvalido);
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

        return ConferenciaDosAnexosDoEdital.ConferirAsync(repositorio, ProcessoId, pedidos, vigentes, CancellationToken.None);
    }

    private static AnexoDoEdital AnexoVigente(DocumentoAPublicar documento) =>
        AnexoDoEdital.Criar(Guid.CreateVersion7(), documento, documento.NomeArquivo).Value!;

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
