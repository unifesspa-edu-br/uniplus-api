namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Converte o corpo da requisição para a forma que o domínio valida. Um valor nulo no JSON
/// (<c>[null]</c>) vira valor sem código e sem rótulo, para voltar como erro do campo daquele índice em
/// vez de derrubar a requisição.
/// </summary>
internal static class DefinicaoDoComando
{
    private const string Caminho = "definicao";

    public static DefinicaoInformada De(CriarTipoConceitoCommand comando) => Montar(comando.Definicao);

    public static DefinicaoInformada De(AtualizarTipoConceitoCommand comando) => Montar(comando.Definicao);

    /// <summary>
    /// As recusas dos campos da definição saem em <c>definicao.&lt;campo&gt;</c>, onde o corpo os recebe; a do código, que
    /// não faz parte dela, fica como está.
    /// </summary>
    public static IReadOnlyList<FieldError> NoCaminhoDaDefinicao(IEnumerable<FieldError> erros) =>
        [.. erros.Select(static erro => erro.Field == "codigo" ? erro : erro.NoCaminhoDe(Caminho))];

    private static DefinicaoInformada Montar(DefinicaoDoTipoConceitoInput? definicao) =>
        new(
            definicao?.Nome,
            definicao?.Descricao,
            definicao?.Natureza,
            definicao?.Numerico is { } numerico ? new EscalaInformada(numerico.EscalaMinima, numerico.EscalaMaxima, numerico.CasasDecimais) : null,
            definicao?.Valores is { } valores ? [.. valores.Select(static valor => ValorInformadoDe(valor))] : null);

    private static ValorInformado ValorInformadoDe(ValorCategoricoCommand? valor) =>
        valor is null
            ? new ValorInformado(null, null, null)
            : new ValorInformado(valor.Codigo, valor.Rotulo, valor.ContaComoAprovacao);
}
