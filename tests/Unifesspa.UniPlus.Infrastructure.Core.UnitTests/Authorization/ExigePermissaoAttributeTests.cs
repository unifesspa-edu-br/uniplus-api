namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ExigePermissaoAttributeTests
{
    private const string Codigo = "autorizacao:permissoes:consultar-catalogo";

    private readonly IVerificadorDeAcesso _acesso = Substitute.For<IVerificadorDeAcesso>();

    [Theory(DisplayName = "Traduz os três desfechos da verificação para a resposta da borda")]
    [InlineData(ResultadoDoAcesso.Permitido, null)]
    [InlineData(ResultadoDoAcesso.Negado, typeof(ForbidResult))]
    [InlineData(ResultadoDoAcesso.Impedido, typeof(ForbidResult))]
    [InlineData(ResultadoDoAcesso.IdentidadeIncompleta, typeof(ChallengeResult))]
    public async Task OnAuthorizationAsync_TraduzODesfecho(ResultadoDoAcesso desfecho, Type? resposta)
    {
        Principal(Decisao(desfecho));
        AuthorizationFilterContext contexto = Contexto();

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo)
            .OnAuthorizationAsync(contexto);

        if (resposta is null)
        {
            contexto.Result.Should().BeNull();
        }
        else
        {
            contexto.Result.Should().BeOfType(resposta);
        }
    }

    [Fact(DisplayName = "Pede ao verificador o requisito da permissão do catálogo")]
    public async Task OnAuthorizationAsync_PedeORequisitoDoCatalogo()
    {
        Principal(Decisao(ResultadoDoAcesso.Permitido));

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo)
            .OnAuthorizationAsync(Contexto());

        await _acesso.Received(1).AvaliarAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == Codigo),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ResourceTipo informado vira o resource da decisão, com a sensibilidade da permissão")]
    public async Task OnAuthorizationAsync_ComResourceTipo_InformaOResource()
    {
        Principal(Decisao(ResultadoDoAcesso.Permitido));

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo) { ResourceTipo = "catalogo" }
            .OnAuthorizationAsync(Contexto());

        await _acesso.Received(1).AvaliarAsync(
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext?>(r => r!.ResourceTipo == "catalogo"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Negado na permissão principal, a alternativa em alguma unidade decide")]
    public async Task OnAuthorizationAsync_ComAlternativa_NegadoNaPrincipal_ConsultaAlgumaUnidade()
    {
        Principal(Decisao(ResultadoDoAcesso.Negado));
        Alternativa(Decisao(ResultadoDoAcesso.Permitido, PermissaoAutorizacao.PerfisManter));
        AuthorizationFilterContext contexto = Contexto();

        await ComAlternativa(PermissaoAutorizacao.ConsultarCatalogo).OnAuthorizationAsync(contexto);

        contexto.Result.Should().BeNull();
        await _acesso.Received(1).AvaliarEmAlgumaUnidadeAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == "autorizacao:perfis:manter"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Permitido na principal, a alternativa nem é consultada")]
    public async Task OnAuthorizationAsync_ComAlternativa_PermitidoNaPrincipal_NaoConsultaAlgumaUnidade()
    {
        Principal(Decisao(ResultadoDoAcesso.Permitido));

        await ComAlternativa(PermissaoAutorizacao.ConsultarCatalogo).OnAuthorizationAsync(Contexto());

        await _acesso.DidNotReceive().AvaliarEmAlgumaUnidadeAsync(Arg.Any<PermissionRequirement>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Principal impedida: a alternativa não é consultada e a resposta é 403")]
    public async Task OnAuthorizationAsync_PrincipalImpedida_NaoConsultaAAlternativa()
    {
        Principal(Decisao(ResultadoDoAcesso.Impedido, PermissaoAutorizacao.ConcessoesAdministrar));
        AuthorizationFilterContext contexto = Contexto();

        await ComAlternativa(PermissaoAutorizacao.ConcessoesAdministrar).OnAuthorizationAsync(contexto);

        contexto.Result.Should().BeOfType<ForbidResult>();
        await _acesso.DidNotReceive().AvaliarEmAlgumaUnidadeAsync(Arg.Any<PermissionRequirement>(), Arg.Any<CancellationToken>());
        Registrados().Should().ContainSingle().Which.Resultado.Should().Be(ResultadoDoAcesso.Impedido);
    }

    [Fact(DisplayName = "Alternativa permitida: só ela é registrada, e não a negativa da principal")]
    public async Task OnAuthorizationAsync_AlternativaPermite_NaoRegistraANegativaDaPrincipal()
    {
        Principal(Decisao(ResultadoDoAcesso.Negado, PermissaoAutorizacao.ConcessoesAdministrar));
        Alternativa(Decisao(ResultadoDoAcesso.Negado, PermissaoAutorizacao.ConcessoesAdministrar), Decisao(ResultadoDoAcesso.Permitido, PermissaoAutorizacao.ConcessoesAdministrar));

        await ComAlternativa(PermissaoAutorizacao.ConcessoesAdministrar).OnAuthorizationAsync(Contexto());

        Registrados().Should().ContainSingle().Which.Permitido.Should().BeTrue();
    }

    [Theory(DisplayName = "Principal e alternativa negadas, em qualquer ordem de negado e impedido: 403 e a negativa de cada uma é registrada")]
    [InlineData(ResultadoDoAcesso.Negado, ResultadoDoAcesso.Impedido)]
    [InlineData(ResultadoDoAcesso.Impedido, ResultadoDoAcesso.Negado)]
    public async Task OnAuthorizationAsync_AmbasNegam_RegistraAsNegativas(ResultadoDoAcesso primeira, ResultadoDoAcesso segunda)
    {
        Principal(Decisao(ResultadoDoAcesso.Negado, PermissaoAutorizacao.ConcessoesAdministrar));
        Alternativa(
            Decisao(primeira, PermissaoAutorizacao.ConcessoesAdministrar),
            Decisao(segunda, PermissaoAutorizacao.ConcessoesAdministrar));
        AuthorizationFilterContext contexto = Contexto();

        await ComAlternativa(PermissaoAutorizacao.ConcessoesAdministrar).OnAuthorizationAsync(contexto);

        contexto.Result.Should().BeOfType<ForbidResult>();
        Registrados().Select(static r => r.Resultado).Should().Equal(
            [ResultadoDoAcesso.Negado, primeira, segunda], "a negativa de cada unidade é gravada, na ordem em que foram avaliadas");
        DesfechoEntregue().Last().Resultado.Should().Be(segunda, "sem unidade que permita, vale o desfecho da última avaliada");
    }

    [Fact(DisplayName = "Negado na principal, a outra permissão decide sobre o mesmo resource, com a sensibilidade dela")]
    public async Task OnAuthorizationAsync_OuPermissao_NegadoNaPrincipal_AvaliaAOutra()
    {
        PrincipalEOutra(Decisao(ResultadoDoAcesso.Negado, PermissaoAutorizacao.ConcessoesAdministrar), Decisao(ResultadoDoAcesso.Permitido, PermissaoAutorizacao.ConcessoesConsultar));
        AuthorizationFilterContext contexto = Contexto();

        await ComOutraPermissao().OnAuthorizationAsync(contexto);

        contexto.Result.Should().BeNull();
        await _acesso.Received(1).AvaliarAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == "autorizacao:concessoes:consultar"),
            Arg.Is<ResourceContext?>(r => r!.ResourceTipo == "concessao"),
            Arg.Any<CancellationToken>());
        Registrados().Should().ContainSingle().Which.Permissao.Should().Be("autorizacao:concessoes:consultar");
    }

    [Fact(DisplayName = "Principal e outra permissão negadas: 403 e as duas negativas são registradas")]
    public async Task OnAuthorizationAsync_OuPermissao_AmbasNegam_RegistraAsDuas()
    {
        PrincipalEOutra(Decisao(ResultadoDoAcesso.Negado, PermissaoAutorizacao.ConcessoesAdministrar), Decisao(ResultadoDoAcesso.Negado, PermissaoAutorizacao.ConcessoesConsultar));
        AuthorizationFilterContext contexto = Contexto();

        await ComOutraPermissao().OnAuthorizationAsync(contexto);

        contexto.Result.Should().BeOfType<ForbidResult>();
        Registrados().Select(static r => r.Permissao).Should().Equal("autorizacao:concessoes:administrar", "autorizacao:concessoes:consultar");
    }

    [Fact(DisplayName = "Permitido na principal, a outra permissão nem é avaliada")]
    public async Task OnAuthorizationAsync_OuPermissao_PermitidoNaPrincipal_NaoAvaliaAOutra()
    {
        Principal(Decisao(ResultadoDoAcesso.Permitido, PermissaoAutorizacao.ConcessoesAdministrar));

        await ComOutraPermissao().OnAuthorizationAsync(Contexto());

        await _acesso.Received(1).AvaliarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A exigência não combina as duas alternativas: em alguma unidade e outra permissão")]
    public void Construtor_DuasAlternativas_Falha()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar = () => new(PermissaoAutorizacao.ConsultarCatalogo)
        {
            OuEmAlgumaUnidade = PermissaoAutorizacao.PerfisManter,
            OuPermissao = PermissaoAutorizacao.ConcessoesConsultar,
        };

        criar.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Permissão operacional registra a negativa")]
    public async Task OnAuthorizationAsync_PermissaoOperacionalNegada_Registra()
    {
        Principal(Decisao(ResultadoDoAcesso.Negado));

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo).OnAuthorizationAsync(Contexto());

        Registrados().Should().ContainSingle().Which.Permitido.Should().BeFalse();
    }

    [Fact(DisplayName = "Permissão operacional permitida não registra")]
    public async Task OnAuthorizationAsync_PermissaoOperacionalPermitida_NaoRegistra()
    {
        Principal(Decisao(ResultadoDoAcesso.Permitido));

        await new ExigePermissaoAttribute<PermissaoAutorizacao>(PermissaoAutorizacao.ConsultarCatalogo).OnAuthorizationAsync(Contexto());

        Registrados().Should().BeEmpty();
    }

    [Fact(DisplayName = "Permissão de outro enum não constrói o atributo")]
    public void Construtor_PermissaoDeOutroEnum_Falha()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar = () => new(DayOfWeek.Monday);

        criar.Should().Throw<ArgumentException>().WithMessage("*PermissaoAutorizacao*");
    }

    [Fact(DisplayName = "ResourceTipo em branco falha ao construir o atributo, não na requisição")]
    public void ResourceTipo_EmBranco_Falha()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar =
            () => new(PermissaoAutorizacao.ConsultarCatalogo) { ResourceTipo = " " };

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ResourceTipo fora da convenção falha ao construir o atributo, não na requisição")]
    public void ResourceTipo_ForaDaConvencao_Falha()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar =
            () => new(PermissaoAutorizacao.ConsultarCatalogo) { ResourceTipo = "TiposDocumento" };

        criar.Should().Throw<ArgumentException>().WithMessage("*kebab-case*");
    }

    private void Principal(DecisaoDeAcesso decisao) =>
        _acesso.AvaliarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(decisao);

    private void Alternativa(params DecisaoDeAcesso[] decisoes) =>
        _acesso.AvaliarEmAlgumaUnidadeAsync(Arg.Any<PermissionRequirement>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<DecisaoDeAcesso>)decisoes);

    private static ExigePermissaoAttribute<PermissaoAutorizacao> ComAlternativa(PermissaoAutorizacao principal) =>
        new(principal) { OuEmAlgumaUnidade = PermissaoAutorizacao.PerfisManter };

    private static ExigePermissaoAttribute<PermissaoAutorizacao> ComOutraPermissao() =>
        new(PermissaoAutorizacao.ConcessoesAdministrar) { ResourceTipo = "concessao", OuPermissao = PermissaoAutorizacao.ConcessoesConsultar };

    private void PrincipalEOutra(DecisaoDeAcesso principal, DecisaoDeAcesso outra) =>
        _acesso.AvaliarAsync(Arg.Any<PermissionRequirement>(), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(principal, outra);

    private IReadOnlyCollection<DecisaoDeAcesso> DesfechoEntregue() =>
        _acesso.ReceivedCalls().Where(static c => c.GetMethodInfo().Name == nameof(IVerificadorDeAcesso.RegistrarDesfechoAsync))
            .Select(static c => (IReadOnlyCollection<DecisaoDeAcesso>)c.GetArguments()[0]!).Single();

    // O que a política de registro manda gravar do desfecho que o filtro entregou ao verificador.
    private List<RegistroDecisaoAcesso> Registrados()
    {
        List<RegistroDecisaoAcesso> registros = [];
        foreach (NSubstitute.Core.ICall chamada in _acesso.ReceivedCalls()
            .Where(static c => c.GetMethodInfo().Name == nameof(IVerificadorDeAcesso.RegistrarDesfechoAsync)))
        {
            object[] argumentos = chamada.GetArguments()!;
            registros.AddRange(PoliticaDeRegistroDeAcesso.Desfecho(
                (IReadOnlyCollection<DecisaoDeAcesso>)argumentos[0]!, (IntencaoDoRegistro)argumentos[1]!));
        }

        return registros;
    }

    private static DecisaoDeAcesso Decisao(
        ResultadoDoAcesso resultado, PermissaoAutorizacao permissao = PermissaoAutorizacao.ConsultarCatalogo) =>
        DecisaoDeAcessoDeTeste.Tomada(
            resultado, CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Requisito);

    private AuthorizationFilterContext Contexto()
    {
        ServiceProvider servicos = new ServiceCollection().AddSingleton(_acesso).BuildServiceProvider();
        DefaultHttpContext http = new() { RequestServices = servicos };

        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }
}
