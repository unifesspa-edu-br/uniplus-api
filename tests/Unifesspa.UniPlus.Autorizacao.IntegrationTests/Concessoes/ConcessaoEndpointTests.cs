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

/// <summary>A unicidade, a consulta e o detalhe das concessões pelo endpoint real.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class ConcessaoEndpointTests : ConcessaoEndpointTestBase
{
    public ConcessaoEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Segunda concessão ativa igual na mesma unidade é barrada pelo índice único")]
    public async Task Gravar_AtivaIgualNaUnidade_ViolaOIndiceUnico()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid unidade = Guid.NewGuid();
        await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade));

        Exception falha = await FalhaAoConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade));

        ConcessaoViolacoes.EhConcessaoDuplicada(falha).Should().BeTrue();
    }

    [Fact(DisplayName = "Segunda concessão ativa igual no escopo da instituição é barrada, apesar da unidade nula")]
    public async Task Gravar_AtivaIgualNaInstituicao_ViolaOIndiceUnico()
    {
        await LimparConcessoesAsync(RepresentanteDoControladorId);
        await ConcederAsync(RepresentanteDoControladorId, Isabella, EscopoDaConcessao.DaInstituicao());

        Exception falha = await FalhaAoConcederAsync(RepresentanteDoControladorId, Isabella, EscopoDaConcessao.DaInstituicao());

        ConcessaoViolacoes.EhConcessaoDuplicada(falha).Should().BeTrue();
    }

    [Fact(DisplayName = "Revogada a concessão, a mesma pode ser concedida de novo")]
    public async Task Gravar_AposRevogacao_AceitaConcederDeNovo()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid unidade = Guid.NewGuid();
        Guid primeira = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        using HttpResponseMessage revogada = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{primeira}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario });
        revogada.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid segunda = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade));

        segunda.Should().NotBe(primeira);
    }

    [Fact(DisplayName = "Concessão vencida igual continua barrando a nova até ser revogada")]
    public async Task Gravar_VencidaIgual_ViolaOIndiceUnicoAteARevogacao()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid unidade = Guid.NewGuid();
        Guid vencida = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade), validaAteDaquiAMinutos: 1);
        await EnvelhecerAsync(vencida);

        Exception falha = await FalhaAoConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade));
        ConcessaoViolacoes.EhConcessaoDuplicada(falha).Should().BeTrue();

        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        (await RevogarAsync(diogo, vencida)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid nova = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade));
        nova.Should().NotBe(vencida);
    }

    [Fact(DisplayName = "A lista traz só as ativas por padrão e a revogada sai delas")]
    public async Task Listar_PorPadrao_TrazSoAsAtivas()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid ativa = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        Guid revogada = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{revogada}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        string filtro = $"?pessoa={Leandro.Sub}&emissor={Uri.EscapeDataString(Leandro.Emissor)}&perfil={perfil}";

        (await IdsAsync(diogo, filtro)).Should().Equal(ativa);
        (await IdsAsync(diogo, filtro + "&situacao=revogada")).Should().Equal(revogada);
        (await IdsAsync(diogo, filtro + "&situacao=todas")).Should().BeEquivalentTo([ativa, revogada]);
    }

    [Fact(DisplayName = "Situação vencida traz só a não revogada com vigência vencida, sem distinguir maiúsculas")]
    public async Task Listar_Vencida_TrazSoAVigenciaVencida()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid vencida = await ConcederAsync(
            perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()), validaAteDaquiAMinutos: 1);
        await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()), validaAteDaquiAMinutos: 600);
        await EnvelhecerAsync(vencida);
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        string filtro = $"?pessoa={Leandro.Sub}&emissor={Uri.EscapeDataString(Leandro.Emissor)}&perfil={perfil}";

        (await IdsAsync(diogo, filtro + "&situacao=VENCIDA")).Should().Equal(vencida);
        (await IdsAsync(diogo, filtro)).Should().HaveCount(1).And.NotContain(vencida);
    }

    [Fact(DisplayName = "A lista filtra pela unidade")]
    public async Task Listar_PorUnidade_TrazSoDaUnidade()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid unidade = Guid.NewGuid();
        Guid daUnidade = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade));
        await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        (await IdsAsync(diogo, $"?unidade={unidade}")).Should().Equal(daUnidade);
    }

    [Fact(DisplayName = "Pessoa sem emissor é recusada com 400, e o emissor sem a pessoa também")]
    public async Task Listar_PessoaSemEmissor_Devolve400()
    {
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        using HttpResponseMessage semEmissor = await diogo.GetAsync(new Uri($"{Rota}?pessoa={Leandro.Sub}", UriKind.Relative));
        semEmissor.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await semEmissor.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.concessao.filtro_de_pessoa_invalido");
        (await diogo.GetAsync(new Uri($"{Rota}?emissor={Uri.EscapeDataString(Leandro.Emissor)}", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Situação desconhecida é recusada com 400")]
    public async Task Listar_SituacaoDesconhecida_Devolve400()
    {
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri($"{Rota}?situacao=expirada", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.concessao.filtro_de_situacao_invalido");
    }

    [Fact(DisplayName = "O detalhe devolve beneficiário, perfil, escopo e origem sem nome nem CPF")]
    public async Task Obter_Concessao_DevolveOContrato()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid unidade = Guid.NewGuid();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        using JsonDocument concessao = await LerAsync(diogo, $"{Rota}/{id}");

        JsonElement raiz = concessao.RootElement;
        (raiz.GetProperty("beneficiario").GetProperty("sub").GetString(), raiz.GetProperty("perfil").GetProperty("id").GetGuid(), raiz.GetProperty("origem").GetProperty("tipo").GetString())
            .Should().Be((Leandro.Sub, perfil, "pedido"));
        (raiz.GetProperty("escopo").GetProperty("tipo").GetString(), raiz.GetProperty("escopo").GetProperty("unidadeId").GetGuid()).Should().Be(("unidade", unidade));
        raiz.GetProperty("ativa").GetBoolean().Should().BeTrue();
        raiz.GetProperty("revogadaEm").ValueKind.Should().Be(JsonValueKind.Null);
        raiz.EnumerateObject().Select(p => p.Name).Should().NotContain(["cpf", "nome"]);
    }

    [Fact(DisplayName = "Concessão inexistente devolve 404 com o código de erro")]
    public async Task Obter_Inexistente_Devolve404()
    {
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri($"{Rota}/{Guid.NewGuid()}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.concessao.nao_encontrada");
    }
}
