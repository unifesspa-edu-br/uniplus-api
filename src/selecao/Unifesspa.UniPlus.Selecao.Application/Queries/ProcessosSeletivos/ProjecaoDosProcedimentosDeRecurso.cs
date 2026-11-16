namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Projeta, para leitura, os procedimentos de recurso que uma janela declara, na ordem canônica.</summary>
internal static class ProjecaoDosProcedimentosDeRecurso
{
    internal static IReadOnlyList<ProcedimentoDeRecursoDto> De(ProcedimentosDeRecurso procedimentos) =>
        [.. procedimentos.Itens.Select(static p => new ProcedimentoDeRecursoDto(
            p.TipoDeDecisao.ToCodigo(),
            Alvo(p.Alvo),
            ProjecaoDoProcessoSeletivo.Referencia(p.Regra)))];

    private static AlvoDoProcedimentoDeRecursoDto? Alvo(AlvoDoProcedimentoDeRecurso? alvo) => alvo?.Tipo switch
    {
        TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao => new(Guid.ParseExact(alvo.Valor, "D"), null),
        TipoDeAlvoDoProcedimentoDeRecurso.AreaDoEnem => new(null, alvo.Valor),
        _ => null,
    };
}
