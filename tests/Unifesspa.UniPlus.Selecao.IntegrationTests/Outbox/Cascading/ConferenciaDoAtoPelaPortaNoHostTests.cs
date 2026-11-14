namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.RegistroPelaPortaDeTeste;

/// <summary>
/// O que a porta de escrita do registro de atos confere e grava, contra o host real: os atributos do tipo como o domínio
/// os conferiu, os anexos na ordem em que o ato os apresenta e a classe de congelamento da cadeia de retificação.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ConferenciaDoAtoPelaPortaNoHostTests
{
    private readonly CascadingFixture _fixture;

    public ConferenciaDoAtoPelaPortaNoHostTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "a porta recusa a retificação cuja classe de congelamento diverge da do ato retificado, e nada é gravado")]
    public async Task Retificacao_ComClasseDeCongelamentoDivergente_EhRecusadaPelaPorta()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        await TiposDeAtoSeeder.SemearTipoAsync(api.Services, "AVISO_NAO_CONGELANTE", unicoPorObjeto: false, congela: false);
        (Guid processoId, Guid abertura) = await PublicarAberturaAsync(api, nameof(Retificacao_ComClasseDeCongelamentoDivergente_EhRecusadaPelaPorta));
        AtoNormativoPublicado emenda = EmendaNaoCongelante(processoId, abertura);

        Result resultado = await RegistrarSemGravarAsync(api, emenda);

        resultado.Error!.Code.Should().Be("AtoNormativo.ClasseDeCongelamentoDivergente");
        (await AtosComOIdAsync(api, emenda.AtoId)).Should().Be(0);
    }

    [Fact(DisplayName = "o ato é gravado com os atributos conferidos pelo domínio, não com os do catálogo no momento do registro")]
    public async Task AtributosConferidos_VencemOCatalogo()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearTipoAsync(api.Services, "TIPO_MUDOU_NO_CATALOGO", unicoPorObjeto: false, congela: false);
        Guid processoId = (await SemearProcessoAsync(api, nameof(AtributosConferidos_VencemOCatalogo))).ProcessoId;
        AtoNormativoPublicado ato = AtoDoProcesso(processoId, "TIPO_MUDOU_NO_CATALOGO", "777/2026", new AtributosDoTipoAto(true, false, false, "Nome conferido"));

        await RegistrarEGravarAsync(api, ato);

        AtoNormativo gravado = await AtoNoRegistroCentral.LerAsync(api, ato.AtoId);
        gravado.CongelaConfiguracao.Should().BeTrue("o cadastro editado depois não reescreve o passado do ato");
        gravado.TipoNome.Should().Be("Nome conferido");
    }

    [Fact(DisplayName = "os anexos são gravados na ordem em que o ato os apresenta")]
    public async Task Anexos_SaoGravadosNaOrdemApresentada()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearTipoAsync(api.Services, "TIPO_COM_ANEXOS", unicoPorObjeto: false, congela: false);
        Guid processoId = (await SemearProcessoAsync(api, nameof(Anexos_SaoGravadosNaOrdemApresentada))).ProcessoId;
        AtoNormativoPublicado ato = ComAnexosEmOrdem(AtoDoProcesso(processoId, "TIPO_COM_ANEXOS", "888/2026", new AtributosDoTipoAto(false, false, false, null)));

        await RegistrarEGravarAsync(api, ato);

        (await NomesDosAnexosAsync(api, ato.AtoId)).Should().Equal("Anexo II.pdf", "Anexo I.pdf");
    }

    private static AtoNormativoPublicado EmendaNaoCongelante(Guid processoId, Guid abertura) =>
        AtoDoProcesso(processoId, "AVISO_NAO_CONGELANTE", "010/2026", new AtributosDoTipoAto(false, false, false, null)) with
        {
            Retificacao = new RetificacaoDoAto(abertura, "Tentativa de emendar um ato congelante com um não congelante"),
        };

    private static AtoNormativoPublicado ComAnexosEmOrdem(AtoNormativoPublicado ato) =>
        ato with
        {
            Anexos =
            [
                new AnexoDoAtoPublicado("Anexo II.pdf", "application/pdf", new string('b', 64)),
                new AnexoDoAtoPublicado("Anexo I.pdf", "application/pdf", new string('a', 64)),
            ],
        };

    private static async Task<(Guid ProcessoId, Guid AtoId)> PublicarAberturaAsync(CascadingApiFactory api, string nome)
    {
        using HttpClient client = api.CreateClient();
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nome);
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        return (processoId, await ObterAtoIdAsync(api, processoId));
    }

    private static async Task<int> AtosComOIdAsync(CascadingApiFactory api, Guid atoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Set<AtoNormativo>().AsNoTracking().CountAsync(a => a.Id == atoId);
    }

    private static async Task<IReadOnlyList<string>> NomesDosAnexosAsync(CascadingApiFactory api, Guid atoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        AtoNormativo gravado = await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .Set<AtoNormativo>().AsNoTracking().Include(a => a.Anexos).SingleAsync(a => a.Id == atoId);

        return [.. gravado.Anexos.OrderBy(a => a.Ordem).Select(a => a.Nome)];
    }
}
