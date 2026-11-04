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
            Result<RestricaoValor>? restricao = LerRestricao(leitor, leitor.ItemObjeto(array, i, $"{path}.restricoes"), $"{path}.restricoes[{i}]");
            if (leitor.Falhou || restricao is null)
            {
                return null;
            }

            if (restricao.IsFailure)
            {
                return leitor.Propagar<List<RestricaoValor>?>(restricao.Error!);
            }

            restricoes.Add(restricao.Value!);
        }

        return leitor.Falhou ? null : restricoes;
    }

    private static Result<RestricaoValor>? LerRestricao(LeitorEnvelope leitor, JsonObject bloco, string pathRestricao)
    {
        string tipo = leitor.TextoNaoVazio(bloco, "tipo", pathRestricao);
        if (leitor.Falhou)
        {
            return null;
        }

        return RestricaoValorJson.TipoDoToken(tipo) switch
        {
            TipoRestricaoValor.FaixaNumerica => LerFaixa(leitor, bloco, pathRestricao),
            TipoRestricaoValor.TamanhoTexto => LerTamanho(leitor, bloco, pathRestricao),
            TipoRestricaoValor.OpcoesPermitidas => LerOpcoesPermitidas(leitor, bloco, pathRestricao),
            TipoRestricaoValor.OpcoesDasRespostas => LerFatosDaRestricao(leitor, bloco, pathRestricao, RestricoesDeValor.DasRespostas),
            TipoRestricaoValor.MunicipiosDaUf => LerFatosDaRestricao(leitor, bloco, pathRestricao, RestricoesDeValor.DaUf),
            TipoRestricaoValor.FaixaDeData => LerFaixaDeData(leitor, bloco, pathRestricao),
            _ => leitor.Recusar<Result<RestricaoValor>?>($"'{pathRestricao}.tipo' não é um tipo de restrição de valor."),
        };
    }

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

        return minima is { IsFailure: true } || maxima is { IsFailure: true }
            ? Result<RestricaoValor>.Failure((minima?.Error ?? maxima?.Error)!)
            : RestricoesDeValor.FaixaDeData(minima?.Value, maxima?.Value);
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
        string ancora = leitor.TextoNaoVazio(limite, "ancora", pathLimite);
        if (leitor.Falhou)
        {
            return null;
        }

        switch (RestricaoValorJson.AncoraDoToken(ancora))
        {
            case AncoraDoLimiteDeData.DataFixa:
                leitor.ExigirChaves(limite, pathLimite, "ancora", "data");
                DateOnly data = leitor.Data(limite, "data", pathLimite);
                return leitor.Falhou ? null : RestricoesDeValor.Limite(ancora, data, anos: null, dias: null);
            case AncoraDoLimiteDeData.Envio:
                leitor.ExigirChaves(limite, pathLimite, "ancora", "anos", "dias");
                int anos = leitor.Inteiro(limite, "anos", pathLimite);
                int dias = leitor.Inteiro(limite, "dias", pathLimite);
                return leitor.Falhou ? null : RestricoesDeValor.Limite(ancora, data: null, anos, dias);
            default:
                return leitor.Propagar<Result<LimiteDeData>?>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado, $"'{pathLimite}.ancora' não é uma âncora de limite de data."));
        }
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
        List<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> lidas = [];
        for (int i = 0; i < entradas.Count && !leitor.Falhou; i++)
        {
            JsonObject entrada = leitor.ItemObjeto(entradas, i, $"{path}.entradas");
            string pathEntrada = $"{path}.entradas[{i}]";
            leitor.ExigirChaves(entrada, pathEntrada, "quando", "valores");
            IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> quando = LeituraDasCondicoes.LerDnf(leitor, entrada, "quando", pathEntrada);
            IReadOnlyList<string> valores = leitor.Textos(entrada, "valores", pathEntrada);
            if (leitor.Falhou)
            {
                return null;
            }

            Result<PredicadoDnf?> predicado = LeituraDasCondicoes.PredicadoOpcional(quando);
            if (predicado.IsFailure)
            {
                return Result<RestricaoValor>.Failure(predicado.Error!);
            }

            lidas.Add((predicado.Value, valores));
        }

        return leitor.Falhou ? null : RestricoesDeValor.Opcoes(lidas);
    }

    /// <summary>A restrição que cita fatos — as opções formadas pelas respostas ou os municípios da UF.</summary>
    internal static Result<RestricaoValor>? LerFatosDaRestricao(
        LeitorEnvelope leitor, JsonObject bloco, string path, Func<IReadOnlyCollection<string>, Result<RestricaoValor>> montar)
    {
        leitor.ExigirChaves(bloco, path, "tipo", "fatos");
        IReadOnlyList<string> fatos = leitor.Textos(bloco, "fatos", path);
        return leitor.Falhou ? null : montar(fatos);
    }
}
