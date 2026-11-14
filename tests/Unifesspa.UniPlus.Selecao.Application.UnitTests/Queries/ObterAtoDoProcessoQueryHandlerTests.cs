namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A consulta de um ato devolve o mesmo registro que a lista do processo traz, e não revela o ato de outro processo.
/// </summary>
public sealed class ObterAtoDoProcessoQueryHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IAtoDoProcessoRepository _atos = Substitute.For<IAtoDoProcessoRepository>();
    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();

    public ObterAtoDoProcessoQueryHandlerTests()
    {
        _processos.ExisteAsync(_processoId, Arg.Any<CancellationToken>()).Returns(true);
        _documentos.ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private Task<Result<AtoDoProcessoDto>> Consultar(Guid atoId) => ObterAtoDoProcessoQueryHandler.Handle(
        new ObterAtoDoProcessoQuery(_processoId, atoId),
        _processos,
        _atos,
        _documentos,
        CancellationToken.None);

    private static DocumentoAPublicar Confirmado(Guid processoId)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDoEdital, "aviso.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static AtoDoProcesso Registro(Guid processoId, DocumentoAPublicar principal, IReadOnlyList<AnexoDeclarado>? anexos = null) =>
        AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), processoId, "AVISO", "Aviso", "3/2026", new DateOnly(2026, 12, 10),
                "Diretor do CEPS", "ceps", "psm-medicina-2027", principal.Id, anexos ?? []),
            Agora).Value!;

    [Fact(DisplayName = "O ato do processo volta com o principal e os anexos pelo nome de exibição")]
    public async Task Handle_AtoDoProcesso_ProjetaOsDocumentos()
    {
        DocumentoAPublicar principal = Confirmado(_processoId);
        DocumentoAPublicar anexo = Confirmado(_processoId);
        AtoDoProcesso ato = Registro(_processoId, principal, [new AnexoDeclarado(anexo.Id, "Anexo I")]);
        _atos.ObterComAnexosAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(ato);
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([principal, anexo]);

        Result<AtoDoProcessoDto> resultado = await Consultar(ato.Id);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Id.Should().Be(ato.Id);
        resultado.Value.TipoCodigo.Should().Be("AVISO");
        resultado.Value.DocumentoPrincipal.DocumentoAPublicarId.Should().Be(principal.Id);
        resultado.Value.Anexos.Select(a => (a.DocumentoAPublicarId, a.NomeDeExibicao)).Should().Equal((anexo.Id, "Anexo I"));
    }

    [Fact(DisplayName = "O ato de outro processo devolve não encontrado, sem revelar que existe")]
    public async Task Handle_AtoDeOutroProcesso_DevolveNaoEncontrado()
    {
        Guid outroProcesso = Guid.CreateVersion7();
        AtoDoProcesso ato = Registro(outroProcesso, Confirmado(outroProcesso));
        _atos.ObterComAnexosAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(ato);

        Result<AtoDoProcessoDto> resultado = await Consultar(ato.Id);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.NaoEncontrado);
        await _documentos.DidNotReceiveWithAnyArgs()
            .ListarDoProcessoAsync(default, default!, default);
    }

    [Fact(DisplayName = "O ato sem registro devolve não encontrado")]
    public async Task Handle_AtoInexistente_DevolveNaoEncontrado()
    {
        Guid atoId = Guid.CreateVersion7();
        _atos.ObterComAnexosAsync(atoId, Arg.Any<CancellationToken>()).Returns((AtoDoProcesso?)null);

        Result<AtoDoProcessoDto> resultado = await Consultar(atoId);

        resultado.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.NaoEncontrado);
    }

    [Fact(DisplayName = "O processo inexistente devolve o não encontrado do processo, antes de procurar o ato")]
    public async Task Handle_ProcessoInexistente_DevolveNaoEncontradoDoProcesso()
    {
        _processos.ExisteAsync(_processoId, Arg.Any<CancellationToken>()).Returns(false);

        Result<AtoDoProcessoDto> resultado = await Consultar(Guid.CreateVersion7());

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await _atos.DidNotReceiveWithAnyArgs().ObterComAnexosAsync(default, default);
    }

    [Fact(DisplayName = "Ato que referencia documento ausente é inconsistência de dado e não é escondido")]
    public async Task Handle_DocumentoReferenciadoAusente_Lanca()
    {
        AtoDoProcesso ato = Registro(_processoId, Confirmado(_processoId));
        _atos.ObterComAnexosAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(ato);

        Func<Task> consultar = () => Consultar(ato.Id);

        await consultar.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{ato.Id}*");
    }
}
