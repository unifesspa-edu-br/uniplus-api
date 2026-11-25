namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura das restrições de valor de um item: faixa numérica, tamanho de texto, opções permitidas, municípios da UF e faixa de data.
/// </summary>
internal static class LeituraDasRestricoes
{
    /// <summary>
    /// As restrições de valor de um item, cada uma com as chaves do seu tipo. Nulo quando a forma é
    /// outra, com a recusa já propagada ao leitor.
    /// </summary>
    internal static List<RestricaoValor>? LerRestricoes(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonArray array = leitor.Array(item, "restricoes", path);
        List<RestricaoValor> restricoes = [];
        for (int i = 0; i < array.Count && !leitor.Falhou; i++)
        {
            if (!AdicionarRestricao(leitor, array, i, path, restricoes))
            {
                return null;
            }
        }

        return leitor.Falhou ? null : restricoes;
    }

    private static bool AdicionarRestricao(LeitorEnvelope leitor, JsonArray array, int indice, string path, List<RestricaoValor> restricoes)
    {
        Result<RestricaoValor>? restricao = LerRestricao(leitor, leitor.ItemObjeto(array, indice, $"{path}.restricoes"), $"{path}.restricoes[{indice}]");
        if (leitor.Falhou || restricao is null)
        {
            return false;
        }

        if (restricao.IsFailure)
        {
            leitor.Propagar<List<RestricaoValor>?>(restricao.Errors, $"{path}.restricoes[{indice}]");
            return false;
        }

        restricoes.Add(restricao.Value!);
        return true;
    }

    private static Result<RestricaoValor>? LerRestricao(LeitorEnvelope leitor, JsonObject bloco, string pathRestricao)
    {
        string tipo = leitor.TextoNaoVazio(bloco, "tipo", pathRestricao);
        if (leitor.Falhou)
        {
            return null;
        }

        return LerRestricaoTipada(leitor, bloco, pathRestricao, RestricoesDeValor.TipoDeclarado(tipo));
    }

    private static Result<RestricaoValor>? LerRestricaoTipada(LeitorEnvelope leitor, JsonObject bloco, string pathRestricao, TipoRestricaoValor? tipo) => tipo switch
    {
        TipoRestricaoValor.FaixaNumerica => LerFaixa(leitor, bloco, pathRestricao),
        TipoRestricaoValor.TamanhoTexto => LerTamanho(leitor, bloco, pathRestricao),
        TipoRestricaoValor.OpcoesPermitidas => LerOpcoesPermitidas(leitor, bloco, pathRestricao),
        TipoRestricaoValor.OpcoesDasRespostas => LerFatosDaRestricao(leitor, bloco, pathRestricao, RestricoesDeValor.DasRespostas),
        TipoRestricaoValor.MunicipiosDaUf => LerFatosDaRestricao(leitor, bloco, pathRestricao, RestricoesDeValor.DaUf),
        TipoRestricaoValor.FaixaDeData => LerFaixaDeData(leitor, bloco, pathRestricao),
        _ => leitor.Recusar<Result<RestricaoValor>?>($"'{pathRestricao}.tipo' não é um tipo de restrição de valor."),
    };

    internal static Result<RestricaoValor>? LerFaixa(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, "tipo", "minimo", "maximo");
        decimal? minimo = leitor.DecimalOpcional(bloco, "minimo", FormaDoItem.CasasDecimaisDaFaixa, path);
        decimal? maximo = leitor.DecimalOpcional(bloco, "maximo", FormaDoItem.CasasDecimaisDaFaixa, path);
        return leitor.Falhou ? null : RestricoesDeValor.Faixa(minimo, maximo);
    }

    internal static Result<RestricaoValor>? LerFaixaDeData(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, "tipo", "dataMinima", "dataMaxima");
        Result<LimiteDeData>? minima = LerLimiteDeData(leitor, bloco, "dataMinima", path);
        Result<LimiteDeData>? maxima = LerLimiteDeData(leitor, bloco, "dataMaxima", path);
        if (leitor.Falhou)
        {
            return null;
        }

        return MontarFaixaDeData(minima, maxima);
    }

    private static Result<RestricaoValor> MontarFaixaDeData(Result<LimiteDeData>? minima, Result<LimiteDeData>? maxima)
    {
        List<FieldError> causas = [];
        AcrescentarCausasDoLimite(causas, minima, "dataMinima");
        AcrescentarCausasDoLimite(causas, maxima, "dataMaxima");
        return causas.Count > 0 ? Result<RestricaoValor>.ValidationFailure(causas) : RestricoesDeValor.FaixaDeData(minima?.Value, maxima?.Value);
    }

    private static void AcrescentarCausasDoLimite(List<FieldError> causas, Result<LimiteDeData>? limite, string caminho)
    {
        if (limite is { IsFailure: true })
        {
            causas.AddRange(limite.Errors.Select(causa => causa.NoCaminhoDoItem(caminho)));
        }
    }

    /// <summary>O limite da faixa de data, com exatamente as chaves da âncora; nulo quando o limite falta.</summary>
    internal static Result<LimiteDeData>? LerLimiteDeData(LeitorEnvelope leitor, JsonObject bloco, string chave, string path)
    {
        JsonObject? limite = leitor.ObjetoOpcional(bloco, chave, path);
        if (limite is null)
        {
            return null;
        }

        string pathLimite = $"{path}.{chave}";
        string ancora = leitor.TextoNaoVazio(limite, "referenciaDaData", pathLimite);
        if (leitor.Falhou)
        {
            return null;
        }

        return LerLimitePelaAncora(leitor, limite, pathLimite, ancora);
    }

    private static Result<LimiteDeData>? LerLimitePelaAncora(LeitorEnvelope leitor, JsonObject limite, string path, string ancora) =>
        RestricaoValorJson.AncoraDoToken(ancora) switch
        {
            AncoraDoLimiteDeData.DataFixa => LerLimiteDeDataFixa(leitor, limite, path, ancora),
            AncoraDoLimiteDeData.Envio => LerLimiteRelativoAoEnvio(leitor, limite, path, ancora),
            _ => leitor.Propagar<Result<LimiteDeData>?>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.referenciaDaData' não é uma âncora de limite de data.")),
        };

    private static Result<LimiteDeData>? LerLimiteDeDataFixa(LeitorEnvelope leitor, JsonObject limite, string path, string ancora)
    {
        leitor.ExigirChaves(limite, path, "referenciaDaData", "data");
        DateOnly data = leitor.Data(limite, "data", path);
        return leitor.Falhou ? null : RestricoesDeValor.Limite(ancora, data, anos: null, dias: null);
    }

    private static Result<LimiteDeData>? LerLimiteRelativoAoEnvio(LeitorEnvelope leitor, JsonObject limite, string path, string ancora)
    {
        leitor.ExigirChaves(limite, path, "referenciaDaData", "anos", "dias");
        int anos = leitor.Inteiro(limite, "anos", path);
        int dias = leitor.Inteiro(limite, "dias", path);
        return leitor.Falhou ? null : RestricoesDeValor.Limite(ancora, data: null, anos, dias);
    }

    internal static Result<RestricaoValor>? LerTamanho(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, "tipo", "minimo", "maximo");
        int? minimo = leitor.InteiroOpcional(bloco, "minimo", path);
        int? maximo = leitor.InteiroOpcional(bloco, "maximo", path);
        return leitor.Falhou ? null : RestricoesDeValor.Tamanho(minimo, maximo);
    }

    internal static Result<RestricaoValor>? LerOpcoesPermitidas(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, "tipo", "entradas");
        JsonArray entradas = leitor.Array(bloco, "entradas", path);
        return LerEntradasDeOpcoes(leitor, entradas, path);
    }

    private static Result<RestricaoValor>? LerEntradasDeOpcoes(LeitorEnvelope leitor, JsonArray entradas, string path)
    {
        List<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> lidas = [];
        for (int i = 0; i < entradas.Count && !leitor.Falhou; i++)
        {
            Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)>? entrada = LerEntradaDeOpcoes(leitor, entradas, i, path);
            if (leitor.Falhou || entrada is null || entrada.IsFailure)
            {
                return entrada is { IsFailure: true } && !leitor.Falhou
                    ? FalhaDaEntrada(entrada, i)
                    : null;
            }
            lidas.Add(entrada.Value);
        }

        return leitor.Falhou ? null : RestricoesDeValor.Opcoes(lidas);
    }

    private static Result<RestricaoValor> FalhaDaEntrada(
        Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> entrada, int indice) =>
        Result<RestricaoValor>.ValidationFailure(entrada.Errors.Select(causa => causa.NoCaminhoDe($"entradas[{indice}].quando")).ToList());

    private static Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)>? LerEntradaDeOpcoes(
        LeitorEnvelope leitor, JsonArray entradas, int indice, string path)
    {
        JsonObject entrada = leitor.ItemObjeto(entradas, indice, $"{path}.entradas");
        string pathEntrada = $"{path}.entradas[{indice}]";
        leitor.ExigirChaves(entrada, pathEntrada, "quando", "valores");
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> quando = LeituraDasCondicoes.LerDnf(leitor, entrada, "quando", pathEntrada);
        IReadOnlyList<string> valores = leitor.Textos(entrada, "valores", pathEntrada);
        return leitor.Falhou ? null : MontarEntradaDeOpcoes(quando, valores);
    }

    private static Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> MontarEntradaDeOpcoes(
        IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> quando, IReadOnlyList<string> valores)
    {
        Result<PredicadoDnf?> predicado = LeituraDasCondicoes.PredicadoOpcional(quando);
        return predicado.IsFailure
            ? Result<(PredicadoDnf?, IReadOnlyCollection<string>)>.ValidationFailure(predicado.Errors)
            : Result<(PredicadoDnf?, IReadOnlyCollection<string>)>.Success((predicado.Value, valores));
    }

    /// <summary>A restrição que cita fatos — as opções formadas pelas respostas ou os municípios da UF.</summary>
    internal static Result<RestricaoValor>? LerFatosDaRestricao(
        LeitorEnvelope leitor, JsonObject bloco, string path, Func<IReadOnlyCollection<string>, Result<RestricaoValor>> montar)
    {
        leitor.ExigirChaves(bloco, path, "tipo", "codigosDosDados");
        IReadOnlyList<string> fatos = leitor.Textos(bloco, "codigosDosDados", path);
        return leitor.Falhou ? null : montar(fatos);
    }
}
