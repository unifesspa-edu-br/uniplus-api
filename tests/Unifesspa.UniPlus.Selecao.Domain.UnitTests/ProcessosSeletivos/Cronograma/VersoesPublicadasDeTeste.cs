namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Montagem das versões publicadas, das fases e das etapas dos testes do início das fases, com instantes fixos.</summary>
internal static class VersoesPublicadasDeTeste
{
    internal static readonly DateTimeOffset Vigencia = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static readonly DateTimeOffset Abertura = new(2040, 3, 10, 0, 0, 0, TimeSpan.Zero);

    internal static readonly FatosDoInicioDasFases SemFatos = new([], [], [], [], []);

    internal static FasesDaVersaoPublicada Versao(
        FaseDaVersaoPublicada fase, DateTimeOffset? vigenteAPartirDe = null, DateTimeOffset? substituidaEm = null) =>
        new(Guid.CreateVersion7(), vigenteAPartirDe ?? Vigencia, substituidaEm, [fase]);

    internal static FaseDaVersaoPublicada UmaFase(
        Guid? origem = null, DateTimeOffset? inicio = null, bool coletaInscricao = false, params EtapaDaVersaoPublicada[] etapas) =>
        new(Guid.CreateVersion7(), origem ?? Guid.CreateVersion7(), "FASE", "Fase de teste publicada", null, new JanelaDeTempo(inicio, null), coletaInscricao, etapas);

    internal static EtapaDaVersaoPublicada UmaEtapa(DateTimeOffset? inicio = null) =>
        new(Guid.CreateVersion7(), "Etapa", new JanelaDeTempo(inicio, null));

    internal static InicioDasFases Inicio(FasesDaVersaoPublicada[] versoes, FatosDoInicioDasFases fatos) => new(versoes, fatos);

    internal static AtoPublicadoNoLugar AtoDaFase(Guid faseCanonicaOrigemId, DateTimeOffset instante) =>
        new(faseCanonicaOrigemId, null, instante);

    internal static AtoPublicadoNoLugar AtoDaEtapa(Guid etapaId, DateTimeOffset instante) =>
        new(null, etapaId, instante);
}
