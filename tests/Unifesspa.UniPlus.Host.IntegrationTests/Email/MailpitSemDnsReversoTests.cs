namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using System.Diagnostics;
using System.Net.Sockets;

using AwesomeAssertions;

using DotNet.Testcontainers.Containers;

/// <summary>
/// O Mailpit dos testes saúda a conexão SMTP sem consultar o DNS. Com o DNS do contêiner apontado para um servidor que
/// nunca responde, a consulta reversa fica pendente até o prazo do resolvedor (cerca de 5 s): desligada, a saudação
/// chega antes disso; ligada, a mesma conexão espera o prazo, e é essa espera que parava a fila de envio de e-mail.
/// </summary>
public sealed class MailpitSemDnsReversoTests
{
    private static readonly TimeSpan PrazoDaSaudacao = TimeSpan.FromSeconds(15);

    [Fact(DisplayName = "Sem DNS reverso, a saudação SMTP do Mailpit dos testes chega antes do prazo do resolvedor")]
    public async Task SemDnsReverso_SaudaAntesDoPrazoDoResolvedor()
    {
        TimeSpan? espera = await EsperaPelaSaudacaoAsync(comDnsReverso: false);

        espera.Should().NotBeNull().And.BeLessThan(TimeSpan.FromSeconds(3), "a saudação não pode depender do DNS");
    }

    [Fact(DisplayName = "Com DNS reverso, o mesmo Mailpit segura a saudação até o prazo do resolvedor")]
    public async Task ComDnsReverso_SeguraASaudacao()
    {
        TimeSpan? espera = await EsperaPelaSaudacaoAsync(comDnsReverso: true);

        (espera is null || espera > TimeSpan.FromSeconds(4)).Should().BeTrue($"a consulta pendente atrasa a saudação, que veio em {espera}");
    }

    // Nulo quando a saudação não vem dentro do prazo; os dois contêineres são liberados em qualquer caso.
    private static async Task<TimeSpan?> EsperaPelaSaudacaoAsync(bool comDnsReverso)
    {
        IContainer dns = MailpitDeTeste.CriarDnsMudo();
        await using (dns.ConfigureAwait(false))
        {
            await dns.StartAsync();
            IContainer mailpit = MailpitDeTeste.Criar(dns.IpAddress, comDnsReverso);
            await using (mailpit.ConfigureAwait(false))
            {
                await mailpit.StartAsync();
                return await CronometrarSaudacaoAsync(mailpit);
            }
        }
    }

    private static async Task<TimeSpan?> CronometrarSaudacaoAsync(IContainer mailpit)
    {
        using CancellationTokenSource prazo = new(PrazoDaSaudacao);
        using TcpClient cliente = new();
        Stopwatch relogio = Stopwatch.StartNew();
        try
        {
            await cliente.ConnectAsync(mailpit.Hostname, mailpit.GetMappedPublicPort(MailpitDeTeste.PortaSmtp), prazo.Token);
            (await LerSaudacaoAsync(cliente, prazo.Token)).Should().StartWith("220");
            return relogio.Elapsed;
        }
        catch (OperationCanceledException) when (prazo.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<string?> LerSaudacaoAsync(TcpClient cliente, CancellationToken prazo)
    {
        using StreamReader leitor = new(cliente.GetStream());
        return await leitor.ReadLineAsync(prazo);
    }
}
