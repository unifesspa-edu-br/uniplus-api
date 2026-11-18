namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;


using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Wolverine.EntityFrameworkCore;

/// <summary>
/// A emissão dos avisos contra o banco real: o registro grava na transação de quem emite, a corrida pela mesma chave
/// deixa um registro só, e duas emissões com chaves sobrepostas em ordens inversas não entram em deadlock.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
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

    [Fact(DisplayName = "Duas transações deixam um registro e um envelope, entregue após a confirmação")]
    public async Task Corrida_MesmaChave_EntregaEnvelope()
    {
        await using CenarioDeEmissaoConcorrente cenario = new();
        await cenario.ExecutarAsync(this);

        cenario.Vencedora.Should().ContainSingle();
        cenario.Perdedora.Should().BeEmpty();
        (await RegistrosAsync(cenario.Fato)).Should().Be(1);
        (await EnvelopesDeEmailAsync()).Should().Be(cenario.EnvelopesAntes + 1, "só a vencedora deixou envelope");
        await cenario.Primeira.Servicos.GetRequiredService<IDbContextOutbox<UniPlusDbContext>>().FlushOutgoingMessagesAsync();
        await PedidosDeConcessaoTestBase.AguardarAsync(
            async () => await EnvelopesDeEmailAsync() <= cenario.EnvelopesAntes, "o envelope não foi entregue");
    }

    private sealed class CenarioDeEmissaoConcorrente : IAsyncDisposable
    {
        public Guid Fato { get; } = Guid.CreateVersion7();
        public int EnvelopesAntes { get; private set; }
        public TransacaoRetidaDeTeste Primeira { get; private set; } = null!;
        public TransacaoRetidaDeTeste Segunda { get; private set; } = null!;
        public IReadOnlyList<EnviarEmail> Vencedora { get; private set; } = [];
        public IReadOnlyList<EnviarEmail> Perdedora { get; private set; } = [];

        public async Task ExecutarAsync(EmissaoDeAvisosNoHostTests teste)
        {
            EnvelopesAntes = await teste.EnvelopesDeEmailAsync();
            Primeira = await TransacaoRetidaDeTeste.AbrirAsync(teste._fixture.Factory.Services);
            Segunda = await TransacaoRetidaDeTeste.AbrirAsync(teste._fixture.Factory.Services);
            Vencedora = await EmitirNoOutboxAsync(Primeira, Fato, [Leandro]);
            Task<IReadOnlyList<EnviarEmail>> segunda = EmitirNoOutboxAsync(Segunda, Fato, [Leandro]);
            await Primeira.AguardarBloqueioAsync();
            await Primeira.ConfirmarAsync();
            Perdedora = await segunda;
            await Segunda.ConfirmarAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Segunda is not null)
                {
                    await Segunda.DisposeAsync();
                }
            }
            finally
            {
                if (Primeira is not null)
                {
                    await Primeira.DisposeAsync();
                }
            }
        }
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

    private static DestinatarioDoAviso Destinatario(string contaId, string email) => new(new Identidade(Emissor, contaId), email);

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
        IDbContextOutbox<UniPlusDbContext> outbox = transacao.Servicos.GetRequiredService<IDbContextOutbox<UniPlusDbContext>>();
        foreach (EnviarEmail email in emitidos)
        {
            await outbox.PublishAsync(email);
        }

        return emitidos;
    }

    private async Task<int> EnvelopesDeEmailAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM wolverine.wolverine_outgoing_envelopes WHERE message_type ILIKE '%email%'")
            .SingleAsync();
    }

    private async Task<int> RegistrosAsync(Guid fato)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.avisos_emitidos WHERE fato_id = {fato}")
            .SingleAsync();
    }
}
