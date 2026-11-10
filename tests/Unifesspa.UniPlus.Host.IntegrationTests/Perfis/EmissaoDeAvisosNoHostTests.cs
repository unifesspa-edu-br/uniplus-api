namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using Wolverine.EntityFrameworkCore;

/// <summary>
/// A emissão dos avisos contra o banco real: o registro grava na transação de quem emite, a corrida pela mesma chave
/// deixa um registro só, e duas emissões com chaves sobrepostas em ordens inversas não entram em deadlock.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class EmissaoDeAvisosNoHostTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";

    private static readonly DestinatarioDoAviso Leandro = Destinatario("leandro.viana", "leandro-viana84@uniplus-test.local");
    private static readonly DestinatarioDoAviso Betina = Destinatario("betina.neves", "betina-dasneves81@uniplus-test.local");
    private static readonly DestinatarioDoAviso Isabella = Destinatario("isabella.figueiredo", "isabellapriscilafigueiredo@uniplus-test.local");

    private readonly MonolitoDaInstalacaoFixture _fixture;

    public EmissaoDeAvisosNoHostTests(MonolitoDaInstalacaoFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Duas transações que emitem o mesmo aviso deixam um registro e um envelope no outbox, e a perdedora conclui sem mensagem nem erro")]
    public async Task Corrida_MesmaChave_UmRegistroEUmEnvelope()
    {
        Guid fato = Guid.CreateVersion7();
        int envelopesAntes = await EnvelopesDeEmailAsync();
        await using TransacaoRetidaDeTeste primeira = await TransacaoRetidaDeTeste.AbrirAsync(_fixture.Factory.Services);
        await using TransacaoRetidaDeTeste segunda = await TransacaoRetidaDeTeste.AbrirAsync(_fixture.Factory.Services);

        IReadOnlyList<EnviarEmail> daPrimeira = await EmitirNoOutboxAsync(primeira, fato, [Leandro]);
        Task<IReadOnlyList<EnviarEmail>> daSegunda = EmitirNoOutboxAsync(segunda, fato, [Leandro]);
        await primeira.AguardarBloqueioAsync();
        await primeira.ConfirmarAsync();
        IReadOnlyList<EnviarEmail> perdedora = await daSegunda;
        await segunda.ConfirmarAsync();

        daPrimeira.Should().ContainSingle();
        perdedora.Should().BeEmpty();
        (await RegistrosAsync(fato)).Should().Be(1);
        (await EnvelopesDeEmailAsync()).Should().Be(envelopesAntes + 1, "só a vencedora deixou envelope");
        await primeira.Servicos.GetRequiredService<IDbContextOutbox<AutorizacaoDbContext>>().FlushOutgoingMessagesAsync();
        await PedidosDeConcessaoTestBase.AguardarAsync(async () => await EnvelopesDeEmailAsync() <= envelopesAntes, "o envelope não foi entregue");
    }

    [Fact(DisplayName = "Duas emissões com chaves sobrepostas em ordens inversas concluem sem deadlock, e cada chave sai uma vez")]
    public async Task Corrida_ChavesSobrepostasEmOrdensInversas_SemDeadlock()
    {
        Guid fato = Guid.CreateVersion7();
        await using TransacaoRetidaDeTeste primeira = await TransacaoRetidaDeTeste.AbrirAsync(_fixture.Factory.Services);
        await using TransacaoRetidaDeTeste segunda = await TransacaoRetidaDeTeste.AbrirAsync(_fixture.Factory.Services);

        IReadOnlyList<EnviarEmail> daPrimeira = await EmitirAsync(primeira, fato, [Leandro, Betina, Isabella]);
        Task<IReadOnlyList<EnviarEmail>> daSegunda = EmitirAsync(segunda, fato, [Isabella, Betina, Leandro]);
        await primeira.AguardarBloqueioAsync();
        await primeira.ConfirmarAsync();
        IReadOnlyList<EnviarEmail> perdedora = await daSegunda;
        await segunda.ConfirmarAsync();

        daPrimeira.Should().HaveCount(3);
        perdedora.Should().BeEmpty();
        (await RegistrosAsync(fato)).Should().Be(3);
    }

    [Fact(DisplayName = "A transação que não confirma leva junto o registro do aviso, e a emissão seguinte volta a enviá-lo")]
    public async Task SemConfirmar_NaoDeixaRegistro()
    {
        Guid fato = Guid.CreateVersion7();
        await using (TransacaoRetidaDeTeste descartada = await TransacaoRetidaDeTeste.AbrirAsync(_fixture.Factory.Services))
        {
            (await EmitirAsync(descartada, fato, [Leandro])).Should().ContainSingle();
        }

        await using TransacaoRetidaDeTeste nova = await TransacaoRetidaDeTeste.AbrirAsync(_fixture.Factory.Services);
        IReadOnlyList<EnviarEmail> reenviado = await EmitirAsync(nova, fato, [Leandro]);
        await nova.ConfirmarAsync();

        reenviado.Should().ContainSingle();
        (await RegistrosAsync(fato)).Should().Be(1);
    }

    [Fact(DisplayName = "A mesma pessoa com o e-mail trocado entre duas gerações do mesmo fato recebe um aviso só")]
    public async Task EmailTrocado_UmAvisoSo()
    {
        Guid fato = Guid.CreateVersion7();
        DestinatarioDoAviso comOutroEmail = new(Leandro.Pessoa, "leandro.novo@uniplus-test.local");
        await using TransacaoRetidaDeTeste primeira = await TransacaoRetidaDeTeste.AbrirAsync(_fixture.Factory.Services);
        IReadOnlyList<EnviarEmail> geracaoUm = await EmitirAsync(primeira, fato, [Leandro]);
        await primeira.ConfirmarAsync();
        await using TransacaoRetidaDeTeste segunda = await TransacaoRetidaDeTeste.AbrirAsync(_fixture.Factory.Services);

        IReadOnlyList<EnviarEmail> geracaoDois = await EmitirAsync(segunda, fato, [comOutroEmail]);

        geracaoUm.Should().ContainSingle();
        geracaoDois.Should().BeEmpty();
    }

    [Fact(DisplayName = "O identificador do aviso aguenta separadores e Unicode na identidade sem colidir com outra pessoa")]
    public async Task IdentidadeComSeparadoresEUnicode_NaoColide()
    {
        Guid fato = Guid.CreateVersion7();
        DestinatarioDoAviso comBarra = Destinatario("ana|beatriz", "ana-beatriz@uniplus-test.local");
        DestinatarioDoAviso parecida = Destinatario("ana", "beatriz-ana@uniplus-test.local");
        DestinatarioDoAviso acentuada = Destinatario("josé.conceição", "jose-conceicao@uniplus-test.local");
        await using TransacaoRetidaDeTeste transacao = await TransacaoRetidaDeTeste.AbrirAsync(_fixture.Factory.Services);

        IReadOnlyList<EnviarEmail> enviados = await EmitirAsync(transacao, fato, [comBarra, parecida, acentuada]);
        await transacao.ConfirmarAsync();

        enviados.Should().HaveCount(3);
        (await RegistrosAsync(fato)).Should().Be(3);
    }

    private static DestinatarioDoAviso Destinatario(string sub, string email) => new(new Identidade(Emissor, sub), email);

    private static Task<IReadOnlyList<EnviarEmail>> EmitirAsync(
        TransacaoRetidaDeTeste transacao, Guid fato, IReadOnlyList<DestinatarioDoAviso> destinatarios)
    {
        ModeloDeAviso modelo = new(TipoDeAviso.VersaoEncerrada, "Acompanhamento", 1, [], "https://uniplus.test/configuracao/perfis/1", Forma: FormaDoAviso.VersaoSubstituida);

        return transacao.Servicos.GetRequiredService<EmissorDeAvisos>()
            .EmitirAsync(new PedidoDeAviso(modelo, fato, string.Empty, destinatarios, []), CancellationToken.None);
    }

    private static async Task<IReadOnlyList<EnviarEmail>> EmitirNoOutboxAsync(
        TransacaoRetidaDeTeste transacao, Guid fato, IReadOnlyList<DestinatarioDoAviso> destinatarios)
    {
        IReadOnlyList<EnviarEmail> emitidos = await EmitirAsync(transacao, fato, destinatarios);
        IDbContextOutbox<AutorizacaoDbContext> outbox = transacao.Servicos.GetRequiredService<IDbContextOutbox<AutorizacaoDbContext>>();
        foreach (EnviarEmail email in emitidos)
        {
            await outbox.PublishAsync(email);
        }

        return emitidos;
    }

    private async Task<int> EnvelopesDeEmailAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM wolverine.wolverine_outgoing_envelopes WHERE message_type ILIKE '%email%'")
            .SingleAsync();
    }

    private async Task<int> RegistrosAsync(Guid fato)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.avisos_emitidos WHERE fato_id = {fato}")
            .SingleAsync();
    }
}
