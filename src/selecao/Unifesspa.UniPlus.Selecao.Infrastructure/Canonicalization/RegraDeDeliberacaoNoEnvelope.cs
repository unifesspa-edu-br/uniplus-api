namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra de deliberação de uma banca no envelope: o mesmo bloco na banca da fase e na da etapa.
/// Ausente enquanto a banca não declara a regra, e nulo no envelope nesse caso.
/// </summary>
internal static class RegraDeDeliberacaoNoEnvelope
{
    private static readonly string[] Chaves =
        ["modo", "integrantes", "suplentes", "quorum", "criterio", "desempate", "encerramento", "semUnanimidade"];

    internal static JsonNode? Serializar(RegraDeDeliberacao? regra) => regra is null
        ? null
        : new JsonObject
        {
            ["modo"] = regra.Modo.ToString(),
            ["integrantes"] = regra.Integrantes,
            ["suplentes"] = regra.Suplentes,
            ["quorum"] = regra.Quorum,
            ["criterio"] = regra.Criterio?.ToString(),
            ["desempate"] = regra.Desempate?.ToString(),
            ["encerramento"] = regra.Encerramento?.ToString(),
            ["semUnanimidade"] = regra.SemUnanimidade?.ToString(),
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
        return regra.IsFailure ? leitor.Propagar<RegraDeDeliberacao>(regra.Error!) : regra.Value;
    }

    private static DeclaracaoDaDeliberacao? LerDeclaracao(LeitorEnvelope leitor, JsonObject bloco, string path)
    {
        leitor.ExigirChaves(bloco, path, Chaves);
        DeclaracaoDaDeliberacao declaracao = new(
            leitor.Enumeracao<ModoDeDeliberacao>(bloco, "modo", path),
            leitor.Inteiro(bloco, "integrantes", path),
            leitor.Inteiro(bloco, "suplentes", path),
            leitor.InteiroOpcional(bloco, "quorum", path),
            leitor.EnumeracaoOpcional<CriterioDeVotacao>(bloco, "criterio", path),
            leitor.EnumeracaoOpcional<DesempateDaVotacao>(bloco, "desempate", path),
            leitor.EnumeracaoOpcional<EncerramentoDaVotacao>(bloco, "encerramento", path),
            leitor.EnumeracaoOpcional<ResultadoSemUnanimidade>(bloco, "semUnanimidade", path));
        return leitor.Falhou ? null : declaracao;
    }
}
