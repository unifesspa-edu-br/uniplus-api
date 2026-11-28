namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.ValueObjects;

using Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>O processo divulgado e o instante do comando: o que as consultas seguintes precisam para correr.</summary>
internal readonly record struct EstadoDoPedidoConferido(ProcessoDivulgadoConferido Processo, InstanteDoComando Instante);

/// <summary>
/// O pedido depois do instante, da forma, do estado do processo e dos documentos. É o único lugar que junta as
/// causas de recusa de um pedido de publicação, e só ele monta o ato a publicar. O instante que não se resolveu é
/// defeito de instalação e, quando existe, é a única causa devolvida.
/// </summary>
/// <param name="Dados">O pedido.</param>
/// <param name="Pedido">A forma do pedido.</param>
/// <param name="Estado">O estado conferido; nulo quando o processo foi recusado e nenhuma consulta pode correr.</param>
/// <param name="RecusasDoEstado">As causas do estado do processo, sem campo.</param>
/// <param name="Documentos">Os documentos conferidos; sem leitura quando o estado foi recusado.</param>
/// <param name="FalhaDoInstante">A falha ao resolver o instante do comando; nula quando o instante foi resolvido.</param>
internal sealed record PedidoDoAtoConferido(
    IDadosDoAtoPublicado Dados,
    PedidoDoAtoDoProcesso Pedido,
    EstadoDoPedidoConferido? Estado,
    IReadOnlyList<FieldError> RecusasDoEstado,
    ResultadoDosDocumentos Documentos,
    DomainError? FalhaDoInstante = null)
{
    /// <summary>O pedido cujo instante não se resolveu: a forma não é conferida e nada mais é lido.</summary>
    public static PedidoDoAtoConferido SemInstante(IDadosDoAtoPublicado dados, DomainError falhaDoInstante) =>
        new(dados, PedidoDoAtoDoProcesso.Vazio, null, [], ResultadoDosDocumentos.NaoLido, falhaDoInstante);

    /// <summary>
    /// Devolve a falha do instante sozinha, quando houve. Senão junta as causas na ordem única das recusas: a forma, o
    /// estado do processo, as consultas anteriores aos documentos, os documentos e as consultas posteriores. Sem causa
    /// nenhuma, monta o ato.
    /// </summary>
    public Result<AtoAPublicar> Reunir(
        IReadOnlyList<FieldError> antesDosDocumentos, IReadOnlyList<FieldError> depoisDosDocumentos, Func<AtoAPublicar> montar)
    {
        ArgumentNullException.ThrowIfNull(montar);

        if (FalhaDoInstante is { } falha)
        {
            return Result<AtoAPublicar>.Failure(falha);
        }

        FieldError[] recusas = [.. Pedido.Recusas, .. RecusasDoEstado, .. antesDosDocumentos, .. Documentos.Recusas, .. depoisDosDocumentos];

        return recusas.Length == 0
            ? Result<AtoAPublicar>.Success(montar())
            : Result<AtoAPublicar>.ValidationFailure(recusas);
    }

    /// <summary>
    /// O ato com o que foi conferido: a data, o assinante, o processo, o instante e os documentos. Só vale sem nenhuma
    /// causa: a forma do pedido já recusou a data e o assinante ausentes.
    /// </summary>
    public AtoAPublicar Ato(TipoAtoPublicadoView tipo, IdentidadeDoItemDoEdital? itemDoEdital = null, string? contaId = null)
    {
        EstadoDoPedidoConferido estado = Estado!.Value;

        return new(
            Dados, Dados.DataDoAto!.Value, Dados.Assinante!, estado.Processo, tipo, Documentos.Conferidos!,
            estado.Instante.Instante, itemDoEdital, contaId);
    }
}
