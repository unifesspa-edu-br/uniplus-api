namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>As regras de deliberação que os testes de montagem declaram no payload.</summary>
internal static class RegrasDeDeliberacaoDeTeste
{
    /// <summary>Maioria simples, 3 titulares, quórum 2, a presidência desempata e encerra no quórum.</summary>
    public static RegraDeDeliberacaoInput Valida() => new(
        ModoDeDeliberacao.ColegiadaComVotacao, Integrantes: 3, Suplentes: 1, Quorum: 2,
        CriterioDeVotacao.MaioriaSimples, DesempateDaVotacao.VotoDaPresidencia, EncerramentoDaVotacao.PresidenciaNoQuorum);

    /// <summary>Sem integrantes e sem critério nem encerramento: viola três campos ao mesmo tempo.</summary>
    public static RegraDeDeliberacaoInput ComTresViolacoes() => new(
        ModoDeDeliberacao.ColegiadaComVotacao, Integrantes: 0, Suplentes: 0);
}
