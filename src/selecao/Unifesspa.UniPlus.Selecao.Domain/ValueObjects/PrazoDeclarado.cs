namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Um prazo do recurso como quem configura o declarou: o valor e a unidade, cada um ausente ou não, e a unidade como o código
/// recebido. Quem o confere é o domínio, uma só vez, para o prazo de interposição e para o da 2ª instância.
/// </summary>
/// <param name="Valor">Magnitude declarada, ou <see langword="null"/> quando não foi informada.</param>
/// <param name="Unidade">Código de contrato da unidade declarada, ou <see langword="null"/> quando não foi informada.</param>
public sealed record PrazoDeclarado(decimal? Valor, string? Unidade)
{
    /// <summary>Nenhuma das duas partes foi declarada.</summary>
    public static PrazoDeclarado Ausente { get; } = new(null, null);

    /// <summary>
    /// A primeira recusa do prazo, na ordem: valor ausente ou não positivo, unidade ausente ou desconhecida, dias corridos
    /// e fração de dia útil. Uma recusa por prazo, porque corrigir a primeira pode resolver as seguintes.
    /// </summary>
    /// <param name="campoDoValor">O campo do valor, relativo ao recurso.</param>
    /// <param name="campoDaUnidade">O campo da unidade, relativo ao recurso.</param>
    /// <param name="nomeDoPrazo">Como a mensagem chama o prazo: "prazo de interposição" ou "prazo da 2ª instância".</param>
    internal RecusaDosParametrosDoRecurso? Recusa(string campoDoValor, string campoDaUnidade, string nomeDoPrazo)
    {
        if (Valor is not > 0m)
        {
            return new(MotivoDaRecusaDosParametros.PrazoNaoPositivo, campoDoValor, $"Informe o {nomeDoPrazo} com valor maior que zero.");
        }

        return CatalogoDe<UnidadePrazo>.TentarObter(Unidade, out UnidadePrazo unidade)
            ? RecusaDaUnidade(unidade, campoDoValor, campoDaUnidade, nomeDoPrazo)
            : new(MotivoDaRecusaDosParametros.PrazoSemUnidadeDeclaravel, campoDaUnidade, $"Informe a unidade do {nomeDoPrazo}: dias úteis ou horas.");
    }

    /// <summary>O prazo conferido, ou <see langword="null"/> quando o valor ou a unidade não existem.</summary>
    internal PrazoDoRecurso? ComoPrazo() =>
        Valor is { } valor && CatalogoDe<UnidadePrazo>.TentarObter(Unidade, out UnidadePrazo unidade)
            ? new PrazoDoRecurso(valor, unidade)
            : null;

    private RecusaDosParametrosDoRecurso? RecusaDaUnidade(UnidadePrazo unidade, string campoDoValor, string campoDaUnidade, string nomeDoPrazo) => unidade switch
    {
        UnidadePrazo.Dias => new(MotivoDaRecusaDosParametros.PrazoEmDiasCorridos, campoDaUnidade,
            $"O {nomeDoPrazo} deve ser informado em dias úteis ou em horas; dias corridos não são aceitos."),
        UnidadePrazo.DiasUteis when Valor != decimal.Truncate(Valor.GetValueOrDefault()) => new(MotivoDaRecusaDosParametros.PrazoEmFracaoDeDiaUtil, campoDoValor,
            $"O {nomeDoPrazo} em dias úteis exige número inteiro. Para prazo menor que um dia, informe em horas."),
        _ => null,
    };
}
