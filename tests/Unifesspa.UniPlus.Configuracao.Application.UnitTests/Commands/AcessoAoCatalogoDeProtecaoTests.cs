namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O acesso ao catálogo é avaliado sem registrar, e as negativas só entram na trilha quando nenhuma das três permissões
/// autoriza o pedido. O token que não identifica o solicitante é identidade incompleta, e não falta de permissão.
/// </summary>
public sealed class AcessoAoCatalogoDeProtecaoTests
{
    private readonly IVerificadorDeAcesso _verificador = Substitute.For<IVerificadorDeAcesso>();

    [Fact(DisplayName = "Quem aprova tem o acesso de aprovar, e nada é gravado")]
    public async Task Aprovar_NaoGrava()
    {
        Avaliar(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo);

        Result<AcessoAoCatalogoDeProtecao> acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default);

        acesso.Value.Should().Be(new AcessoAoCatalogoDeProtecao(ManterTipos: false, ManterFatos: false, Aprovar: true));
        await _verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Fact(DisplayName = "Sem nenhuma das três permissões, as três negativas vão ao desfecho do pedido")]
    public async Task SemNenhuma_RegistraAsTresNegativas()
    {
        Avaliar();

        Result<AcessoAoCatalogoDeProtecao> acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default);

        acesso.Value!.Algum.Should().BeFalse();
        await _verificador.Received(1).RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d => d.Count == 3 && d.All(x => x is DecisaoNegada)),
            IntencaoDoRegistro.Pedido,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "As três negativas do catálogo geram um registro cada, ainda que as permissões sejam de dado interno")]
    public async Task SemNenhuma_GeraUmRegistroPorNegativa()
    {
        Avaliar();
        IReadOnlyCollection<DecisaoDeAcesso>? entregues = null;
        _verificador.RegistrarDesfechoAsync(Arg.Do<IReadOnlyCollection<DecisaoDeAcesso>>(d => entregues = d), IntencaoDoRegistro.Pedido, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default);

        PoliticaDeRegistroDeAcesso.Desfecho(entregues!, IntencaoDoRegistro.Pedido).Should().HaveCount(3);
    }

    [Fact(DisplayName = "Token sem sujeito recusa com identidade incompleta, e não com falta de permissão")]
    public async Task SemSujeito_RecusaComIdentidadeIncompleta()
    {
        _verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(SemSujeito.Instancia);

        Result<AcessoAoCatalogoDeProtecao> acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default);

        acesso.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.IdentidadeIncompleta);
        await _verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    private void Avaliar(params string[] permitidas) =>
        _verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(chamada =>
        {
            PermissionRequirement requisito = chamada.ArgAt<PermissionRequirement>(0);
            return DecisaoDeAcessoDeTeste.Tomada(
                permitidas.Contains(requisito.Permissao) ? ResultadoDoAcesso.Permitido : ResultadoDoAcesso.Negado,
                requisito,
                chamada.ArgAt<ResourceContext?>(1));
        });
}
