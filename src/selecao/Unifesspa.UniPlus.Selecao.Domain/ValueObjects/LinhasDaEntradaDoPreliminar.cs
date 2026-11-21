namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Globalization;

using Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// A forma canônica das linhas de <see cref="EntradaDoPreliminar"/> que a fase e a etapa escrevem do mesmo
/// jeito: o par campo e valor, a referência de regra, os parâmetros do prazo de recurso, a regra de
/// deliberação da banca, os tipos de decisão declarados e o produto em que o recurso ancora.
/// </summary>
internal static class LinhasDaEntradaDoPreliminar
{
    private const string FormatoDoNumero = "0.############################";

    internal static string Linha(string campo, object? valor) => $"{campo}={Valor(valor)}";

    internal static string Valor(object? valor) => valor switch
    {
        null => string.Empty,
        string texto => HashCanonicalComputer.NormalizeNfc(texto),
        bool booleano => booleano ? "true" : "false",
        DateTimeOffset instante => instante.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        decimal numero => numero.ToString(FormatoDoNumero, CultureInfo.InvariantCulture),
        IFormattable formatavel => formatavel.ToString(null, CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(valor), valor, "O valor não tem forma canônica na entrada do preliminar."),
    };

    internal static IEnumerable<string> DaReferencia(string campo, ReferenciaRegra? regra) =>
        regra is null
            ? []
            : [Linha($"{campo}.codigo", regra.Codigo), Linha($"{campo}.versao", regra.Versao), Linha($"{campo}.hash", regra.Hash)];

    // O prazo da 2ª instância fica de fora: ele vale pela versão vigente quando o indeferimento é publicado ao candidato, e mudá-lo
    // não pode exigir ciclo novo do recurso da 1ª instância.
    internal static IEnumerable<string> DosParametrosDoRecurso(string campo, ParametrosRegraPrazoRecurso parametros) =>
    [
        Linha($"{campo}.prazoValor", parametros.PrazoDeInterposicao.Valor),
        Linha($"{campo}.prazoUnidade", parametros.PrazoDeInterposicao.Unidade.Codigo()),
        Linha($"{campo}.admiteInstanciaSuperior", parametros.AdmiteInstanciaSuperior),
        Linha($"{campo}.efeitoSuspensivo", parametros.EfeitoSuspensivo.Codigo()),
        Linha($"{campo}.guardaDaVaga", parametros.GuardaDaVaga.Codigo()),
    ];

    /// <summary>Os procedimentos declarados da janela: o tipo de decisão e o alvo identificam, e a regra congelada vale.</summary>
    internal static IEnumerable<string> DosProcedimentos(string campo, ProcedimentosDeRecurso procedimentos) =>
        procedimentos.Itens.SelectMany(procedimento => DaReferencia(
            $"{campo}.procedimentos[{Valor(procedimento.TipoDeDecisao.ToCodigo())}|{ChaveDoAlvo(procedimento.Alvo)}].regra", procedimento.Regra));

    private static string ChaveDoAlvo(AlvoDoProcedimentoDeRecurso? alvo) => alvo is null ? string.Empty : $"{alvo.Tipo}:{Valor(alvo.Valor)}";

    internal static IEnumerable<string> DaDeliberacao(string campo, RegraDeDeliberacao? regra) =>
        regra is null
            ? []
            :
            [
                Linha($"{campo}.modo", regra.Modo),
                Linha($"{campo}.integrantes", regra.Integrantes),
                Linha($"{campo}.suplentes", regra.Suplentes),
                Linha($"{campo}.quorum", regra.Quorum),
                Linha($"{campo}.criterio", regra.Criterio),
                Linha($"{campo}.desempate", regra.Desempate),
                Linha($"{campo}.encerramento", regra.Encerramento),
                Linha($"{campo}.semUnanimidade", regra.SemUnanimidade),
            ];

    /// <summary>Uma linha por tipo de decisão declarado: a mesma declaração em outra ordem dá as mesmas linhas.</summary>
    internal static IEnumerable<string> DosTiposDeDecisao(IEnumerable<string> tipos) =>
        tipos.Select(static tipo => Linha($"tiposDeDecisao[{Valor(tipo)}]", true));

    /// <summary>O produto em que o prazo ancora, pelo tipo de ato e pelo papel e nunca pelo <c>Id</c>.</summary>
    internal static IEnumerable<string> DaAncora(string campo, IEnumerable<IProdutoPublicado> produtos, Guid produtoAncoraId)
    {
        IProdutoPublicado? ancora = ProdutoAncora(produtos, produtoAncoraId);

        return [Linha($"{campo}.atoCodigo", ancora?.AtoCodigo), Linha($"{campo}.papel", ancora?.Papel)];
    }

    internal static IProdutoPublicado? ProdutoAncora(IEnumerable<IProdutoPublicado> produtos, Guid produtoAncoraId) =>
        produtos.FirstOrDefault(p => p.Id == produtoAncoraId);
}
