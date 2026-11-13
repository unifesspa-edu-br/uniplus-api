namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.ValueObjects;

using DTOs;

internal static class ProjecaoDaRegraDeDeliberacao
{
    internal static RegraDeDeliberacaoDto? De(RegraDeDeliberacao? regra) =>
        regra is null
            ? null
            : new RegraDeDeliberacaoDto(
                regra.Modo,
                regra.Integrantes,
                regra.Suplentes,
                regra.Quorum,
                regra.Criterio,
                regra.Desempate,
                regra.Encerramento,
                regra.SemUnanimidade);
}
