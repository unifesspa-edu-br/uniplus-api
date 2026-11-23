namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A etapa que o banco hidrata, com o identificador que ela já tinha, para os testes que a carregam no processo.</summary>
internal static class ApoioDeEtapaReidratada
{
    internal static EtapaProcesso DeProvaObjetiva(
        Guid id, string nome, int ordem, string? faseCodigo = null, decimal peso = 1m, decimal? notaMinima = null,
        CaraterEtapa carater = CaraterEtapa.Classificatoria) =>
        EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            id,
            new IdentificacaoDaEtapa(Nome: nome, Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: notaMinima, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);
}
