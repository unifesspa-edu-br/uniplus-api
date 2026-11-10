namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// O emissor único dos avisos da governança de perfis. Aplica as exclusões, mantém uma mensagem por identidade, deriva
/// o <c>MensagemId</c> do evento, do fato e da identidade, registra os identificadores de uma vez e só devolve o e-mail
/// dos que acabaram de entrar no registro. O aviso sem destinatário não sai e não falha a operação: a ausência vai ao
/// log, sem dado pessoal, e soma no contador de avisos sem destinatário.
/// </summary>
public sealed partial class EmissorDeAvisos
{
    private readonly IRegistroDeAvisos _registro;
    private readonly MetricasDaGovernancaDePerfis _metricas;
    private readonly ILogger<EmissorDeAvisos> _logger;

    public EmissorDeAvisos(IRegistroDeAvisos registro, MetricasDaGovernancaDePerfis metricas, ILogger<EmissorDeAvisos> logger)
    {
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(metricas);
        ArgumentNullException.ThrowIfNull(logger);

        _registro = registro;
        _metricas = metricas;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EnviarEmail>> EmitirAsync(PedidoDeAviso pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        string tipo = pedido.Modelo.Tipo.ToCodigo();
        Dictionary<Guid, Email> porId = Destinatarios(pedido);
        if (porId.Count == 0)
        {
            LogSemDestinatario(_logger, tipo, pedido.FatoId);
            _metricas.AvisoSemDestinatario(tipo);

            return [];
        }

        IReadOnlyCollection<Guid> novos = await _registro
            .RegistrarAsync(tipo, pedido.FatoId, [.. porId.Keys.Order()], cancellationToken)
            .ConfigureAwait(false);

        return [.. novos.Select(id => Montar(pedido.Modelo, id, porId[id]))];
    }

    // Um por identidade e por endereço, sem quem praticou o ato e sem endereço inválido.
    private static Dictionary<Guid, Email> Destinatarios(PedidoDeAviso pedido) =>
        pedido.Destinatarios
            .Where(d => !pedido.Excluidos.Contains(d.Pessoa))
            .DistinctBy(static d => d.Pessoa)
            .Select(d => (d.Pessoa, Email: Email.Criar(d.Email)))
            .Where(static d => d.Email.IsSuccess)
            .DistinctBy(static d => d.Email.Value!.Valor)
            .ToDictionary(d => IdDoAviso.De(new ChaveDoAviso(pedido.Modelo.Tipo, pedido.FatoId, pedido.Marco, d.Pessoa)), static d => d.Email.Value!);

    private static EnviarEmail Montar(ModeloDeAviso modelo, Guid mensagemId, Email destinatario) =>
        EnviarEmail.Para(
            mensagemId,
            destinatario,
            RedacaoDoAviso.Assunto(modelo.Tipo),
            RedacaoDoAviso.CorpoEmTexto(modelo),
            RedacaoDoAviso.CorpoEmHtml(modelo),
            modelo.Tipo.Finalidade());

    [LoggerMessage(Level = LogLevel.Information, Message = "Aviso {Tipo} do fato {FatoId} sem destinatário: nada foi emitido")]
    private static partial void LogSemDestinatario(ILogger logger, string tipo, Guid fatoId);
}
