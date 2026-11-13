namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed partial class ProcessoSeletivoRestaurarConfiguracaoTests
{
    internal static EtapaProcesso EtapaDaProvaCongelada(Guid id, int ordem) =>
        EtapaProcesso.Reidratar(id, "Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, null, ordem);

    internal static EtapaProcesso EtapaSemNotaCongelada(Guid id, int ordem) =>
        EtapaProcesso.Reidratar(id, "Entrevista", CaraterEtapa.Eliminatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ENTREVISTA", "Entrevista", admitePontuacao: false, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, null, null, ordem);

    private static Result RestaurarSemMudarOAgregado(GrafoConfiguracao grafo)
    {
        ProcessoSeletivo processo = ProcessoPublicado(TipoProcesso.PSIQ);
        Estado antes = Estado.De(processo);

        Result resultado = processo.RestaurarConfiguracaoCongelada(VersaoDo(processo), grafo);

        resultado.IsFailure.Should().BeTrue();
        Estado.De(processo).Should().BeEquivalentTo(antes, "a recusa das etapas não deixa o agregado meio-reposto");
        return resultado;
    }

    [Fact(DisplayName = "Restaurar etapas com a mesma ordem é recusado, como na gravação, e não altera o agregado")]
    public void Restaurar_OrdemDeEtapaDuplicada_Recusa()
    {
        GrafoConfiguracao invalido = Grafo(etapas: [EtapaDaProvaCongelada(EtapaCongelada, 1), EtapaDaProvaCongelada(EtapaOriginal, 1)]);

        Result resultado = RestaurarSemMudarOAgregado(invalido);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
    }

    [Fact(DisplayName = "Restaurar: a ordem duplicada vence a etapa de nota do ENEM sob classificação que não é ENEM")]
    public void Restaurar_OrdemDuplicada_VenceACoerenciaDaEtapaDoEnem()
    {
        GrafoConfiguracao invalido = Grafo(etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1), EtapaDaProvaCongelada(EtapaOriginal, 1)]);

        Result resultado = RestaurarSemMudarOAgregado(invalido);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
    }

    [Fact(DisplayName = "Restaurar etapas das quais nenhuma compõe a nota é recusado, como na gravação, e não altera o agregado")]
    public void Restaurar_NenhumaEtapaComponeNota_Recusa()
    {
        GrafoConfiguracao invalido = Grafo(etapas: [EtapaSemNotaCongelada(EtapaCongelada, 1)]);

        Result resultado = RestaurarSemMudarOAgregado(invalido);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }

    [Fact(DisplayName = "Restaurar etapa de nota do ENEM sob classificação que não é ENEM não altera o agregado")]
    public void Restaurar_EtapaNotaEnemSemClassificacaoEnem_NaoAlteraOAgregado()
    {
        GrafoConfiguracao invalido = Grafo(etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1)]);

        RestaurarSemMudarOAgregado(invalido);
    }

    [Fact(DisplayName = "Restaurar eliminação por ausência na etapa do ENEM não altera o agregado")]
    public void Restaurar_AusenciaNaEtapaDoEnem_NaoAlteraOAgregado()
    {
        GrafoConfiguracao invalido = Grafo(
            etapas: [EtapaNotaEnemCongelada(EtapaCongelada, 1)],
            classificacao: ClassificacaoEnemMediaPonderada([RegraEliminacao.Criar(
                Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, 'e'),
                new ArgsElimAusenciaNaEtapa(EtapaCongelada)).Value!]));

        RestaurarSemMudarOAgregado(invalido);
    }
}
