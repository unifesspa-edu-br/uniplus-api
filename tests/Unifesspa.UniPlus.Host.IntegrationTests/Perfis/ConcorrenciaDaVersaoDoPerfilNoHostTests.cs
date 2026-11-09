namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine;
using Wolverine.Tracking;

/// <summary>
/// As operações sobre o perfil seguem a mesma ordem de travas, então duas transações concorrentes se resolvem pela que
/// trava primeiro. Cada teste segura uma transação aberta, espera o banco registrar a outra bloqueada e só então
/// confirma, de modo que a ordem é a decidida pelo teste e não pela sorte do agendador.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ConcorrenciaDaVersaoDoPerfilNoHostTests : VersoesDoPerfilNoHostTestBase
{
    public ConcorrenciaDaVersaoDoPerfilNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A concessão que vence a trava faz a entrada em vigor da versão que amplia encontrá-la e recusar a ampliação")]
    public async Task ConcessaoVenceATrava_EntradaEmVigorRecusaAAmpliacao()
    {
        (Envio original, Envio ampliada) = await PerfilComVersaoAmpliadaEmVerificacaoAsync();
        Guid unidade = await SemearUnidadeAsync();
        await using TransacaoRetidaDeTeste concessao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await ConcederAsync(concessao.Servicos, Leandro, original.PerfilId, EscopoDaConcessao.DaUnidade(unidade));

        Task<DesfechoDoResultadoDaVersao> entrada = RegistrarEmTransacaoAsync(ampliada.DocumentoId);
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        entrada.IsCompleted.Should().BeFalse();
        await concessao.ConfirmarAsync();

        (await entrada).Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        (await ConcessoesAsync(Leandro, original.PerfilId)).Should().ContainSingle();
        (await SituacaoDaVersaoNoBancoAsync(ampliada.VersaoId)).Should().Be("recusada");
        (await SituacaoNoBancoAsync(ampliada.DocumentoId)).Should().Be("verificado");
        (await VigenciasAsync(original.PerfilId)).Should().ContainSingle(v => v.Aberta && v.VersaoId == original.VersaoId);
        (await ConsultarPerfilAsync(original.PerfilId)).Should().Be(("ampliacao_exige_protecao_de_dados", 1));
    }

    [Fact(DisplayName = "A entrada em vigor que vence a trava faz a concessão ser avaliada contra a composição nova")]
    public async Task EntradaEmVigorVenceATrava_ConcessaoLeAComposicaoNova()
    {
        (Envio original, Envio ampliada) = await PerfilComVersaoAmpliadaEmVerificacaoAsync();
        Guid unidade = await SemearUnidadeAsync();
        await using TransacaoRetidaDeTeste entrada = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        (await entrada.Servicos.GetRequiredService<ResultadoDoDocumentoDaVersao>()
            .RegistrarAsync(ResultadoDe(ampliada.DocumentoId, VeredictoDaVerificacao.Verificado), CancellationToken.None))
            .Should().Be(DesfechoDoResultadoDaVersao.Aplicado);

        Task<Perfil> concessao = ConcederEmTransacaoAsync(original.PerfilId, EscopoDaConcessao.DaUnidade(unidade));
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        concessao.IsCompleted.Should().BeFalse();
        await entrada.ConfirmarAsync();

        (await concessao).ComposicaoEmVigor.Should().BeEquivalentTo(Consultar, Sensivel);
        (await ConcessoesAsync(Leandro, original.PerfilId)).Should().ContainSingle();
        (await SituacaoDaVersaoNoBancoAsync(ampliada.VersaoId)).Should().Be("vigente");
        (await ConsultarPerfilAsync(original.PerfilId)).Should().Be((null, 2));
    }

    [Fact(DisplayName = "Nome tomado entre a conferência e a gravação desfaz a transação, e a reentrega pela fila recusa por nome indisponível")]
    public async Task NomeTomadoNoCommit_DesfazTudoEAFilaReentregaRecusando()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, original);
        string nomeNovo = Nome();
        Envio nova = await CriarVersaoAsync(gestor, original.PerfilId, nomeNovo, Consultar);
        await ConfirmarEnvioAsync(gestor, nova);
        await using TransacaoRetidaDeTeste outra = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await outra.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "INSERT INTO autorizacao.perfis (id, nome, descricao, ativo, created_at) VALUES ({0}, {1}, 'Perfil que toma o nome.', true, now())",
            Guid.CreateVersion7(), nomeNovo);

        Task<ITrackedSession> fluxo = EntregarPelaFilaAsync(nova.DocumentoId);
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services);
        await using TransacaoRetidaDeTeste janela = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        Task travar = janela.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", original.PerfilId);
        await TransacaoRetidaDeTeste.AguardarBloqueioAsync(Fixture.Factory.Services, 2);
        await outra.ConfirmarAsync();
        await travar;

        (await SituacaoNoBancoAsync(nova.DocumentoId)).Should().Be("em_verificacao");
        (await SituacaoDaVersaoNoBancoAsync(nova.VersaoId)).Should().Be("pendente");
        (await VigenciasAsync(original.PerfilId)).Should().ContainSingle(v => v.Aberta && v.VersaoId == original.VersaoId);
        await janela.ConfirmarAsync();

        ITrackedSession sessao = await fluxo;
        sessao.AllRecordsInOrder().Count(r => r.MessageEventType == MessageEventType.ExecutionStarted).Should().Be(2);
        (await ConsultarPerfilAsync(original.PerfilId)).Should().Be(("nome_indisponivel", 1));
        (await SituacaoNoBancoAsync(nova.DocumentoId)).Should().Be("verificado");
        (await SituacaoDaVersaoNoBancoAsync(nova.VersaoId)).Should().Be("recusada");
        (await VigenciasAsync(original.PerfilId)).Should().ContainSingle(v => v.Aberta && v.VersaoId == original.VersaoId);
    }

    private Task<ITrackedSession> EntregarPelaFilaAsync(Guid documentoId) =>
        Fixture.Factory.Services.GetRequiredService<IHost>()
            .TrackActivity()
            .IncludeExternalTransports()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync(bus => bus
                .EndpointFor(FilasDaVerificacao.ResultadosAutorizacao)
                .SendAsync(ResultadoDe(documentoId, VeredictoDaVerificacao.Verificado)));

    private async Task<(Envio Original, Envio Ampliada)> PerfilComVersaoAmpliadaEmVerificacaoAsync()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, original);
        Envio ampliada = await CriarVersaoAsync(gestor, original.PerfilId, Nome(), Consultar, Sensivel);
        await ConfirmarEnvioAsync(gestor, ampliada);

        return (original, ampliada);
    }

    private async Task<DesfechoDoResultadoDaVersao> RegistrarEmTransacaoAsync(Guid documentoId)
    {
        await using TransacaoRetidaDeTeste transacao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        DesfechoDoResultadoDaVersao desfecho = await transacao.Servicos.GetRequiredService<ResultadoDoDocumentoDaVersao>()
            .RegistrarAsync(ResultadoDe(documentoId, VeredictoDaVerificacao.Verificado), CancellationToken.None);
        await transacao.ConfirmarAsync();

        return desfecho;
    }

    private async Task<Perfil> ConcederEmTransacaoAsync(Guid perfilId, EscopoDaConcessao escopo)
    {
        await using TransacaoRetidaDeTeste transacao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        Perfil perfil = await ConcederAsync(transacao.Servicos, Leandro, perfilId, escopo);
        await transacao.ConfirmarAsync();

        return perfil;
    }

    private async Task<(string? Recusa, int Numero)> ConsultarPerfilAsync(Guid perfilId)
    {
        using HttpClient gestor = await GestorAsync();
        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{perfilId}");

        return (await RecusaDaUltimaVersaoAsync(gestor, perfilId), perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32());
    }
}
