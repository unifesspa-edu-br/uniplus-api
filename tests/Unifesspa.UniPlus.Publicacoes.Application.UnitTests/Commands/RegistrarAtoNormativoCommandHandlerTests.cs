namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RegistrarAtoNormativoCommandHandlerTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateOnly Publicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Agora = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();
    private readonly IPublicacoesUnitOfWork _unitOfWork = Substitute.For<IPublicacoesUnitOfWork>();
    private readonly TimeProvider _relogio = new RelogioFixo(Agora);

    [Fact(DisplayName = "Recusa quando não há tipo vigente na data de publicação")]
    public async Task Handle_SemTipoVigente_Falha()
    {
        _tipos.ObterVigenteAsync("EDITAL_ABERTURA", Publicacao, Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicado?)null);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.TipoSemVersaoVigente);
        await _atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoNormativo>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Copia congela/efeito do catálogo vigente por valor e registra")]
    public async Task Handle_ComTipoVigente_CopiaERegistra()
    {
        VigenteComConsequencia(congela: true, efeito: true);
        SemConflitoDeNumero();

        AtoNormativo? capturado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => capturado = a), Arg.Any<CancellationToken>());

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        capturado.Should().NotBeNull();
        capturado!.CongelaConfiguracao.Should().BeTrue();
        capturado.EfeitoIrreversivel.Should().BeTrue();
        capturado.RegistradoEm.Should().Be(Agora);
        resultado.Value!.RegistradoEm.Should().Be(Agora);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Par de versão incompleto (só id) é recusado")]
    public async Task Handle_ComVersaoInvocadaIncompleta_Falha()
    {
        VigenteComConsequencia(congela: false, efeito: false);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { VersaoInvocadaId = Guid.CreateVersion7(), VersaoInvocadaHash = null });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.VersaoInvocadaIncompleta);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Número já usado gera aviso, sem impedir o registro")]
    public async Task Handle_ComNumeroDuplicado_AvisaERegistra()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        Guid outro = Guid.CreateVersion7();
        _atos.ListarIdsComMesmaNumeracaoAsync(
            "CEPS", "EDITAL", 2026, "13", null, Arg.Any<CancellationToken>())
            .Returns([outro]);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Avisos.Should().ContainSingle();
        resultado.Value.Avisos[0].Codigo.Should().Be("NumeroDuplicado");
        resultado.Value.Avisos[0].AtosConflitantes.Should().ContainSingle().Which.Should().Be(outro);
        await _atos.Received(1).AdicionarAsync(Arg.Any<AtoNormativo>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Número único não gera aviso")]
    public async Task Handle_ComNumeroUnico_SemAviso()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        SemConflitoDeNumero();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Avisos.Should().BeEmpty();
    }

    private Task<Result<RegistrarAtoNormativoResult>> Executar(RegistrarAtoNormativoCommand comando) =>
        RegistrarAtoNormativoCommandHandler.Handle(
            comando, _tipos, _atos, _unitOfWork, _relogio, CancellationToken.None);

    private void VigenteComConsequencia(bool congela, bool efeito)
    {
        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            "EDITAL_ABERTURA", "Edital de abertura", congela, unicoPorObjeto: false,
            efeito, new DateOnly(2026, 1, 1), null, null).Value!;
        _tipos.ObterVigenteAsync("EDITAL_ABERTURA", Publicacao, Arg.Any<CancellationToken>())
            .Returns(tipo);
    }

    private void SemConflitoDeNumero() =>
        _atos.ListarIdsComMesmaNumeracaoAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

    private static RegistrarAtoNormativoCommand Comando() =>
        new(
            Orgao: "CEPS",
            Serie: "EDITAL",
            Ano: 2026,
            Numero: "13",
            TipoCodigo: "EDITAL_ABERTURA",
            DataPublicacao: Publicacao,
            DocumentoHash: HashValido,
            Assinante: "Jairo Belchior");
}
