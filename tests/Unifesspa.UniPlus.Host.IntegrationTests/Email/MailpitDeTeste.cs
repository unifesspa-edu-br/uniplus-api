namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using Docker.DotNet.Models;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

/// <summary>O contêiner de captura de e-mail, com a mesma configuração para todas as fixtures e para o teste dela.</summary>
internal static class MailpitDeTeste
{
    public const ushort PortaSmtp = 1025;
    public const ushort PortaHttp = 8025;

    /// <summary>
    /// Monta o Mailpit. Ele faz DNS reverso de cada conexão SMTP antes da saudação, e DNS externo lento trava a fila de
    /// envio, que entrega uma mensagem por vez: por isso a consulta fica desligada.
    /// </summary>
    /// <param name="servidorDns">Servidor de DNS do contêiner; nulo mantém o padrão do Docker.</param>
    /// <param name="comDnsReverso">Religa a consulta, só para o teste que mostra o efeito dela.</param>
    public static IContainer Criar(string? servidorDns = null, bool comDnsReverso = false)
    {
        ContainerBuilder builder = new ContainerBuilder("axllent/mailpit:v1.27")
            .WithPortBinding(PortaSmtp, true)
            .WithPortBinding(PortaHttp, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(PortaHttp).ForPath("/readyz")));
        builder = comDnsReverso ? builder : builder.WithEnvironment("MP_SMTP_DISABLE_RDNS", "true");

        return (servidorDns is null ? builder : builder.WithCreateParameterModifier(parametros =>
        {
            parametros.HostConfig ??= new HostConfig();
            parametros.HostConfig.DNS = [servidorDns];
        })).Build();
    }

    /// <summary>
    /// Um servidor de DNS que recebe as consultas por UDP e TCP na porta 53 e nunca responde: a consulta fica pendente
    /// até o prazo do resolvedor, sem o erro imediato que uma porta fechada ou uma rede inalcançável devolveriam.
    /// </summary>
    public static IContainer CriarDnsMudo() =>
        new ContainerBuilder("python:3.13-alpine")
            .WithCommand("python", "-c", DnsMudo)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("dns mudo"))
            .Build();

    private const string DnsMudo =
        "import socket,time\n"
        + "u=socket.socket(socket.AF_INET,socket.SOCK_DGRAM);u.bind(('0.0.0.0',53))\n"
        + "t=socket.socket();t.bind(('0.0.0.0',53));t.listen(64)\n"
        + "print('dns mudo',flush=True)\n"
        + "while True: time.sleep(3600)\n";
}
