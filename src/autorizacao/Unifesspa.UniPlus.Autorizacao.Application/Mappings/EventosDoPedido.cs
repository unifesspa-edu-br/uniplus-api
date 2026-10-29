namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Os eventos que o pedido conta, montados na leitura a partir das colunas, das decisões e dos acessos aos anexos, e os
/// que ele acrescenta à trilha da concessão que gerou. Nunca trazem o conteúdo de um anexo nem o nome de uma ameaça.
/// </summary>
public sealed class EventosDoPedido : IEventosDaOrigem
{
    private readonly IPedidoDeConcessaoRepository _repositorio;

    public EventosDoPedido(IPedidoDeConcessaoRepository repositorio)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        _repositorio = repositorio;
    }

    // O veredicto do anexo é do verificador, que não é pessoa: o autor é o sistema.
    private static readonly IdentidadeDto Verificador = new("uniplus:sistema", "verificador");

    public TipoDeOrigem Tipo => TipoDeOrigem.Pedido;

    public async Task<IReadOnlyList<EventoDaTrilhaDto>?> ObterAsync(Guid origemId, CancellationToken cancellationToken)
    {
        PedidoDeConcessao? pedido = await _repositorio.ObterParaLeituraAsync(origemId, cancellationToken).ConfigureAwait(false);
        if (pedido is null)
        {
            return null;
        }

        IReadOnlyList<LeituraDoAnexoDoPedido> leituras = await _repositorio
            .ListarLeiturasAsync(origemId, cancellationToken).ConfigureAwait(false);

        return Montar(pedido, leituras);
    }

    public static IReadOnlyList<EventoDaTrilhaDto> Montar(PedidoDeConcessao pedido, IReadOnlyList<LeituraDoAnexoDoPedido> leituras)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(leituras);

        IdentidadeDto solicitante = new(pedido.SolicitanteEmissor, pedido.SolicitanteSub, pedido.SolicitanteNome);
        List<EventoDaTrilhaDto> eventos =
        [
            new("pedido_criado", pedido.CriadoEm, solicitante, $"Justificativa: {pedido.Justificativa}"),
        ];

        if (pedido.BeneficiarioAceitouEm is { } aceitouEm && pedido.Beneficiario is { } beneficiario)
        {
            eventos.Add(new("beneficiario_aceitou", aceitouEm, new IdentidadeDto(beneficiario.Emissor, beneficiario.Sub, beneficiario.Nome), null));
        }

        foreach (AnexoDoPedido anexo in pedido.Anexos)
        {
            if (anexo.EnviadoEm is { } enviadoEm)
            {
                eventos.Add(new("anexo_enviado", enviadoEm, solicitante, $"Anexo {anexo.Id}."));
            }

            if (anexo.VerificadoEm is { } verificadoEm)
            {
                eventos.Add(new("anexo_verificado", verificadoEm, Verificador, $"Anexo {anexo.Id}. SHA-256 do PDF: {anexo.Sha256}."));
            }

            if (anexo.RecusadoEm is { } recusadoEm)
            {
                eventos.Add(new("anexo_recusado", recusadoEm, Verificador, $"Anexo {anexo.Id}. O verificador recusou o documento ou pediu o reenvio."));
            }
        }

        eventos.AddRange(leituras.Select(l => new EventoDaTrilhaDto(
            "anexo_lido", l.Instante, new IdentidadeDto(l.LeitorEmissor, l.LeitorSub, l.LeitorNome), $"Anexo {l.AnexoId}.")));

        foreach (DecisaoDoPedido decisao in pedido.Decisoes)
        {
            IdentidadeDto decisor = new(decisao.DecisorEmissor, decisao.DecisorSub, decisao.DecisorNome);
            if (decisao.Resultado == ResultadoDaDecisao.PerfilAlterado)
            {
                eventos.Add(new(
                    "perfil_alterado_entre_etapas",
                    decisao.Instante,
                    decisor,
                    $"Etapa: {decisao.Etapa.ToCodigo()}. O perfil mudou depois da aprovação do gestor, e o pedido voltou a aguardá-lo."));
                continue;
            }

            string detalhe = $"Etapa: {decisao.Etapa.ToCodigo()}. Sensibilidade avaliada: {decisao.SensibilidadeAvaliada}."
                + (decisao.Motivo is null ? string.Empty : $" Motivo: {decisao.Motivo}");
            eventos.Add(new(decisao.Resultado.ToCodigo(), decisao.Instante, decisor, detalhe));

            if (decisao.Dispensa is { } dispensa)
            {
                eventos.Add(new("segunda_aprovacao_dispensada", decisao.Instante, decisor, $"Motivo: {dispensa}."));
            }
        }

        if (pedido.Situacao == SituacaoDoPedido.Cancelado && pedido.EncerradoEm is { } canceladoEm)
        {
            eventos.Add(new("cancelado", canceladoEm, solicitante, null));
        }

        return [.. eventos.OrderBy(e => e.Instante)];
    }
}
