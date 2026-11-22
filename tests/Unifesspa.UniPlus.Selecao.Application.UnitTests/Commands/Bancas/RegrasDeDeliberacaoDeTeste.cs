namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>As regras de deliberação que os testes de montagem declaram no payload.</summary>
internal static class RegrasDeDeliberacaoDeTeste
{
    /// <summary>Maioria simples, 3 titulares, quórum 2, a presidência desempata e encerra no quórum.</summary>
    public static RegraDeDeliberacaoInput Valida() => new()
    {
        Modo = ModoDeDeliberacao.ColegiadaComVotacao.Codigo(),
        Integrantes = 3,
        Suplentes = 1,
        Quorum = 2,
        Criterio = CriterioDeVotacao.MaioriaSimples.Codigo(),
        Desempate = DesempateDaVotacao.VotoDaPresidencia.Codigo(),
        Encerramento = EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(),
    };

    /// <summary>Sem integrantes e sem critério nem encerramento: viola três campos ao mesmo tempo.</summary>
    public static RegraDeDeliberacaoInput ComTresViolacoes() => new()
    {
        Modo = ModoDeDeliberacao.ColegiadaComVotacao.Codigo(),
        Integrantes = 0,
        Suplentes = 0,
    };

    /// <summary>Ato individual sem votação do recurso: decide a homologação ou o ato por um titular.</summary>
    public static RegraDeDeliberacaoInput AtoIndividual() => new()
    {
        Modo = ModoDeDeliberacao.AtoIndividual.Codigo(),
        Integrantes = 1,
        Suplentes = 0,
    };

    /// <summary>Ato individual com a votação do recurso inteira: 3 titulares, quórum 2, maioria simples, desempate da presidência.</summary>
    public static RegraDeDeliberacaoInput AtoIndividualComVotacaoDoRecurso() => new()
    {
        Modo = ModoDeDeliberacao.AtoIndividual.Codigo(),
        Integrantes = 3,
        Suplentes = 1,
        Quorum = 2,
        Criterio = CriterioDeVotacao.MaioriaSimples.Codigo(),
        Desempate = DesempateDaVotacao.VotoDaPresidencia.Codigo(),
        Encerramento = EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(),
    };

    /// <summary>Ato individual que declara só o quórum da votação do recurso, sem critério nem encerramento.</summary>
    public static RegraDeDeliberacaoInput AtoIndividualSoComOQuorum() => new()
    {
        Modo = ModoDeDeliberacao.AtoIndividual.Codigo(),
        Integrantes = 3,
        Suplentes = 1,
        Quorum = 2,
    };
}
