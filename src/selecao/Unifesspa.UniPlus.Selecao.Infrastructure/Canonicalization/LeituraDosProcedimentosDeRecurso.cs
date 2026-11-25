namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura dos procedimentos de recurso de uma fase ou de uma etapa do envelope. A chave é obrigatória, e a lista vazia é
/// a fase ou etapa sem procedimento declarado. O que se confere é o que se prova sem estado vivo: o tipo de decisão do vocabulário, o
/// alvo de forma conhecida, a regra do rol de procedimentos e a coerência entre as declarações; que o lugar que
/// declara o procedimento admita o tipo ou que o alvo exista é conferência da gravação e da verificação de publicação, e o congelado não se
/// reinterpreta contra o que a fase ou a etapa declaram hoje.
/// </summary>
internal static class LeituraDosProcedimentosDeRecurso
{
    private const string Procedimentos = "procedimentos";

    internal static ProcedimentosDeRecurso? Ler(LeitorEnvelope leitor, JsonObject pai, string path, CodigosDoProcedimentoDeRecurso codigos)
    {
        JsonArray array = leitor.Array(pai, Procedimentos, path);
        string pathLista = $"{path}.{Procedimentos}";
        List<ProcedimentoDeRecursoDeclarado>? declarados = LerCadaProcedimento(leitor, array, pathLista);
        if (declarados is null)
        {
            return null;
        }

        IReadOnlyList<FieldError> incoerencias = ConferenciaDosProcedimentosDeRecurso.FaltasDeCoerencia(declarados, codigos, Procedimentos);
        return incoerencias.Count > 0
            ? leitor.Propagar<ProcedimentosDeRecurso?>(incoerencias, path)
            : ProcedimentosDeRecurso.De(declarados);
    }

    private static List<ProcedimentoDeRecursoDeclarado>? LerCadaProcedimento(LeitorEnvelope leitor, JsonArray array, string pathLista)
    {
        List<ProcedimentoDeRecursoDeclarado> declarados = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            declarados.Add(LerProcedimento(leitor, leitor.ItemObjeto(array, i, pathLista), $"{pathLista}[{i}]")!);
        }

        return leitor.Falhou ? null : declarados;
    }

    private static ProcedimentoDeRecursoDeclarado? LerProcedimento(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "tipoDeDecisao", "alvo", "regra");
        string codigoDoTipo = leitor.TextoNaoVazio(item, "tipoDeDecisao", path);
        AlvoDoProcedimentoDeRecurso? alvo = LerAlvo(leitor, item, path);
        ReferenciaRegra regra = leitor.Regra<RegraDeProcedimentoDoRecurso>(item, "regra", path);
        if (leitor.Falhou)
        {
            return null;
        }

        return CatalogoDe<TipoMotivoDeDecisao>.TentarObter(codigoDoTipo, out TipoMotivoDeDecisao tipo)
            ? ProcedimentoDeRecursoDeclarado.De(tipo, alvo, regra)
            : leitor.Malformado<ProcedimentoDeRecursoDeclarado?>($"{path}.tipoDeDecisao", $"o tipo de decisão não pertence ao vocabulário de decisões. Tipos aceitos: {TipoDoMotivoInformado.RotulosAceitosEmTexto}.");
    }

    private static AlvoDoProcedimentoDeRecurso? LerAlvo(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(item, "alvo", path);
        return leitor.Falhou || bloco is null ? null : LerAlvoDoBloco(leitor, bloco, $"{path}.alvo");
    }

    private static AlvoDoProcedimentoDeRecurso? LerAlvoDoBloco(LeitorEnvelope leitor, JsonObject bloco, string pathAlvo)
    {
        leitor.ExigirChaves(bloco, pathAlvo, "tipo", "valor");
        string tipo = leitor.TextoNaoVazio(bloco, "tipo", pathAlvo);
        string valor = leitor.TextoNaoVazio(bloco, "valor", pathAlvo);
        return leitor.Falhou ? null : AlvoDoTipo(leitor, tipo, valor, pathAlvo);
    }

    private static AlvoDoProcedimentoDeRecurso? AlvoDoTipo(LeitorEnvelope leitor, string tipo, string valor, string pathAlvo)
    {
        if (CatalogoDe<TipoDeAlvoDoProcedimentoDeRecurso>.TentarObter(tipo, out TipoDeAlvoDoProcedimentoDeRecurso tipoDeAlvo))
        {
            if (tipoDeAlvo == TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao && Guid.TryParseExact(valor, "D", out Guid id))
            {
                return AlvoDoProcedimentoDeRecurso.DoItem(id);
            }
            if (tipoDeAlvo == TipoDeAlvoDoProcedimentoDeRecurso.AreaDoEnem)
            {
                return AlvoDoProcedimentoDeRecurso.DaArea(valor);
            }
        }

        return leitor.Malformado<AlvoDoProcedimentoDeRecurso?>(pathAlvo, "o alvo deve ser um item de avaliação, por identificador, ou uma área do ENEM, por código.");
    }
}
