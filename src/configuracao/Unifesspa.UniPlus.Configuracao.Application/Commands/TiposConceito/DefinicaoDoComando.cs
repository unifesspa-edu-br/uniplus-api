namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Converte o corpo da requisição para a forma que o domínio valida. Um valor nulo no JSON
/// (<c>[null]</c>) vira valor sem código e sem rótulo, para voltar como erro do campo daquele índice em
/// vez de derrubar a requisição.
/// </summary>
internal static class DefinicaoDoComando
{
    public static DefinicaoInformada De(CriarTipoConceitoCommand comando) =>
        Montar(comando.Nome, comando.Descricao, comando.Natureza, comando.Numerico, comando.Valores);

    public static DefinicaoInformada De(AtualizarTipoConceitoCommand comando) =>
        Montar(comando.Nome, comando.Descricao, comando.Natureza, comando.Numerico, comando.Valores);

    private static DefinicaoInformada Montar(
        string? nome,
        string? descricao,
        string? natureza,
        EscalaNumericaCommand? numerico,
        IReadOnlyList<ValorCategoricoCommand>? valores) =>
        new(
            nome,
            descricao,
            natureza,
            numerico is null ? null : new EscalaInformada(numerico.EscalaMinima, numerico.EscalaMaxima, numerico.CasasDecimais),
            valores is null ? null : [.. valores.Select(static valor => valor is null
                ? new ValorInformado(null, null, null)
                : new ValorInformado(valor.Codigo, valor.Rotulo, valor.ContaComoAprovacao))]);
}
