namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Modelos resolvidos por posição e causas das citações não resolvidas.</summary>
public sealed record ResultadoDosModelosCitados
{
    private ResultadoDosModelosCitados(IReadOnlyDictionary<int, ModeloDaExigencia> modelos, IReadOnlyList<FieldError> causas)
    {
        Modelos = modelos;
        Causas = causas;
    }

    public IReadOnlyDictionary<int, ModeloDaExigencia> Modelos { get; }

    public IReadOnlyList<FieldError> Causas { get; }

    internal static ResultadoDosModelosCitados DasCitacoes(
        IReadOnlyList<(int Posicao, Guid Id)> citados, IReadOnlyDictionary<Guid, ModeloDeDocumento> encontrados)
    {
        var resolucoes = citados.Select(citado =>
            (citado.Posicao, Resultado: ModeloDaExigencia.DoCitado(citado.Id, encontrados))).ToList();
        Dictionary<int, ModeloDaExigencia> resolvidos = resolucoes
            .Where(static resolucao => resolucao.Resultado.IsSuccess)
            .ToDictionary(static resolucao => resolucao.Posicao, static resolucao => resolucao.Resultado.Value!);
        List<FieldError> causas = resolucoes
            .Where(static resolucao => resolucao.Resultado.IsFailure)
            .Select(static resolucao => new FieldError(
                ExigenciasDoDocumento.PonteiroDoModelo(resolucao.Posicao), resolucao.Resultado.Error!)).ToList();
        return new ResultadoDosModelosCitados(resolvidos, causas);
    }

    public void DescreverNo(JsonArray exigencias)
    {
        ArgumentNullException.ThrowIfNull(exigencias);
        foreach ((int posicao, ModeloDaExigencia modelo) in Modelos)
        {
            exigencias[posicao]!["modelo"] = DescricaoDoModelo(modelo);
        }
    }

    private static JsonObject DescricaoDoModelo(ModeloDaExigencia modelo) => new()
    {
        ["nomeArquivo"] = modelo.NomeArquivo,
        ["formato"] = ModeloDeDocumento.TokenDe(modelo.Formato),
        ["hashSha256"] = modelo.HashSha256,
    };
}
