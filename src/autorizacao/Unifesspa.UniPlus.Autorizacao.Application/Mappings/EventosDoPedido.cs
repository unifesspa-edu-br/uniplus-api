namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Os eventos que o pedido conta, montados na leitura a partir das colunas, das decisões, do aceite do termo e dos acessos
/// aos anexos, e os que ele acrescenta à trilha da concessão que gerou. Nunca trazem o conteúdo de um anexo, o texto do
/// termo nem o nome de uma ameaça.
/// </summary>
public sealed class EventosDoPedido : IEventosDaOrigem
{
    // O veredicto do anexo é do verificador, que não é pessoa: o autor é o sistema.
    private static readonly IdentidadeDto Verificador = new("uniplus:sistema", "verificador");

    private readonly IPedidoDeConcessaoRepository _repositorio;

    public EventosDoPedido(IPedidoDeConcessaoRepository repositorio)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        _repositorio = repositorio;
    }

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

        IEnumerable<EventoDaTrilhaDto> eventos =
        [
            .. DoSolicitante(pedido),
            .. DoAceite(pedido),
            .. leituras.Select(DaLeitura),
            .. pedido.Decisoes.SelectMany(DaDecisao),
        ];

        return [.. eventos.OrderBy(e => e.Instante)];
    }

    private static IEnumerable<EventoDaTrilhaDto> DoSolicitante(PedidoDeConcessao pedido)
    {
        IdentidadeDto solicitante = Dto(pedido.SolicitanteEmissor, pedido.SolicitanteSub, pedido.SolicitanteNome);

        return
        [
            new("pedido_criado", pedido.CriadoEm, solicitante, $"Justificativa: {pedido.Justificativa}"),
            .. pedido.Anexos.SelectMany(a => DoAnexo(a, solicitante)),
            .. DoCancelamento(pedido, solicitante),
        ];
    }

    private static IEnumerable<EventoDaTrilhaDto> DoAceite(PedidoDeConcessao pedido)
    {
        if (pedido.BeneficiarioAceitouEm is { } aceitouEm && pedido.Beneficiario is { } beneficiario)
        {
            yield return new("beneficiario_aceitou", aceitouEm, Dto(beneficiario.Emissor, beneficiario.Sub, beneficiario.Nome), null);
        }

        if (pedido.AceiteDoTermo is { } aceite)
        {
            yield return new(
                "termo_aceito", aceite.AceitoEm, Dto(aceite.PessoaEmissor, aceite.PessoaSub, aceite.PessoaNome), $"Versão {aceite.Numero}, resumo {aceite.Resumo}.");
        }
    }

    private static IEnumerable<EventoDaTrilhaDto> DoAnexo(AnexoDoPedido anexo, IdentidadeDto solicitante)
    {
        if (anexo.EnviadoEm is { } enviadoEm)
        {
            yield return new("anexo_enviado", enviadoEm, solicitante, $"Anexo {anexo.Id}.");
        }

        if (anexo.VerificadoEm is { } verificadoEm)
        {
            yield return new("anexo_verificado", verificadoEm, Verificador, $"Anexo {anexo.Id}. SHA-256 do PDF: {anexo.Sha256}.");
        }

        if (anexo.RecusadoEm is { } recusadoEm)
        {
            yield return new("anexo_recusado", recusadoEm, Verificador, $"Anexo {anexo.Id}. O verificador recusou o documento ou pediu o reenvio.");
        }
    }

    private static EventoDaTrilhaDto DaLeitura(LeituraDoAnexoDoPedido l) =>
        new("anexo_lido", l.Instante, Dto(l.LeitorEmissor, l.LeitorSub, l.LeitorNome), $"Anexo {l.AnexoId}.");

    private static EventoDaTrilhaDto[] DaDecisao(DecisaoDoPedido decisao)
    {
        IdentidadeDto decisor = Dto(decisao.DecisorEmissor, decisao.DecisorSub, decisao.DecisorNome);
        string etapa = $"Etapa: {decisao.Etapa.ToCodigo()}.";
        if (SemDecisao(decisao.Resultado) is { } semDecisao)
        {
            return [new(semDecisao.Tipo, decisao.Instante, decisor, $"{etapa} {semDecisao.Detalhe}")];
        }

        string motivo = decisao.Motivo is null ? string.Empty : $" Motivo: {decisao.Motivo}";
        EventoDaTrilhaDto decidida = new(decisao.Resultado.ToCodigo(), decisao.Instante, decisor, $"{etapa} Sensibilidade avaliada: {decisao.SensibilidadeAvaliada}.{motivo}");

        return decisao.Dispensa is { } dispensa
            ? [decidida, new("segunda_aprovacao_dispensada", decisao.Instante, decisor, $"Motivo: {dispensa}.")]
            : [decidida];
    }

    // A tentativa que não decidiu: o pedido voltou ao gestor ou foi devolvido ao solicitante.
    private static (string Tipo, string Detalhe)? SemDecisao(ResultadoDaDecisao resultado) => resultado switch
    {
        ResultadoDaDecisao.PerfilAlterado =>
            ("perfil_alterado_entre_etapas", "O perfil mudou depois da aprovação do gestor, e o pedido voltou a aguardá-lo."),
        ResultadoDaDecisao.PrazoVencido =>
            ("prazo_vencido_entre_etapas", "O prazo definido pelo gestor venceu antes desta aprovação, e o pedido voltou a aguardá-lo."),
        ResultadoDaDecisao.TermoExigido =>
            ("termo_exigido", "O perfil passou a exigir o termo de responsabilidade depois da criação, e o pedido foi devolvido."),
        _ => null,
    };

    private static IEnumerable<EventoDaTrilhaDto> DoCancelamento(PedidoDeConcessao pedido, IdentidadeDto solicitante)
    {
        if (pedido.Situacao == SituacaoDoPedido.Cancelado && pedido.EncerradoEm is { } canceladoEm)
        {
            yield return new("cancelado", canceladoEm, solicitante, null);
        }
    }

    private static IdentidadeDto Dto(string emissor, string sub, string? nome) => new(emissor, sub, nome);
}
