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
/// O acesso ao catálogo é avaliado sem registrar, e as negativas só entram na trilha quando nenhuma das quatro permissões
/// autoriza o pedido. O token que não identifica o solicitante é identidade incompleta, e não falta de permissão. Quem só
/// consulta a proteção do catálogo vê os dois cadastros, mas não mantém nenhum, e a leitura dele é registrada.
/// </summary>
public sealed class AcessoAoCatalogoDeProtecaoTests
{
    private readonly IVerificadorDeAcesso _verificador = Substitute.For<IVerificadorDeAcesso>();

    [Fact(DisplayName = "Quem aprova tem o acesso de aprovar, e nada é gravado")]
    public async Task Aprovar_NaoGrava()
    {
        Avaliar(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo);

        Result<AcessoAoCatalogoDeProtecao> acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default);

        (acesso.Value.ManterTipos, acesso.Value.ManterFatos, acesso.Value.Aprovar, acesso.Value.Consultar).Should().Be((false, false, true, false));
        await _verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Fact(DisplayName = "Sem nenhuma das quatro permissões, as quatro negativas vão ao desfecho do pedido")]
    public async Task SemNenhuma_RegistraAsQuatroNegativas()
    {
        Avaliar();

        Result<AcessoAoCatalogoDeProtecao> acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default);

        acesso.Value!.Algum.Should().BeFalse();
        await _verificador.Received(1).RegistrarDesfechoAsync(
            Arg.Is<IReadOnlyCollection<DecisaoDeAcesso>>(d => d.Count == 4 && d.All(x => x is DecisaoNegada)),
            IntencaoDoRegistro.Pedido,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "As quatro negativas do catálogo geram um registro cada, ainda que algumas permissões sejam de dado interno")]
    public async Task SemNenhuma_GeraUmRegistroPorNegativa()
    {
        Avaliar();
        List<RegistroDecisaoAcesso> registros = Registros();

        await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default);

        registros.Should().HaveCount(4);
    }

    [Fact(DisplayName = "Token sem sujeito recusa com identidade incompleta, e não com falta de permissão")]
    public async Task SemSujeito_RecusaComIdentidadeIncompleta()
    {
        _verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(SemSujeito.Instancia);

        Result<AcessoAoCatalogoDeProtecao> acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default);

        acesso.Error!.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.IdentidadeIncompleta);
        await _verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Theory(DisplayName = "A leitura da fila grava a permissão pessoal que a autorizou, e não grava a de manter um cadastro, que é interna")]
    [InlineData("configuracao:protecao-do-catalogo:consultar", true)]
    [InlineData("configuracao:protecao-do-catalogo:aprovar", true)]
    [InlineData("configuracao:tipos-documento:manter", false)]
    public async Task RegistrarLeitura_GravaSoAPermissaoPessoal(string permitida, bool grava)
    {
        Avaliar(permitida);
        AcessoAoCatalogoDeProtecao acesso = (await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default)).Value;
        List<RegistroDecisaoAcesso> registros = Registros();

        await acesso.RegistrarLeituraAsync(_verificador, default);

        registros.Select(static r => r.Permissao).Should().Equal(grava ? [permitida] : []);
    }

    [Fact(DisplayName = "Quem só consulta a proteção vê os dois cadastros sem manter nenhum nem aprovar")]
    public async Task Consultar_VeOsDoisCadastrosSemManter()
    {
        Avaliar(PermissaoConfiguracao.ProtecaoDoCatalogoConsultar.Codigo);

        AcessoAoCatalogoDeProtecao acesso = (await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, default)).Value;

        (acesso.VeOsDoisCadastros, acesso.ManterTipos, acesso.ManterFatos, acesso.Aprovar, acesso.Algum).Should().Be((true, false, false, false, true));
    }

    // O que a política de registro manda gravar de cada desfecho que o acesso entrega ao verificador.
    private List<RegistroDecisaoAcesso> Registros()
    {
        List<RegistroDecisaoAcesso> registros = [];
        _verificador.RegistrarDesfechoAsync(
                Arg.Do<IReadOnlyCollection<DecisaoDeAcesso>>(d => registros.AddRange(PoliticaDeRegistroDeAcesso.Desfecho(d, IntencaoDoRegistro.Pedido))),
                IntencaoDoRegistro.Pedido,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return registros;
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
