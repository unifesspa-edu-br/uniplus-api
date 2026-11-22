namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Enums;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

internal static class ProjecaoDaRegraDeDeliberacao
{
    internal static RegraDeDeliberacaoDto? De(RegraDeDeliberacao? regra) =>
        regra is null
            ? null
            : new RegraDeDeliberacaoDto
            {
                Modo = CodigoComRotulo<ModoDeDeliberacao>.De(regra.Modo),
                Integrantes = regra.Integrantes,
                Suplentes = regra.Suplentes,
                Quorum = regra.Quorum,
                Criterio = regra.Criterio is { } criterio ? CodigoComRotulo<CriterioDeVotacao>.De(criterio) : null,
                Desempate = regra.Desempate is { } desempate ? CodigoComRotulo<DesempateDaVotacao>.De(desempate) : null,
                Encerramento = regra.Encerramento is { } encerramento ? CodigoComRotulo<EncerramentoDaVotacao>.De(encerramento) : null,
                SemUnanimidade = regra.SemUnanimidade is { } resultado ? CodigoComRotulo<ResultadoSemUnanimidade>.De(resultado) : null,
            };
}
