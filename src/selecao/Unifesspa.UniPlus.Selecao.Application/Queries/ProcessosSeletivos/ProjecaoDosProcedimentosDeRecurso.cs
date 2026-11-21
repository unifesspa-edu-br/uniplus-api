namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Projeta, para leitura, os procedimentos de recurso que uma janela declara, na ordem canônica.</summary>
internal static class ProjecaoDosProcedimentosDeRecurso
{
    internal static IReadOnlyList<ProcedimentoDeRecursoDto> De(ProcedimentosDeRecurso procedimentos, OrigemDaNotaDaJanela origem) =>
        [.. procedimentos.Itens.Select(p => new ProcedimentoDeRecursoDto(
            p.TipoDeDecisao.Codigo(),
            Alvo(p.Alvo),
            ProjecaoDoProcessoSeletivo.Referencia(p.Regra),
            SugestaoDoProcedimentoDeRecurso.Para(p.TipoDeDecisao, p.Alvo, origem)))];

    private static AlvoDoProcedimentoDeRecursoDto? Alvo(AlvoDoProcedimentoDeRecurso? alvo) => alvo?.Tipo switch
    {
        TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao => new(Guid.ParseExact(alvo.Valor, "D"), null),
        TipoDeAlvoDoProcedimentoDeRecurso.AreaDoEnem => new(null, alvo.Valor),
        _ => null,
    };
}
