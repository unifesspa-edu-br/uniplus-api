namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCenarioDeIndicadores;

internal sealed class InscricoesDoCenarioDeIndicadores
{
    private readonly CascadingFixture _ambiente;
    private readonly ApoioDoCenarioDeIndicadores _apoio;

    public InscricoesDoCenarioDeIndicadores(CascadingFixture cenario, ApoioDoCenarioDeIndicadores apoio)
    {
        _ambiente = cenario;
        _apoio = apoio;
    }

    private static readonly DateTimeOffset InstanteDoCancelamento = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    public async Task SemearInscricoesAsync(Guid processoId)
    {
        // O mesmo sub em emissores distintos e CPFs distintos diferencia a identidade canônica por CPF.
        await SemearInscricaoAsync(processoId, PersonasDeTeste.FernandaDrumond, "candidato.antiga", SituacaoInscricao.Cancelada);
        await SemearInscricaoAsync(processoId, PersonasDeTeste.FernandaDrumond, "same-sub", SituacaoInscricao.Submetida);
        await SemearInscricaoAsync(
            processoId, PersonasDeTeste.KevinCarvalho, "same-sub", SituacaoInscricao.Submetida,
            "https://outro-emissor.exemplo/realms/outro");
        await SemearInscricaoAsync(processoId, PersonasDeTeste.ErickIsaacRamos, "candidato.rascunho.ativo", SituacaoInscricao.Rascunho);
        await SemearInscricaoAsync(
            processoId, Cpf.Criar(PersonasDeTeste.Candidatas[2].Cpf).Value!, "candidato.rascunho", SituacaoInscricao.Rascunho);
    }

    public async Task DescartarRascunhoPelaApiAsync(Guid processoId)
    {
        const string sub = "indicadores.rascunho.descartado.pela.api";
        Cpf cpf = Cpf.Criar(PersonasDeTeste.Candidatas[4].Cpf).Value!;
        string rota = $"{Rota}/{processoId}/inscricao";
        using HttpResponseMessage iniciado = await _apoio.EnviarAsync(
            sub, rota, "application/vnd.uniplus.inscricao.v1+json", new(Cpf: cpf.Valor, Metodo: HttpMethod.Post));
        iniciado.StatusCode.Should().Be(HttpStatusCode.OK, await iniciado.Content.ReadAsStringAsync());
        string etag = await LerETagDaInscricaoAsync(sub, rota, cpf);
        using HttpResponseMessage descartado = await _apoio.EnviarAsync(
            sub, rota, "application/vnd.uniplus.inscricao.v1+json", new(Cpf: cpf.Valor, Metodo: HttpMethod.Delete, IfMatch: etag));
        descartado.StatusCode.Should().Be(HttpStatusCode.NoContent, await descartado.Content.ReadAsStringAsync());
    }

    private async Task<string> LerETagDaInscricaoAsync(string sub, string rota, Cpf cpf)
    {
        using HttpResponseMessage leitura = await _apoio.EnviarAsync(
            sub, rota, "application/vnd.uniplus.inscricao.v1+json", new(Cpf: cpf.Valor));
        leitura.StatusCode.Should().Be(HttpStatusCode.OK, await leitura.Content.ReadAsStringAsync());
        using JsonDocument inscricao = JsonDocument.Parse(await leitura.Content.ReadAsStringAsync());
        return inscricao.RootElement.GetProperty("eTag").GetString()!;
    }

    public async Task SemearInscricaoAsync(
        Guid processoId, Cpf cpf, string sub, SituacaoInscricao situacao, string emissor = TestAuthHandler.DefaultIssuer)
    {
        Inscricao inscricao = CenarioDaInscricaoGravada.NovaInscricao(cpf, processoId, sub, emissor, Guid.CreateVersion7());
        await using AsyncServiceScope escopo = _ambiente.Factory.Services.CreateAsyncScope();
        IInscricaoRepository inscricoes = escopo.ServiceProvider.GetRequiredService<IInscricaoRepository>();
        IComprovanteDaInscricaoRepository comprovantes = escopo.ServiceProvider.GetRequiredService<IComprovanteDaInscricaoRepository>();
        if (situacao != SituacaoInscricao.Rascunho)
            await SubmeterInscricaoAsync(inscricao, comprovantes, situacao, sub);
        await inscricoes.AdicionarAsync(inscricao);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync();
    }

    private static async Task SubmeterInscricaoAsync(
        Inscricao inscricao, IComprovanteDaInscricaoRepository comprovantes, SituacaoInscricao situacao, string sub)
    {
        string numero = Random.Shared.NextInt64(1000000000000000, 9999999999999999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Result<ComprovanteInscricao> envio = CenarioDaInscricaoGravada.Enviar(inscricao, numero);
        envio.IsSuccess.Should().BeTrue();
        await comprovantes.AdicionarComprovanteAsync(envio.Value!);
        if (situacao == SituacaoInscricao.Cancelada)
            inscricao.Cancelar("a pedido do candidato", sub, InstanteDoCancelamento, CenarioDaInscricaoGravada.PrecondicaoDe(inscricao))
                .IsSuccess.Should().BeTrue();
        else
            await RegistrarCorrecaoAsync(inscricao, comprovantes, numero);
    }

    private static async Task RegistrarCorrecaoAsync(
        Inscricao inscricao, IComprovanteDaInscricaoRepository comprovantes, string numero)
    {
        Result<ComprovanteInscricao> correcao = CenarioDaInscricaoGravada.Enviar(inscricao, numero);
        correcao.IsSuccess.Should().BeTrue();
        await comprovantes.AdicionarComprovanteAsync(correcao.Value!);
    }
}
