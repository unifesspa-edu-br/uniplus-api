namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O que o pedido de correção diz do ato corrigido, antes de qualquer consulta.</summary>
internal enum SituacaoDaCorrecaoPedida
{
    SemCorrecao,
    CorrigeUmAto,

    /// <summary>O campo do ato corrigido foi recusado na forma: nenhuma consulta lê um valor recusado.</summary>
    RecusadaNaForma,
}

/// <summary>A situação da correção pedida e, quando corrige, o ato que o pedido aponta.</summary>
internal readonly record struct CorrecaoPedida(SituacaoDaCorrecaoPedida Situacao, Guid? AtoCorrigidoId)
{
    public static CorrecaoPedida De(Guid? atoCorrigidoId, Result formaDaCorrecao)
    {
        ArgumentNullException.ThrowIfNull(formaDaCorrecao);

        if (formaDaCorrecao.Errors.Any(static e => e.Field == AtoDoProcesso.CampoDoAtoCorrigido))
        {
            return new(SituacaoDaCorrecaoPedida.RecusadaNaForma, null);
        }

        return atoCorrigidoId is { } corrigido
            ? new(SituacaoDaCorrecaoPedida.CorrigeUmAto, corrigido)
            : new(SituacaoDaCorrecaoPedida.SemCorrecao, null);
    }
}

/// <summary>
/// A forma de todo pedido de publicação de ato, conferida sem ler nada, na ordem dos campos do pedido: o que o pedido
/// tem de trazer (tipo, data, assinante), a forma dos documentos e a coerência entre o ato corrigido e o motivo.
/// </summary>
/// <param name="Recusas">As causas da forma, na ordem dos campos do pedido.</param>
/// <param name="Documentos">A forma dos documentos e o que a conferência deles lê.</param>
/// <param name="Correcao">A situação da correção pedida.</param>
internal sealed record PedidoDoAtoDoProcesso(
    IReadOnlyList<FieldError> Recusas, PedidoDosDocumentosDoAto Documentos, CorrecaoPedida Correcao)
{
    /// <summary>O pedido ainda não conferido, sem causa nem documento: o que fica quando a forma não chega a ser lida.</summary>
    public static PedidoDoAtoDoProcesso Vazio { get; } = new([], PedidoDosDocumentosDoAto.Vazio, new(SituacaoDaCorrecaoPedida.SemCorrecao, null));

    /// <param name="dados">O pedido.</param>
    /// <param name="formaPropria">As causas de forma dos campos que só este tipo de pedido tem; vêm antes das comuns.</param>
    public static PedidoDoAtoDoProcesso De(IDadosDoAtoPublicado dados, IReadOnlyList<FieldError> formaPropria)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(formaPropria);

        PedidoDosDocumentosDoAto documentos = PedidoDosDocumentosDoAto.De(dados.DocumentoPrincipalId, dados.Anexos);
        Result correcao = AtoDoProcesso.ValidarCorrecao(dados.AtoCorrigidoId, dados.MotivoDaCorrecao);
        FieldError[] recusas = [.. formaPropria, .. AusenciasDosCampos(dados), .. documentos.Recusas, .. correcao.Errors];

        return new(recusas, documentos, CorrecaoPedida.De(dados.AtoCorrigidoId, correcao));
    }

    // A data e o assinante, na ordem do pedido: o tipo vem antes, da forma própria, e o documento principal depois, da
    // forma dos documentos. A leitura do pedido deixa ausentes passarem; ninguém os lê sem antes conferir aqui.
    private static IEnumerable<FieldError> AusenciasDosCampos(IDadosDoAtoPublicado dados)
    {
        if (dados.DataDoAto is null)
        {
            yield return AusenciasNosDadosDoAto.Data();
        }

        if (string.IsNullOrWhiteSpace(dados.Assinante))
        {
            yield return AusenciasNosDadosDoAto.Assinante();
        }
    }
}
