namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura dos procedimentos de recurso de uma janela do envelope. A chave é obrigatória, e a lista vazia é a janela
/// sem procedimento declarado. O que se confere é o que se prova sem estado vivo: o tipo de decisão do vocabulário, o
/// alvo de forma conhecida, a regra do rol de procedimentos e a coerência entre as declarações; que o lugar da
/// janela declare o tipo ou que o alvo exista é conferência da gravação e do portão, e o congelado não se
/// reinterpreta contra o que a fase ou a etapa declaram hoje.
/// </summary>
internal static class LeituraDosProcedimentosDeRecurso
{
    private static readonly string[] RolDeProcedimentos = [.. RegraProcedimentoRecursoCodigo.Rol];

    internal static ProcedimentosDeRecurso? Ler(LeitorEnvelope leitor, JsonObject pai, string path, CodigosDoProcedimentoDeRecurso codigos)
    {
        JsonArray array = leitor.Array(pai, "procedimentos", path);
        string pathLista = $"{path}.procedimentos";
        List<ProcedimentoDeRecursoDeclarado> declarados = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            declarados.Add(LerProcedimento(leitor, leitor.ItemObjeto(array, i, pathLista), $"{pathLista}[{i}]")!);
        }

        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<FieldError> incoerencias = ConferenciaDosProcedimentosDeRecurso.FaltasDeCoerencia(declarados, codigos, pathLista);
        return incoerencias.Count > 0
            ? leitor.Propagar<ProcedimentosDeRecurso?>(incoerencias[0].Error)
            : ProcedimentosDeRecurso.De(declarados);
    }

    private static ProcedimentoDeRecursoDeclarado? LerProcedimento(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "tipoDeDecisao", "alvo", "regra");
        string codigoDoTipo = leitor.TextoNaoVazio(item, "tipoDeDecisao", path);
        AlvoDoProcedimentoDeRecurso? alvo = LerAlvo(leitor, item, path);
        ReferenciaRegra regra = leitor.Regra(item, "regra", path, RolDeProcedimentos);
        if (leitor.Falhou)
        {
            return null;
        }

        TipoMotivoDeDecisao tipo = TipoMotivoDeDecisaoCodigo.FromCodigo(codigoDoTipo);
        return tipo == TipoMotivoDeDecisao.Nenhum
            ? leitor.Malformado<ProcedimentoDeRecursoDeclarado?>($"{path}.tipoDeDecisao", $"o tipo '{codigoDoTipo}' não pertence ao vocabulário de decisões.")
            : ProcedimentoDeRecursoDeclarado.De(tipo, alvo, regra);
    }

    private static AlvoDoProcedimentoDeRecurso? LerAlvo(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(item, "alvo", path);
        if (leitor.Falhou || bloco is null)
        {
            return null;
        }

        string pathAlvo = $"{path}.alvo";
        leitor.ExigirChaves(bloco, pathAlvo, "tipo", "valor");
        string tipo = leitor.TextoNaoVazio(bloco, "tipo", pathAlvo);
        string valor = leitor.TextoNaoVazio(bloco, "valor", pathAlvo);
        if (leitor.Falhou)
        {
            return null;
        }

        return tipo switch
        {
            SerializacaoDosProcedimentosDeRecurso.AlvoItemDeAvaliacao when Guid.TryParseExact(valor, "D", out Guid id) =>
                AlvoDoProcedimentoDeRecurso.DoItem(id),
            SerializacaoDosProcedimentosDeRecurso.AlvoAreaDoEnem => AlvoDoProcedimentoDeRecurso.DaArea(valor),
            _ => leitor.Malformado<AlvoDoProcedimentoDeRecurso?>(pathAlvo, "o alvo deve ser um item de avaliação, por identificador, ou uma área do ENEM, por código."),
        };
    }
}
