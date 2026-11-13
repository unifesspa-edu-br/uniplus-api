namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Os avisos de cada ato da providência, emitidos depois de a gravação ser aceita, na mesma transação (UNI-REQ-0375,
/// ADR-0164). Quem recebe cada aviso se decide num ponto só, <see cref="PedidoAsync"/>. Nenhum aviso leva fundamento,
/// motivo, nome de pessoa nem e-mail de outra pessoa: só o nome do perfil e o link.
/// </summary>
public sealed partial class AvisosDaProvidencia
{
    private readonly EmissorDeAvisos _emissor;
    private readonly GruposDaGovernanca _grupos;
    private readonly ILinkDaGovernanca _link;
    private readonly ILogger<AvisosDaProvidencia> _logger;

    public AvisosDaProvidencia(EmissorDeAvisos emissor, GruposDaGovernanca grupos, ILinkDaGovernanca link, ILogger<AvisosDaProvidencia> logger)
    {
        ArgumentNullException.ThrowIfNull(emissor);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(logger);

        _emissor = emissor;
        _grupos = grupos;
        _link = link;
        _logger = logger;
    }

    /// <summary>Os avisos do ato que acabou de ser gravado, conforme o estado em que ele deixou a providência.</summary>
    public async Task<IReadOnlyList<EnviarEmail>> AvisarAsync(ProvidenciaComConcessao providencia, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providencia);

        if (providencia.Providencia is { Estado: EstadoDaProvidencia.Aberta, SolicitadaPorEmail: null })
        {
            LogAberturaSemEmailVerificado(_logger, providencia.Providencia.Id);
        }

        return await EmitirAsync(AvisosDoAto(providencia.Providencia.Estado), providencia, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<EnviarEmail>> EmitirAsync(
        TipoDeAviso[] tipos, ProvidenciaComConcessao providencia, CancellationToken cancellationToken)
    {
        List<EnviarEmail> avisos = [];
        foreach (TipoDeAviso tipo in tipos)
        {
            PedidoDeAviso pedido = await PedidoAsync(tipo, providencia, cancellationToken).ConfigureAwait(false);
            avisos.AddRange(await _emissor.EmitirAsync(pedido, cancellationToken).ConfigureAwait(false));
        }

        return avisos;
    }

    private static TipoDeAviso[] AvisosDoAto(EstadoDaProvidencia estado) => estado switch
    {
        EstadoDaProvidencia.Aberta => [TipoDeAviso.ProvidenciaSolicitada],
        EstadoDaProvidencia.Respondida => [TipoDeAviso.ProvidenciaRespondida],
        _ => [TipoDeAviso.ProvidenciaAtendida, TipoDeAviso.ConcessaoRevogadaNoAtendimento],
    };

    // Quem recebe cada aviso. A abertura vai a todos os gestores de acessos e Representantes do controlador, inclusive
    // quem solicitou, menos o beneficiário da concessão, por decisão do líder técnico. A resposta e o atendimento vão a
    // quem solicitou, pelo e-mail gravado na abertura, mesmo que ele já não tenha a permissão de solicitar. A revogação
    // pelo atendimento vai ao beneficiário, com o link para pedir acesso de novo.
    private async Task<PedidoDeAviso> PedidoAsync(TipoDeAviso tipo, ProvidenciaComConcessao item, CancellationToken cancellationToken)
    {
        (Providencia p, Concessao c) = (item.Providencia, item.Alvo.Concessao);

        return tipo switch
        {
            TipoDeAviso.ProvidenciaSolicitada => Pedido(
                tipo, item, await _grupos.ListarAsync(GrupoDaGovernanca.AdministracaoDasConcessoes, cancellationToken).ConfigureAwait(false), [c.Beneficiario]),
            TipoDeAviso.ConcessaoRevogadaNoAtendimento => Pedido(tipo, item, [new(c.Beneficiario, c.BeneficiarioEmail ?? string.Empty)], []),
            _ => Pedido(tipo, item, [new(p.SolicitadaPor, p.SolicitadaPorEmail ?? string.Empty)], []),
        };
    }

    private PedidoDeAviso Pedido(
        TipoDeAviso tipo, ProvidenciaComConcessao item, IReadOnlyList<DestinatarioDoAviso> destinatarios, IReadOnlyCollection<Identidade> excluidos)
    {
        string link = tipo == TipoDeAviso.ConcessaoRevogadaNoAtendimento ? _link.DoNovoPedido() : _link.DaProvidencia(item.Providencia.Id);

        return new PedidoDeAviso(new ModeloDeAviso(tipo, item.Alvo.PerfilNome, null, [], link), item.Providencia.Id, string.Empty, destinatarios, excluidos);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Providência {ProvidenciaId} aberta sem e-mail verificado de quem a solicitou: os avisos da resposta e do atendimento não terão destinatário")]
    private static partial void LogAberturaSemEmailVerificado(ILogger logger, Guid providenciaId);
}
