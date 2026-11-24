namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O Gestor de proteção de dados lê o aceite do termo pelo detalhe do pedido, com a permissão de consultar os pedidos, e a
/// leitura é registrada como as demais consultas dele: o aceite traz o nome do beneficiário, dado pessoal. Cecília Moura
/// é o Gestor de proteção de dados.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class AceiteDoTermoNaConsultaDoGestorDeProtecaoDeDadosNoHostTests : TermoNoHostTestBase
{
    private static readonly Persona Cecilia = new("cecilia.moura", "Cecília Ester Aurora Moura", "cecilia_ester_moura@uniplus-test.local");

    public AceiteDoTermoNaConsultaDoGestorDeProtecaoDeDadosNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    private RegistroDeDecisoesDeTeste Registro => Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();

    [Fact(DisplayName = "O Gestor de proteção de dados lê o aceite do termo pelo detalhe do pedido, e a leitura é registrada com a permissão de consulta")]
    public async Task GestorDeProtecaoDeDados_LeOAceite_ELeituraRegistrada()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        Guid id = (await CorpoAsync(await PedirAsync(betina, perfil, unidade, Vigente!.Id), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using HttpClient cecilia = await GestorDeProtecaoDeDadosAsync();
        int antes = Registro.Registrados.Count;

        JsonElement aceite = (await TermoDoPedidoAsync(cecilia, id)).GetProperty("aceite");

        aceite.GetProperty("pessoa").GetProperty("nome").GetString().Should().Be(Betina.Nome);
        aceite.GetProperty("resumo").GetString().Should().Be(Vigente.Resumo);
        Registro.Registrados.Skip(antes).Should().Contain(r => r.AtorContaId == Cecilia.ContaId.ToString()
            && r.Permissao == "autorizacao:pedidos:consultar" && r.ResourceId == id && r.Resultado == ResultadoDoAcesso.Permitido);
    }

    // Cecília só com o perfil de Gestor de proteção de dados, recém-concedido e vigente.
    private async Task<HttpClient> GestorDeProtecaoDeDadosAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Cecilia.ContaId.ToString());
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Cecilia.ContaId.ToString(), TipoDeGovernanca.GestorDeProtecaoDeDados, Cecilia.Email);
        return Cliente(Cecilia);
    }
}
