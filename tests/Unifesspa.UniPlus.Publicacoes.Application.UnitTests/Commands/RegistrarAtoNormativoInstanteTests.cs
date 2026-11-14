namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// O instante da divulgação do ato é recebido: o endpoint o lê do relógio uma só vez, e o registro
/// por mensagem traz o que a operação de origem decidiu. A data do ato, que o documento declara,
/// é outra grandeza e nunca o substitui.
/// </summary>
public sealed class RegistrarAtoNormativoInstanteTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateOnly DataDoAto = new(2026, 3, 10);
    private static readonly DateOnly DiaDaPublicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset InstanteDoSistema = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InstanteRecebido = new(2026, 3, 13, 18, 30, 0, TimeSpan.Zero);

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider _relogio = new RelogioFixo(InstanteDoSistema);

    [Fact(DisplayName = "O endpoint grava o instante que o sistema leu, e a data do ato fica a que o documento declara")]
    public async Task Handle_SemInstanteRecebido_GravaOInstanteDoSistema()
    {
        AtoNormativo ato = await RegistrarAsync(Comando());

        ato.PublicadoEm.Should().Be(InstanteDoSistema);
        ato.DataDoAto.Should().Be(DataDoAto);
    }

    [Fact(DisplayName = "O registro por mensagem grava o instante recebido, não o do relógio de Publicações")]
    public async Task Handle_ComInstanteRecebido_GravaOInstanteRecebido()
    {
        AtoNormativo ato = await RegistrarAsync(Comando() with { PublicadoEm = InstanteRecebido });

        ato.PublicadoEm.Should().Be(InstanteRecebido);
    }

    [Fact(DisplayName = "O resultado devolve o instante gravado")]
    public async Task Handle_ComInstanteRecebido_DevolveOInstanteGravado()
    {
        PrepararTipoVigente();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando() with { PublicadoEm = InstanteRecebido });

        resultado.Value!.PublicadoEm.Should().Be(InstanteRecebido);
    }

    private async Task<AtoNormativo> RegistrarAsync(RegistrarAtoNormativoCommand comando)
    {
        PrepararTipoVigente();
        AtoNormativo? gravado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => gravado = a), Arg.Any<CancellationToken>());

        Result<RegistrarAtoNormativoResult> resultado = await Executar(comando);

        resultado.IsSuccess.Should().BeTrue();
        return gravado!;
    }

    private Task<Result<RegistrarAtoNormativoResult>> Executar(RegistrarAtoNormativoCommand comando) =>
        RegistrarAtoNormativoCommandHandler.Handle(
            comando, MontagemDeTeste.Criar(_tipos, _atos), _atos, _unitOfWork, _relogio, CancellationToken.None);

    private void PrepararTipoVigente()
    {
        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            "EDITAL_ABERTURA", "Edital de abertura", congelaConfiguracao: false, unicoPorObjeto: false,
            efeitoIrreversivel: false, ehResultado: false, new DateOnly(2026, 1, 1), null, null).Value!;
        _tipos.ObterVigenteAsync("EDITAL_ABERTURA", DiaDaPublicacao, Arg.Any<CancellationToken>()).Returns(tipo);
        _atos.ListarIdsComMesmaNumeracaoAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private static RegistrarAtoNormativoCommand Comando() =>
        new(
            Orgao: "CEPS",
            Serie: "EDITAL",
            Ano: 2026,
            Numero: "13",
            TipoCodigo: "EDITAL_ABERTURA",
            DataDoAto: DataDoAto,
            DocumentoHash: HashValido,
            Assinante: "Jairo Belchior");
}
