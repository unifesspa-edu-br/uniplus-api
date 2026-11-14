namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// A montagem do ato recusa sem gravar nada e devolve o ato pronto para quem chama adicionar e salvar.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class MontagemDoAtoNormativoTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Tipo = "EDITAL_ABERTURA";
    private static readonly DateOnly DataDoAto = new(2026, 3, 10);
    private static readonly DateOnly DiaDaPublicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset PublicadoEm = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();

    [Fact(DisplayName = "Monta o ato com o instante recebido e sem gravar")]
    public async Task Montar_ComTipoVigente_DevolveOAtoSemGravar()
    {
        TipoVigente(unicoPorObjeto: false);

        Result<AtoMontado> montado = await Montar(Comando());

        montado.IsSuccess.Should().BeTrue();
        montado.Value!.Ato.PublicadoEm.Should().Be(PublicadoEm);
        montado.Value.Ato.DataDoAto.Should().Be(DataDoAto);
        await _atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoNormativo>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa o tipo sem versão vigente na data da publicação")]
    public async Task Montar_SemTipoVigente_Recusa()
    {
        Result<AtoMontado> montado = await Montar(Comando());

        montado.Error!.Code.Should().Be(AtoNormativoErrorCodes.TipoSemVersaoVigente);
    }

    [Fact(DisplayName = "Recusa o ato de processo seletivo fora do registro pelo domínio")]
    public async Task Montar_VinculoComProcessoSeletivo_RecusaForaDoRegistroPeloDominio()
    {
        Result<AtoMontado> montado = await Montar(
            Comando() with { Vinculos = [new(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, Guid.CreateVersion7())] });

        montado.Error!.Code.Should().Be(AtoNormativoErrorCodes.VinculoExigeRegistroPeloDominio);
        await _tipos.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
    }

    [Fact(DisplayName = "Recusa a retificação de um ato que já foi retificado")]
    public async Task Montar_AtoJaRetificado_Recusa()
    {
        TipoVigente(unicoPorObjeto: false);
        AtoNormativo retificado = AtoExistente(unicoPorObjeto: false);
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(AtoExistente(unicoPorObjeto: false));

        Result<AtoMontado> montado = await Montar(
            Comando() with { AtoRetificadoId = retificado.Id, MotivoRetificacao = "corrige o anexo" });

        montado.Error!.Code.Should().Be(AtoNormativoErrorCodes.AtoJaRetificado);
    }

    [Fact(DisplayName = "Recusa a vaga do objeto ocupada por outro ato vivo do mesmo tipo, sem reservar vaga")]
    public async Task Montar_VagaOcupada_RecusaSemReservar()
    {
        TipoVigente(unicoPorObjeto: true);
        _atos.ObterAtoConflitanteNoObjetoAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(AtoExistente(unicoPorObjeto: true));

        Result<AtoMontado> montado = await Montar(Comando() with { Vinculos = [new("OBJETO_A", Guid.CreateVersion7())] });

        montado.Error!.Code.Should().Be(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo);
        await _atos.DidNotReceive().ReservarVagaAsync(Arg.Any<VagaDeLinhagem>(), Arg.Any<CancellationToken>());
    }

    private Task<Result<AtoMontado>> Montar(RegistrarAtoNormativoCommand comando) =>
        MontagemDeTeste.Criar(_tipos, _atos).MontarAsync(comando, PublicadoEm, CancellationToken.None);

    private void TipoVigente(bool unicoPorObjeto)
    {
        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            Tipo, "Edital de abertura", congelaConfiguracao: false, unicoPorObjeto,
            efeitoIrreversivel: false, ehResultado: false, new DateOnly(2026, 1, 1), null, null).Value!;
        _tipos.ObterVigenteAsync(Tipo, DiaDaPublicacao, Arg.Any<CancellationToken>()).Returns(tipo);
    }

    private static AtoNormativo AtoExistente(bool unicoPorObjeto) =>
        AtoNormativo.Registrar(
            Guid.CreateVersion7(),
            "CEPS", "EDITAL", 2026, "13", Tipo,
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto,
            DataDoAto, HashValido, "Jairo Belchior", PublicadoEm, versaoDeConfiguracao: null);

    private static RegistrarAtoNormativoCommand Comando() =>
        new(
            Orgao: "CEPS",
            Serie: "EDITAL",
            Ano: 2026,
            Numero: "13",
            TipoCodigo: Tipo,
            DataDoAto: DataDoAto,
            DocumentoHash: HashValido,
            Assinante: "Jairo Belchior");
}
