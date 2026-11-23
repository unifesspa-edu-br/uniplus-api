namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioDoProcessoSeletivoEmRascunho
{
    internal static UnidadeAdministradoraSnapshot UnidadeAdministradoraCeps() =>
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!;

    internal static ProcessoSeletivo NovoProcesso() => NovoProcesso("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU);

    internal static ProcessoSeletivo NovoProcesso(
        string nome,
        TipoProcessoSnapshot tipo,
        OrigemCandidatos origem = OrigemCandidatos.InscricaoPropria,
        Guid? unidadeAdministradoraOrigemId = null) =>
        ProcessoSeletivo.Criar(
            nome, tipo, origem, unidadeAdministradoraOrigemId ?? Guid.NewGuid(),
            UnidadeAdministradoraCeps(), LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    internal static EtapaProcesso EtapaDeProva(string nome, CaraterEtapa carater, int? ordem, decimal? peso, decimal? notaMinima) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: nome, Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: notaMinima, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
}
