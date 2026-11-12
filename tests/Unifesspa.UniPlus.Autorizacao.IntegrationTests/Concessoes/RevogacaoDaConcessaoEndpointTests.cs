namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A revogação das concessões e a trilha pelo endpoint real, com o mínimo de gestores e as revogações simultâneas.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class RevogacaoDaConcessaoEndpointTests : ConcessaoEndpointTestBase
{
    public RevogacaoDaConcessaoEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Revogar encerra a concessão e grava quem revogou e o motivo")]
    public async Task Revogar_Valida_GravaAutorEMotivo()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        using HttpResponseMessage resposta = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario });
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());

        using JsonDocument concessao = await LerAsync(diogo, $"{Rota}/{id}");
        concessao.RootElement.GetProperty("ativa").GetBoolean().Should().BeFalse();
        concessao.RootElement.GetProperty("revogadaPor").GetProperty("sub").GetString().Should().Be(Diogo.Sub);
        concessao.RootElement.GetProperty("motivoRevogacao").GetString().Should().Be(Motivo);
    }

    [Fact(DisplayName = "O e-mail errado ou ausente recusa a revogação com o código da confirmação e a concessão segue ativa")]
    public async Task Revogar_EmailDivergente_RecusaENaoRevoga()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        foreach (string? email in new[] { "outra.pessoa@uniplus-test.local", null })
        {
            using HttpResponseMessage recusada = await RevogarAsync(diogo, id, email);
            recusada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await recusada.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.concessao.confirmacao_divergente");
        }

        using JsonDocument concessao = await LerAsync(diogo, $"{Rota}/{id}");
        concessao.RootElement.GetProperty("ativa").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "O e-mail certo confirma a revogação mesmo com espaços e outra caixa")]
    public async Task Revogar_EmailComEspacosECaixa_Revoga()
    {
        Guid id = await ConcederAsync(await CriarPerfilComumAsync(), Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        (await RevogarAsync(diogo, id, $"  {EmailDoBeneficiario.ToUpperInvariant()} ")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "O Representante do controlador também revoga")]
    public async Task Revogar_PeloRepresentante_Devolve204()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient isabella = await ClienteDoRepresentanteAsync(Isabella);

        using HttpResponseMessage resposta = await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using JsonDocument concessao = await LerAsync(isabella, $"{Rota}/{id}");
        concessao.RootElement.GetProperty("revogadaPor").GetProperty("sub").GetString().Should().Be(Isabella.Sub);
    }

    [Fact(DisplayName = "Os dois perfis de governança trazem a permissão de administrar concessões")]
    public async Task Governanca_ReuneAPermissaoDeAdministrar()
    {
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        foreach (Guid id in new[] { GestorDeAcessosId, RepresentanteDoControladorId })
        {
            using JsonDocument perfil = await LerAsync(diogo, $"{RotaPerfis}/{id}");
            perfil.RootElement.GetProperty("composicaoEmVigor").GetProperty("permissoes").EnumerateArray()
                .Select(p => p.GetProperty("codigo").GetString()).Should().Contain(Administrar);
        }
    }

    [Fact(DisplayName = "Revogar concessão já revogada devolve 422 e preserva a primeira revogação")]
    public async Task Revogar_JaRevogada_Devolve422()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        using HttpClient isabella = await ClienteDoRepresentanteAsync(Isabella);
        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage segunda = await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = "Outro motivo." });

        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.concessao.ja_revogada");
        using JsonDocument concessao = await LerAsync(diogo, $"{Rota}/{id}");
        concessao.RootElement.GetProperty("revogadaPor").GetProperty("sub").GetString().Should().Be(Diogo.Sub);
    }

    [Fact(DisplayName = "Motivo ausente devolve 422 e a concessão segue ativa")]
    public async Task Revogar_SemMotivo_Devolve422()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        using HttpResponseMessage resposta = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = "  ", confirmacaoEmail = EmailDoBeneficiario });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.concessao.motivo_invalido");
        (await LerAsync(diogo, $"{Rota}/{id}")).RootElement.GetProperty("ativa").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Com dois gestores ativos, revogar um é recusado e a concessão segue ativa")]
    public async Task Revogar_DoisGestores_RecusaOMinimo()
    {
        await LimparConcessoesAsync(GestorDeAcessosId);
        Guid diogoId = await ConcederAsync(GestorDeAcessosId, Diogo, EscopoDaConcessao.DaInstituicao());
        await ConcederAsync(GestorDeAcessosId, Isabella, EscopoDaConcessao.DaInstituicao());
        using HttpClient isabella = await ClienteDoRepresentanteAsync(Isabella);

        using HttpResponseMessage resposta = await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{diogoId}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario });

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.concessao.minimo_de_gestores");
        (await LerAsync(isabella, $"{Rota}/{diogoId}")).RootElement.GetProperty("ativa").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Com três gestores ativos, revogar um é aceito")]
    public async Task Revogar_TresGestores_Aceita()
    {
        await LimparConcessoesAsync(GestorDeAcessosId);
        Guid diogoId = await ConcederAsync(GestorDeAcessosId, Diogo, EscopoDaConcessao.DaInstituicao());
        await ConcederAsync(GestorDeAcessosId, Isabella, EscopoDaConcessao.DaInstituicao());
        await ConcederAsync(GestorDeAcessosId, Betina, EscopoDaConcessao.DaInstituicao());
        using HttpClient isabella = await ClienteDoRepresentanteAsync(Isabella);

        using HttpResponseMessage resposta = await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{diogoId}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Duas revogações simultâneas de gestores diferentes, de três para um, não passam as duas")]
    public async Task Revogar_SimultaneasDeTresParaUm_UmaSoPassa()
    {
        await LimparConcessoesAsync(GestorDeAcessosId);
        Guid diogoId = await ConcederAsync(GestorDeAcessosId, Diogo, EscopoDaConcessao.DaInstituicao());
        Guid isabellaId = await ConcederAsync(GestorDeAcessosId, Isabella, EscopoDaConcessao.DaInstituicao());
        await ConcederAsync(GestorDeAcessosId, Betina, EscopoDaConcessao.DaInstituicao());
        using HttpClient clienteA = await ClienteDoGestorAsync(Betina);
        using HttpClient clienteB = await ClienteDoGestorAsync(Betina);

        HttpStatusCode[] status = await StatusDasSimultaneasAsync(RevogarAsync(clienteA, diogoId), RevogarAsync(clienteB, isabellaId));

        status.Should().BeEquivalentTo([HttpStatusCode.NoContent, HttpStatusCode.UnprocessableEntity]);
    }

    [Fact(DisplayName = "Duas revogações simultâneas da mesma concessão: só uma vale")]
    public async Task Revogar_SimultaneasDaMesmaConcessao_UmaSoVale()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient clienteA = await ClienteDoGestorAsync(Diogo);
        using HttpClient clienteB = await ClienteDoRepresentanteAsync(Isabella);

        HttpStatusCode[] status = await StatusDasSimultaneasAsync(RevogarAsync(clienteA, id), RevogarAsync(clienteB, id));

        status.Count(s => s == HttpStatusCode.NoContent).Should().Be(1);
        status.Single(s => s != HttpStatusCode.NoContent).Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "A trilha traz a concessão e a revogação em ordem, com autor e motivo")]
    public async Task Trilha_ComConcessaoERevogacao_TrazOsDoisEventos()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient isabella = await ClienteDoRepresentanteAsync(Isabella);
        (await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument trilha = await LerAsync(isabella, $"{Rota}/{id}/trilha");

        JsonElement[] eventos = [.. trilha.RootElement.EnumerateArray()];
        eventos.Select(e => (e.GetProperty("tipo").GetString(), e.GetProperty("autor").GetProperty("sub").GetString()))
            .Should().Equal(("concedida", Diogo.Sub), ("revogada", Isabella.Sub));
        eventos[1].GetProperty("detalhe").GetString().Should().Be(Motivo);
        eventos[0].GetProperty("instante").GetDateTimeOffset()
            .Should().BeBefore(eventos[1].GetProperty("instante").GetDateTimeOffset());
    }

    [Theory(DisplayName = "Sem a permissão de administrar concessões, toda operação é 403, inclusive a leitura")]
    [InlineData("GET", "")]
    [InlineData("GET", "/019a0000-0000-7000-8000-000000000099")]
    [InlineData("GET", "/019a0000-0000-7000-8000-000000000099/trilha")]
    [InlineData("POST", "/019a0000-0000-7000-8000-000000000099/revogacao")]
    public async Task SemPermissao_Devolve403(string metodo, string sufixo)
    {
        using HttpClient leandro = await ClienteComPermissoesAsync(Leandro, ConsultarCatalogo);

        using HttpResponseMessage resposta = await EnviarAsync(
            leandro, new HttpMethod(metodo), Rota + sufixo, metodo == "POST" ? new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario } : null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory(DisplayName = "Não há rota de criação nem de edição de concessão")]
    [InlineData("POST", "")]
    [InlineData("PUT", "/019a0000-0000-7000-8000-000000000099")]
    [InlineData("PATCH", "/019a0000-0000-7000-8000-000000000099")]
    [InlineData("DELETE", "/019a0000-0000-7000-8000-000000000099")]
    public async Task SemRotaDeCriacaoNemEdicao(string metodo, string sufixo)
    {
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        using HttpResponseMessage resposta = await EnviarAsync(diogo, new HttpMethod(metodo), Rota + sufixo, new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario });

        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Sem autenticação a leitura é 401, não 403")]
    public async Task SemAutenticacao_Devolve401()
    {
        using HttpClient anonimo = Fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await anonimo.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
