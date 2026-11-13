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
/// O Encarregado de dados lê o aceite do termo pelo detalhe do pedido, com a permissão de consultar os pedidos, e a
/// leitura é registrada como as demais consultas dele: o aceite traz o nome do beneficiário, dado pessoal. Cecília Moura
/// é o Encarregado de dados.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class AceiteDoTermoNaConsultaDoEncarregadoNoHostTests : TermoNoHostTestBase
{
    private static readonly Persona Cecilia = new("cecilia.moura", "Cecília Ester Aurora Moura", "cecilia_ester_moura@uniplus-test.local");

    public AceiteDoTermoNaConsultaDoEncarregadoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    private RegistroDeDecisoesDeTeste Registro => Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();

    [Fact(DisplayName = "O Encarregado lê o aceite do termo pelo detalhe do pedido, e a leitura é registrada com a permissão de consulta")]
    public async Task Encarregado_LeOAceite_ELeituraRegistrada()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        Guid id = (await CorpoAsync(await PedirAsync(betina, perfil, unidade, Vigente!.Id), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using HttpClient cecilia = await EncarregadoDeDadosAsync();
        int antes = Registro.Registrados.Count;

        JsonElement aceite = (await TermoDoPedidoAsync(cecilia, id)).GetProperty("aceite");

        aceite.GetProperty("pessoa").GetProperty("nome").GetString().Should().Be(Betina.Nome);
        aceite.GetProperty("resumo").GetString().Should().Be(Vigente.Resumo);
        Registro.Registrados.Skip(antes).Should().Contain(r => r.AtorSubject == Cecilia.Sub.ToString()
            && r.Permissao == "autorizacao:pedidos:consultar" && r.ResourceId == id && r.Resultado == ResultadoDoAcesso.Permitido);
    }

    // Cecília só com o perfil de Encarregado de dados, recém-concedido e vigente.
    private async Task<HttpClient> EncarregadoDeDadosAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Cecilia.Sub.ToString());
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Cecilia.Sub.ToString(), TipoDeGovernanca.EncarregadoDeDados, Cecilia.Email);
        return Cliente(Cecilia);
    }
}
