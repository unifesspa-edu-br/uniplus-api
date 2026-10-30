namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Concessoes;

using System.Diagnostics.CodeAnalysis;
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

/// <summary>
/// Consulta, trilha e revogação de concessões pelo endpoint real, contra Postgres efêmero. As
/// concessões nascem pelo repositório, porque a criação pertence ao pedido aprovado e à
/// instalação. As personas são as do portal: Diogo Souza e Isabella Figueiredo são gestores de
/// acessos (Isabella também exerce a proteção de dados), Betina Neves é a terceira gestora e Leandro
/// Viana é o beneficiário.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ConcessaoEndpointTests
{
    private const string Rota = "/api/autorizacao/concessoes";
    private const string RotaPerfis = "/api/autorizacao/perfis";
    private const string Administrar = "autorizacao:concessoes:administrar";
    private const string ConsultarCatalogo = "autorizacao:permissoes:consultar-catalogo";
    private const string Motivo = "Mudou de função na unidade.";
    private const string EmailDoBeneficiario = "beneficiario@uniplus-test.local";

    private static readonly Guid GestorDeAcessosId = new("019a0000-0000-7000-8000-000000000001");
    private static readonly Guid ProtecaoDeDadosId = new("019a0000-0000-7000-8000-000000000002");

    private static readonly Identidade Diogo = Pessoa("diogo.souza");
    private static readonly Identidade Isabella = Pessoa("isabella.figueiredo");
    private static readonly Identidade Betina = Pessoa("betina.neves");
    private static readonly Identidade Leandro = Pessoa("leandro.viana");

    private readonly AutorizacaoEndpointFixture _fixture;

    public ConcessaoEndpointTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

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
        await LimparConcessoesAsync(ProtecaoDeDadosId);
        await ConcederAsync(ProtecaoDeDadosId, Isabella, EscopoDaConcessao.DaInstituicao());

        Exception falha = await FalhaAoConcederAsync(ProtecaoDeDadosId, Isabella, EscopoDaConcessao.DaInstituicao());

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
        Guid vencida = await ConcederAsync(
            perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade), validaAteDaquiAMinutos: 1);
        await EnvelhecerAsync(vencida);

        Exception falha = await FalhaAoConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(unidade));
        ConcessaoViolacoes.EhConcessaoDuplicada(falha).Should().BeTrue();

        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        using HttpResponseMessage revogada = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{vencida}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario });
        revogada.StatusCode.Should().Be(HttpStatusCode.NoContent);

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
        raiz.GetProperty("beneficiario").GetProperty("sub").GetString().Should().Be(Leandro.Sub);
        raiz.GetProperty("perfil").GetProperty("id").GetGuid().Should().Be(perfil);
        raiz.GetProperty("escopo").GetProperty("tipo").GetString().Should().Be("unidade");
        raiz.GetProperty("escopo").GetProperty("unidadeId").GetGuid().Should().Be(unidade);
        raiz.GetProperty("origem").GetProperty("tipo").GetString().Should().Be("pedido");
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

        foreach (object corpo in new object[]
        {
            new { motivo = Motivo, confirmacaoEmail = "outra.pessoa@uniplus-test.local" },
            new { motivo = Motivo },
        })
        {
            using HttpResponseMessage recusada = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/revogacao", corpo);
            recusada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await recusada.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.concessao.confirmacao_divergente");
        }

        using JsonDocument concessao = await LerAsync(diogo, $"{Rota}/{id}");
        concessao.RootElement.GetProperty("ativa").GetBoolean().Should().BeTrue();

        using HttpResponseMessage certa = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail = $"  {EmailDoBeneficiario.ToUpperInvariant()} " });
        certa.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "A proteção de dados também revoga")]
    public async Task Revogar_PelaProtecaoDeDados_Devolve204()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient isabella = await ClienteDaProtecaoDeDadosAsync(Isabella);

        using HttpResponseMessage resposta = await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario });

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using JsonDocument concessao = await LerAsync(isabella, $"{Rota}/{id}");
        concessao.RootElement.GetProperty("revogadaPor").GetProperty("sub").GetString().Should().Be(Isabella.Sub);
    }

    [Fact(DisplayName = "Os dois perfis de governança trazem a permissão de administrar concessões")]
    public async Task Governanca_ReuneAPermissaoDeAdministrar()
    {
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);

        foreach (Guid id in new[] { GestorDeAcessosId, ProtecaoDeDadosId })
        {
            using JsonDocument perfil = await LerAsync(diogo, $"{RotaPerfis}/{id}");
            perfil.RootElement.GetProperty("permissoes").EnumerateArray()
                .Select(p => p.GetProperty("codigo").GetString()).Should().Contain(Administrar);
        }
    }

    [Fact(DisplayName = "Revogar concessão já revogada devolve 422 e preserva a primeira revogação")]
    public async Task Revogar_JaRevogada_Devolve422()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        using HttpClient isabella = await ClienteDaProtecaoDeDadosAsync(Isabella);
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
        using HttpClient isabella = await ClienteDaProtecaoDeDadosAsync(Isabella);

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
        using HttpClient isabella = await ClienteDaProtecaoDeDadosAsync(Isabella);

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

        HttpResponseMessage[] respostas = await Task.WhenAll(
            EnviarAsync(clienteA, HttpMethod.Post, $"{Rota}/{diogoId}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario }),
            EnviarAsync(clienteB, HttpMethod.Post, $"{Rota}/{isabellaId}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario }));

        try
        {
            respostas.Select(r => r.StatusCode).Should().BeEquivalentTo(
                [HttpStatusCode.NoContent, HttpStatusCode.UnprocessableEntity]);
        }
        finally
        {
            foreach (HttpResponseMessage r in respostas)
            {
                r.Dispose();
            }
        }
    }

    [Fact(DisplayName = "Duas revogações simultâneas da mesma concessão: só uma vale")]
    public async Task Revogar_SimultaneasDaMesmaConcessao_UmaSoVale()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient clienteA = await ClienteDoGestorAsync(Diogo);
        using HttpClient clienteB = await ClienteDaProtecaoDeDadosAsync(Isabella);

        HttpResponseMessage[] respostas = await Task.WhenAll(
            EnviarAsync(clienteA, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario }),
            EnviarAsync(clienteB, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario }));

        try
        {
            respostas.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
            respostas.Single(r => r.StatusCode != HttpStatusCode.NoContent).StatusCode
                .Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        }
        finally
        {
            foreach (HttpResponseMessage r in respostas)
            {
                r.Dispose();
            }
        }
    }

    [Fact(DisplayName = "A trilha traz a concessão e a revogação em ordem, com autor e motivo")]
    public async Task Trilha_ComConcessaoERevogacao_TrazOsDoisEventos()
    {
        Guid perfil = await CriarPerfilComumAsync();
        Guid id = await ConcederAsync(perfil, Leandro, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
        using HttpClient isabella = await ClienteDaProtecaoDeDadosAsync(Isabella);
        (await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/revogacao", new { motivo = Motivo, confirmacaoEmail = EmailDoBeneficiario }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument trilha = await LerAsync(isabella, $"{Rota}/{id}/trilha");

        JsonElement[] eventos = [.. trilha.RootElement.EnumerateArray()];
        eventos.Select(e => e.GetProperty("tipo").GetString()).Should().Equal("concedida", "revogada");
        eventos[0].GetProperty("autor").GetProperty("sub").GetString().Should().Be(Diogo.Sub);
        eventos[1].GetProperty("autor").GetProperty("sub").GetString().Should().Be(Isabella.Sub);
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
        using HttpClient anonimo = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await anonimo.GetAsync(new Uri(Rota, UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static Identidade Pessoa(string sub) => new(TestAuthHandler.DefaultIssuer, sub);

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> LerAsync(HttpClient client, string url)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<Guid[]> IdsAsync(HttpClient client, string filtro)
    {
        using JsonDocument pagina = await LerAsync(client, Rota + filtro);
        JsonElement itens = pagina.RootElement.ValueKind == JsonValueKind.Array
            ? pagina.RootElement
            : pagina.RootElement.GetProperty("items");

        return [.. itens.EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }

    private async Task<Guid> CriarPerfilComumAsync()
    {
        using HttpClient diogo = await ClienteDoGestorAsync(Diogo);
        using HttpResponseMessage resposta = await EnviarAsync(
            diogo,
            HttpMethod.Post,
            RotaPerfis,
            new { nome = $"Perfil {Guid.NewGuid():N}", descricao = "Perfil de teste das concessões.", permissoes = new[] { "teste:inscricoes:operar" } });
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return Guid.Parse(resposta.Headers.Location!.Segments[^1]);
    }

    // A vigência só vence com o tempo: o teste a recua no banco em vez de esperar.
    private async Task EnvelhecerAsync(Guid id)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '1 hour', valida_ate = now() - interval '1 minute' WHERE id = {0}", id);
    }

    private async Task<Guid> ConcederAsync(
        Guid perfilId, Identidade beneficiario, EscopoDaConcessao escopo, int? validaAteDaquiAMinutos = null)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        Concessao concessao = await MontarAsync(scope, perfilId, beneficiario, escopo, validaAteDaquiAMinutos);
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        await repositorio.AdicionarAsync(concessao);
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
        return concessao.Id;
    }

    private async Task<Exception> FalhaAoConcederAsync(Guid perfilId, Identidade beneficiario, EscopoDaConcessao escopo)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        Concessao concessao = await MontarAsync(scope, perfilId, beneficiario, escopo);
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        await repositorio.AdicionarAsync(concessao);

        return await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync());
    }

    private static async Task<Concessao> MontarAsync(
        AsyncServiceScope scope, Guid perfilId, Identidade beneficiario, EscopoDaConcessao escopo, int? validaAteDaquiAMinutos = null)
    {
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        TimeProvider relogio = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        Perfil perfil = (await repositorio.ObterParaConcederAsync(perfilId))!;
        Result<Concessao> concessao = Concessao.Conceder(
            beneficiario,
            perfil,
            escopo,
            new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
            Diogo,
            relogio.GetUtcNow(),
            scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>(),
            validaAteDaquiAMinutos is { } minutos ? relogio.GetUtcNow().AddMinutes(minutos) : null,
            EmailDoBeneficiario);

        concessao.IsSuccess.Should().BeTrue(string.Join("; ", concessao.Errors.Select(e => e.Error.Message)));
        return concessao.Value!;
    }

    private async Task LimparConcessoesAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM autorizacao.concessoes WHERE perfil_id = {0}", perfilId);
    }

    // O token só identifica a pessoa; o que ela pode vem da concessão gravada, como em produção.
    private Task<HttpClient> ClienteDoGestorAsync(Identidade pessoa) =>
        ClienteDeGovernancaAsync(pessoa, TipoDeGovernanca.GestorDeAcessos);

    private Task<HttpClient> ClienteDaProtecaoDeDadosAsync(Identidade pessoa) =>
        ClienteDeGovernancaAsync(pessoa, TipoDeGovernanca.ProtecaoDeDados);

    private async Task<HttpClient> ClienteDeGovernancaAsync(Identidade pessoa, TipoDeGovernanca governanca)
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, pessoa.Sub, governanca);
        return Cliente(pessoa);
    }

    private async Task<HttpClient> ClienteComPermissoesAsync(Identidade pessoa, params string[] permissoes)
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, pessoa.Sub, permissoes);
        return Cliente(pessoa);
    }

    private HttpClient Cliente(Identidade pessoa)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, pessoa.Sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, pessoa.Sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");
        return client;
    }
}
