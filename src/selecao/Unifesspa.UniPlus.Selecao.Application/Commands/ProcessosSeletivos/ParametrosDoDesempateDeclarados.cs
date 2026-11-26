namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.ProcessosSeletivos.Desempate;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Monta os parâmetros tipados de um critério de desempate a partir dos que o pedido declara, conforme o código da
/// regra referenciada. A recusa sai no campo relativo ao critério (<c>etapaRef</c>, <c>idadeMinima</c>, <c>fato</c>,
/// <c>operador</c>, <c>valor</c>, <c>regraCodigo</c>), que <see cref="RegrasDoDesempate.CampoDoCriterioDesempate"/>
/// leva ao caminho em que o pedido o recebe.
/// </summary>
internal static class ParametrosDoDesempateDeclarados
{
    public static Result<ParametrosCriterioDesempate> Montar(int ordem, ParametrosDoCriterioDesempateInput parametros, string regraCodigo) =>
        CatalogoDe<RegraDeDesempate>.TentarObter(regraCodigo, out RegraDeDesempate regra)
            ? DaRegra(ordem, parametros, regra)
            : Recusa("regraCodigo", new DomainError("CriterioDesempate.RegraTipoInvalido", "O código da regra de desempate não consta do catálogo de regras de desempate."));

    private static Result<ParametrosCriterioDesempate> DaRegra(int ordem, ParametrosDoCriterioDesempateInput parametros, RegraDeDesempate regra) =>
        regra switch
        {
            RegraDeDesempate.MaiorNotaNaEtapa => DaMaiorNotaNaEtapa(ordem, parametros),
            RegraDeDesempate.MaiorIdade => Result<ParametrosCriterioDesempate>.Success(new ParametrosDesempateMaiorIdade()),
            RegraDeDesempate.Idoso => DoIdoso(ordem, parametros),
            RegraDeDesempate.CondicaoSobreDado => DoPredicadoDeFato(parametros),
            RegraDeDesempate.MaiorNotaPorAreaDoEnem => DaMaiorNotaPorAreaDoEnem(parametros),
            _ => throw new ArgumentOutOfRangeException(nameof(regra)),
        };

    private static Result<ParametrosCriterioDesempate> Recusa(string campo, DomainError erro) =>
        Result<ParametrosCriterioDesempate>.ValidationFailure([new(campo, erro)]);

    private static Result<ParametrosCriterioDesempate> DaMaiorNotaNaEtapa(int ordem, ParametrosDoCriterioDesempateInput parametros) =>
        parametros.EtapaRef is { } etapaRef
            ? Result<ParametrosCriterioDesempate>.Success(new ParametrosDesempateMaiorNotaEtapa(etapaRef))
            : Recusa("etapaRef", new DomainError(
                "CriterioDesempate.EtapaRefObrigatorio",
                $"O critério na ordem {ordem} exige EtapaRef para a regra {RegraDeDesempate.MaiorNotaNaEtapa.Codigo()}."));

    private static Result<ParametrosCriterioDesempate> DoIdoso(int ordem, ParametrosDoCriterioDesempateInput parametros) =>
        parametros.IdadeMinima is { } idadeMinima
            ? Result<ParametrosCriterioDesempate>.Success(new ParametrosDesempateIdoso(idadeMinima))
            : Recusa("idadeMinima", new DomainError(
                "CriterioDesempate.IdadeMinimaObrigatoria",
                $"O critério na ordem {ordem} exige IdadeMinima para a regra {RegraDeDesempate.Idoso.Codigo()}."));

    /// <summary>
    /// A ordem ausente vira lista vazia, e item nulo vira texto vazio: quem recusa, com o campo de cada item, é
    /// <c>CriterioDesempate.Criar</c>, junto com as demais violações. Um item além do teto basta para a recusa da lista
    /// inteira, e o resto nem é lido.
    /// </summary>
    private static Result<ParametrosCriterioDesempate> DaMaiorNotaPorAreaDoEnem(ParametrosDoCriterioDesempateInput parametros) =>
        Result<ParametrosCriterioDesempate>.Success(new ParametrosDesempateMaiorNotaAreaEnem(
            [.. (parametros.Areas ?? []).Take(ParametrosDesempateMaiorNotaAreaEnem.AreasMaximo + 1).Select(static area => area ?? string.Empty)]));

    private static Result<ParametrosCriterioDesempate> DoPredicadoDeFato(ParametrosDoCriterioDesempateInput parametros) =>
        ParametrosDesempatePredicadoFato.Criar(
            parametros.Fato,
            parametros.Operador,
            string.IsNullOrWhiteSpace(parametros.Valor) ? null : InterpretarValor(parametros.Valor));

    /// <summary>
    /// <see cref="ParametrosDoCriterioDesempateInput.Valor"/> permanece texto, mas quando o texto já É um JSON válido
    /// (booleano, número, string entre aspas, ou array para o operador ALGUM_DE), ele é interpretado como tal, para que a
    /// matriz operador × domínio (ADR-0111) tenha o tipo correto a validar. Texto que não é JSON válido (ex.: um
    /// código categórico sem aspas) é tratado como o escalar de string que representa.
    /// </summary>
    private static JsonElement InterpretarValor(string valor)
    {
        try
        {
            using JsonDocument documento = JsonDocument.Parse(valor);
            return documento.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(valor);
        }
    }
}
