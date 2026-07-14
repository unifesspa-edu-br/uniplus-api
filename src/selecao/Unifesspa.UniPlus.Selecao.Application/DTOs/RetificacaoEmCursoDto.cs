namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Entities;

/// <summary>
/// A sessão editorial em curso, como o administrador a vê (ADR-0110 D3).
/// </summary>
/// <remarks>
/// O <see cref="ETag"/> viaja também no <b>header</b> da resposta — é lá que o cliente HTTP
/// o lê para devolver no <c>If-Match</c>. No corpo ele é conveniência de diagnóstico e de
/// front-end; o header é o contrato.
/// </remarks>
public sealed record RetificacaoEmCursoDto(
    Guid Id,
    Guid ProcessoSeletivoId,
    string Motivo,
    Guid VersaoBaseId,
    int NumeroVersaoBase,
    DateTimeOffset AbertoEm,
    string AbertoPorSub,
    int Revisao,
    string ETag)
{
    public static RetificacaoEmCursoDto De(RascunhoRetificacao rascunho)
    {
        ArgumentNullException.ThrowIfNull(rascunho);

        return new RetificacaoEmCursoDto(
            rascunho.Id,
            rascunho.ProcessoSeletivoId,
            rascunho.Motivo,
            rascunho.VersaoBaseId,
            rascunho.NumeroVersaoBase,
            rascunho.AbertoEm,
            rascunho.AbertoPorSub,
            rascunho.Revisao,
            rascunho.ETag);
    }
}
