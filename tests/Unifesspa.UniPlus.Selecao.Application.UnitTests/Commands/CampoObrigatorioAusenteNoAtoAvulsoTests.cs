namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A preparação do ato avulso sem a data do ato, sem o documento principal ou com um anexo sem documento: a recusa é 422
/// com o código de campo obrigatório, no campo do pedido, acumulada com a recusa do tipo do ato, que não depende deles, e
/// depois da recusa de que o processo não existe.
/// </summary>
public sealed class CampoObrigatorioAusenteNoAtoAvulsoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Dia = new(2026, 12, 10);

    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly ICertameDivulgadoRepository _divulgados = Substitute.For<ICertameDivulgadoRepository>();
    private readonly IAtoDoProcessoRepository _atos = Substitute.For<IAtoDoProcessoRepository>();
    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();
    private readonly ITipoAtoPublicadoReader _tipos = Substitute.For<ITipoAtoPublicadoReader>();
    private readonly DocumentoAPublicar _principal = DocumentoConfirmado();

    public CampoObrigatorioAusenteNoAtoAvulsoTests()
    {
        _processos.ObterSiglaDaUnidadeAdministradoraAsync(_processoId, Arg.Any<CancellationToken>()).Returns("CEPS");
        _divulgados.ObterParaMutacaoAsync(_processoId, Arg.Any<CancellationToken>()).Returns(
            CertameDivulgado.Criar(
                _processoId, 1, Guid.CreateVersion7(), new string('a', 64), "1",
                new FacetasDoCertameDivulgado("psm-medicina-2027", "PSM Medicina 2027", "001/2026", [], Agora.AddDays(-1), Agora.AddDays(20)),
                """{"nome":"documento"}""", Agora));
        _tipos.ObterVigenteAsync("AVISO", Dia, Arg.Any<CancellationToken>()).Returns(
            new TipoAtoPublicadoView("AVISO", "Aviso", false, false, false, false, PublicavelComoAvulso: true));
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Guid>)call[1]).Where(id => id == _principal.Id).Select(_ => _principal).ToList());
        _atos.ListarDocumentosJaUsadosAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());
    }

    [Fact(DisplayName = "O ato completo é preparado com a data declarada")]
    public async Task AtoCompleto_Prepara()
    {
        Result<AtoAPublicar> preparado = await PrepararAsync(Comando());

        preparado.IsSuccess.Should().BeTrue(preparado.Error?.Message);
        preparado.Value!.DataDoAto.Should().Be(new DateOnly(2026, 12, 1));
    }

    [Fact(DisplayName = "A data do ato ausente é recusada no campo do pedido")]
    public async Task DataAusente_Recusa()
    {
        Result<AtoAPublicar> preparado = await PrepararAsync(Comando() with { DataDoAto = null });

        Recusa(preparado).Should().Equal(("dataDoAto", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "O documento principal ausente é recusado no campo do pedido, e os documentos não são lidos")]
    public async Task DocumentoPrincipalAusente_Recusa()
    {
        Result<AtoAPublicar> preparado = await PrepararAsync(Comando() with { DocumentoPrincipalId = null });

        Recusa(preparado).Should().Equal(("documentoPrincipalId", CodigosDeValidacao.Obrigatorio));
        await _documentos.DidNotReceive().ListarDoProcessoAsync(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A data, o documento principal e o anexo ausentes acumulam, a data primeiro")]
    public async Task VariosCamposAusentes_AcumulamNaOrdemDoPedido()
    {
        Result<AtoAPublicar> preparado = await PrepararAsync(
            Comando() with { DataDoAto = null, DocumentoPrincipalId = null, Anexos = [new(null, "Quadro")] });

        Recusa(preparado).Select(static r => r.Campo).Should().Equal("dataDoAto", "documentoPrincipalId", "anexos[0].documentoId");
        preparado.Errors.Should().OnlyContain(static e => e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "A data ausente acumula com a recusa do tipo sem versão vigente, e a data vem primeiro")]
    public async Task DataAusenteETipoSemVersao_Acumulam()
    {
        _tipos.ObterVigenteAsync("AVISO", Dia, Arg.Any<CancellationToken>()).Returns((TipoAtoPublicadoView?)null);

        Result<AtoAPublicar> preparado = await PrepararAsync(Comando() with { DataDoAto = null });

        Recusa(preparado).Should().Equal(("dataDoAto", CodigosDeValidacao.Obrigatorio), (null, "ProcessoSeletivo.TipoDeAtoSemVersaoVigente"));
    }

    [Fact(DisplayName = "O documento principal e o anexo sem documento acumulam com a recusa do tipo, que vem antes")]
    public async Task DocumentosAusentesETipoSemVersao_Acumulam()
    {
        _tipos.ObterVigenteAsync("AVISO", Dia, Arg.Any<CancellationToken>()).Returns((TipoAtoPublicadoView?)null);

        Result<AtoAPublicar> preparado = await PrepararAsync(Comando() with { DocumentoPrincipalId = null, Anexos = [new(null, "Quadro")] });

        Recusa(preparado).Should().Equal(
            (null, "ProcessoSeletivo.TipoDeAtoSemVersaoVigente"),
            ("documentoPrincipalId", CodigosDeValidacao.Obrigatorio),
            ("anexos[0].documentoId", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "A data, o tipo sem versão e o documento principal ausentes acumulam, nessa ordem")]
    public async Task DataTipoEDocumentoAusentes_AcumulamNessaOrdem()
    {
        _tipos.ObterVigenteAsync("AVISO", Dia, Arg.Any<CancellationToken>()).Returns((TipoAtoPublicadoView?)null);

        Result<AtoAPublicar> preparado = await PrepararAsync(Comando() with { DataDoAto = null, DocumentoPrincipalId = null });

        Recusa(preparado).Should().Equal(
            ("dataDoAto", CodigosDeValidacao.Obrigatorio),
            (null, "ProcessoSeletivo.TipoDeAtoSemVersaoVigente"),
            ("documentoPrincipalId", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "A data ausente com os documentos conferidos recusa só a data, sem montar o ato")]
    public async Task DataAusenteComDocumentosConferidos_RecusaSoAData()
    {
        Result<AtoAPublicar> preparado = await PrepararAsync(Comando() with { DataDoAto = null });

        preparado.IsFailure.Should().BeTrue();
        Recusa(preparado).Should().Equal(("dataDoAto", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "O processo inexistente é recusado antes de a ausência ser julgada")]
    public async Task ProcessoInexistente_RecusaAntesDaAusencia()
    {
        _processos.ObterSiglaDaUnidadeAdministradoraAsync(_processoId, Arg.Any<CancellationToken>()).Returns((string?)null);

        Result<AtoAPublicar> preparado = await PrepararAsync(Comando() with { DataDoAto = null, DocumentoPrincipalId = null });

        Recusa(preparado).Should().Equal((null, "ProcessoSeletivo.NaoEncontrado"));
    }

    private Task<Result<AtoAPublicar>> PrepararAsync(PublicarAtoAvulsoDoProcessoCommand comando) =>
        new PreparacaoDaPublicacaoDoAtoAvulso(
            new ConferenciaDoProcessoDivulgado(_processos, _divulgados),
            new LeituraDoInstanteDoComando(new RelogioFixo(Agora), new ResolvedorFusoDeTeste()),
            new ConferenciaDoTipoDoAtoAvulso(new ConferenciaDoTipoDeAto(_tipos)),
            new ConferenciaDoAtoCorrigido(_atos, _processos),
            new ConferenciaDosDocumentosDoAto(_documentos, _atos))
            .PrepararAsync(comando, CancellationToken.None);

    private PublicarAtoAvulsoDoProcessoCommand Comando() =>
        new(_processoId, "AVISO", "5/2026", new DateOnly(2026, 12, 1), "Diretor do CEPS", _principal.Id);

    private static (string? Campo, string Codigo)[] Recusa(Result<AtoAPublicar> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    private static DocumentoAPublicar DocumentoConfirmado()
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            Guid.CreateVersion7(), FinalidadeDoDocumento.DocumentoDeAto, "aviso.pdf", TiposDeMidiaDeDocumento.Pdf, new RelogioFixo(Agora), TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string('a', 64), new RelogioFixo(Agora)).IsSuccess.Should().BeTrue();
        return documento;
    }
}
