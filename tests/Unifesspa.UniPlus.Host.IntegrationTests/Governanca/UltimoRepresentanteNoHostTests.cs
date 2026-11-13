namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A revogação direta não deixa a instituição sem Representante do controlador vigente (UNI-REQ-0394): a da única
/// designação é recusada, a troca se faz designando antes de revogar, e duas revogações simultâneas dos dois últimos
/// Representantes se serializam pela trava do papel, de modo que uma passa e a outra é recusada. A designação anterior à
/// regra, com prazo ainda não vencido, conta como vigente; quando ela vence, a dispensa da primeira designação recupera.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class UltimoRepresentanteNoHostTests : UltimoRepresentanteNoHostTestBase
{
    public UltimoRepresentanteNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A revogação da única designação vigente de Representante é recusada, e a designação segue vigente")]
    public async Task Revogar_UnicaDesignacao_Recusa()
    {
        Governanca g = await GovernancaAsync();

        await ConferirCodigoAsync(
            await RevogarAsync(g.Diogo, await DesignacaoAsync(Marlene), Motivo, Marlene.Email), HttpStatusCode.UnprocessableEntity, UltimoRepresentante);

        (await VigentesAsync()).Should().Equal(Marlene.Sub.ToString());
    }

    [Fact(DisplayName = "Com outra pessoa designada, a revogação é aceita e a outra designação segue vigente")]
    public async Task Revogar_ComOutraDesignada_Aceita()
    {
        Governanca g = await GovernancaAsync();
        await DesignacaoAsync(Leandro);

        (await RevogarAsync(g.Diogo, await DesignacaoAsync(Marlene), Motivo, Marlene.Email)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await VigentesAsync()).Should().Equal(Leandro.Sub.ToString());
    }

    // As duas revogações correm de fato sobrepostas: a transação do teste segura a trava do papel até o banco registrar
    // as duas em espera. Sem a conferência sob essa trava, as duas passariam e a instituição ficaria sem Representante.
    [Fact(DisplayName = "Duas revogações simultâneas dos dois últimos Representantes: uma passa, a outra é recusada, e sobra exatamente um")]
    public async Task RevogacoesSimultaneas_SobraExatamenteUm()
    {
        Governanca g = await GovernancaAsync();
        Guid marlene = await DesignacaoAsync(Marlene);
        Guid leandro = await DesignacaoAsync(Leandro);

        HttpResponseMessage[] respostas = await EmFilaSobATravaDaLinhaAsync(
            TravaDoPapel, Representante, () => RevogarAsync(g.Diogo, marlene, Motivo, Marlene.Email), () => RevogarAsync(g.Isabella, leandro, Motivo, Leandro.Email));

        (await VigentesAsync()).Should().ContainSingle("a instituição não pode ficar sem Representante vigente");
        await ConferirUmaAceitaEOutraRecusadaAsync(respostas, HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "A troca designa primeiro: Marlene decide a segunda etapa da designação de Leandro, e só então a dela é revogada")]
    public async Task Troca_DesignaAntesDeRevogar()
    {
        Governanca g = await GovernancaAsync();
        Guid pedido = await DesignacaoAceitaAsync(Leandro);

        (await EnviarAsync(g.Diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SituacaoAsync(g.Diogo, pedido)).Should().Be("aguardando_protecao_de_dados");
        (await EnviarAsync(g.Marlene, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await RevogarAsync(g.Diogo, await DesignacaoAsync(Marlene), Motivo, Marlene.Email)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await VigentesAsync()).Should().Equal(Leandro.Sub.ToString());
    }

    [Fact(DisplayName = "A designação anterior à regra conta enquanto vigente; vencida, a instituição fica sem Representante e a dispensa recupera")]
    public async Task DesignacaoAnteriorComPrazo_ContaEnquantoVigente()
    {
        Governanca g = await GovernancaAsync();
        Guid leandro = await EstadoAnteriorAoRepresentanteSemPrazo.DesignarComPrazoAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), DateTimeOffset.UtcNow.AddDays(10), Leandro.Email);

        (await RevogarAsync(g.Diogo, await DesignacaoAsync(Marlene), Motivo, Marlene.Email)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await EstadoAnteriorAoRepresentanteSemPrazo.GravarPrazoNaDesignacaoAsync(Fixture.Factory.Services, leandro, DateTimeOffset.UtcNow.AddMinutes(-1));
        (await VigentesAsync()).Should().BeEmpty();

        Guid pedido = await DesignacaoAceitaAsync(Isabella);
        (await EnviarAsync(g.Diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SituacaoAsync(g.Diogo, pedido)).Should().Be("aprovado");
        (await VigentesAsync()).Should().Equal(Isabella.Sub.ToString());
    }
}
