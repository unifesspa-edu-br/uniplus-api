namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As unidades que o domínio aceita no prazo do recurso, as mesmas que a gravação confere: a de dias corridos existe no enum só para ser recusada, e
/// não é oferecida.
/// </summary>
public static class ListarUnidadesDoPrazoDeRecursoQueryHandler
{
    public static IReadOnlyList<CodigoComRotulo<UnidadePrazo>> Handle(ListarUnidadesDoPrazoDeRecursoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. PrazoDoRecurso.UnidadesDeclaraveis.Select(CodigoComRotulo<UnidadePrazo>.De)];
    }
}
