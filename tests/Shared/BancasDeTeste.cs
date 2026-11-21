namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As bancas e regras de deliberação que o processo publicável de teste declara.</summary>
internal static class BancasDeTeste
{
    private static readonly Guid OrigemDoTipoDeRecursos = new("0197d6f0-0000-7000-8000-00000000b4c0");

    /// <summary>Maioria simples, 3 titulares, quórum 2, a presidência desempata e encerra no quórum.</summary>
    public static RegraDeDeliberacao Regra() => Regra(integrantes: 3, quorum: 2);

    public static RegraDeDeliberacao Regra(int integrantes, int quorum) =>
        RegraDeDeliberacao.Criar(new DeclaracaoDaDeliberacao(
            ModoDeDeliberacao.ColegiadaComVotacao, integrantes, Suplentes: 0, quorum,
            CriterioDeVotacao.MaioriaSimples,
            integrantes > 1 ? DesempateDaVotacao.VotoDaPresidencia : null,
            EncerramentoDaVotacao.PresidenciaNoQuorum, SemUnanimidade: null)).Value!;

    /// <summary>A banca de análise de recursos da fase, com a regra de deliberação declarada.</summary>
    public static BancaRequerida DeRecursosDaFase() =>
        BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.DeRecursos(OrigemDoTipoDeRecursos), [], Regra());

    /// <summary>A banca de análise de recursos de 2ª instância da fase, com a regra de deliberação declarada.</summary>
    public static BancaRequerida DeSegundaInstanciaDaFase() =>
        BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(), [], Regra());

    /// <summary>A banca de análise de recursos da etapa, com a regra de deliberação declarada.</summary>
    public static BancaDaEtapa DeRecursosDaEtapa(RegraDeDeliberacao? regra = null) =>
        BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.DeRecursos(OrigemDoTipoDeRecursos), regra ?? Regra());

    /// <summary>A banca de homologação das inscrições da fase, com a regra de deliberação declarada.</summary>
    public static BancaRequerida DaHomologacaoDasInscricoesDaFase() => DaFase(TipoDeBanca.HomologacaoDasInscricoes);

    /// <summary>Uma banca da fase do tipo pedido, com a regra de deliberação declarada.</summary>
    public static BancaRequerida DaFase(TipoDeBanca tipo) => BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.De(tipo), [], Regra());

    /// <summary>Uma banca da etapa do tipo pedido, com a regra de deliberação declarada.</summary>
    public static BancaDaEtapa DaEtapa(TipoDeBanca tipo) => BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(tipo), Regra());
}
