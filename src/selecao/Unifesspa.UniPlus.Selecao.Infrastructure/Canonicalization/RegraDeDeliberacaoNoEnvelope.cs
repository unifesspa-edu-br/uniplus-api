namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra de deliberação de uma banca no envelope: o mesmo bloco na banca da fase e na da etapa.
/// Ausente enquanto a banca não declara a regra, e nulo no envelope nesse caso.
/// </summary>
internal static class RegraDeDeliberacaoNoEnvelope
{
    private static readonly string[] Chaves =
        ["modo", "integrantes", "suplentes", "quorum", "criterio", "desempate", "encerramento", "semUnanimidade"];

    internal static JsonNode? Serializar(RegraDeDeliberacao? regra)
    {
        if (regra is null)
        {
            return null;
        }
        JsonObject objeto = CamposDaRegra(regra);
        ContextoDosRotulos.Acrescentar(objeto, "modo", regra.Modo);
        ContextoDosRotulos.Acrescentar(objeto, "criterio", regra.Criterio);
        ContextoDosRotulos.Acrescentar(objeto, "desempate", regra.Desempate);
        ContextoDosRotulos.Acrescentar(objeto, "encerramento", regra.Encerramento);
        ContextoDosRotulos.Acrescentar(objeto, "semUnanimidade", regra.SemUnanimidade);
        return objeto;
    }

    private static JsonObject CamposDaRegra(RegraDeDeliberacao regra) => new()
    {
        ["modo"] = regra.Modo.Codigo(),
        ["integrantes"] = regra.Integrantes,
        ["suplentes"] = regra.Suplentes,
        ["quorum"] = regra.Quorum,
        ["criterio"] = regra.Criterio?.Codigo(),
        ["desempate"] = regra.Desempate?.Codigo(),
        ["encerramento"] = regra.Encerramento?.Codigo(),
        ["semUnanimidade"] = regra.SemUnanimidade?.Codigo(),
    };

    internal static RegraDeDeliberacao? Ler(LeitorEnvelope leitor, JsonObject banca, string pathDaBanca)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(banca, "regraDeDeliberacao", pathDaBanca);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        DeclaracaoDaDeliberacao? declaracao = LerDeclaracao(leitor, bloco, $"{pathDaBanca}.regraDeDeliberacao");
        if (declaracao is null)
        {
            return null;
        }

        Result<RegraDeDeliberacao> regra = RegraDeDeliberacao.Criar(declaracao);
        return regra.IsFailure ? leitor.Propagar<RegraDeDeliberacao>(regra.Errors, $"{pathDaBanca}.regraDeDeliberacao") : regra.Value;
    }

    private static DeclaracaoDaDeliberacao? LerDeclaracao(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, Chaves);
        DeclaracaoDaDeliberacao declaracao = new(
            leitor.Texto(bloco, "modo", path),
            leitor.Inteiro(bloco, "integrantes", path),
            leitor.Inteiro(bloco, "suplentes", path),
            new(leitor.InteiroOpcional(bloco, "quorum", path),
            leitor.TextoOpcional(bloco, "criterio", path),
            leitor.TextoOpcional(bloco, "desempate", path),
            leitor.TextoOpcional(bloco, "encerramento", path),
            leitor.TextoOpcional(bloco, "semUnanimidade", path)));
        return leitor.Falhou ? null : declaracao;
    }
}
