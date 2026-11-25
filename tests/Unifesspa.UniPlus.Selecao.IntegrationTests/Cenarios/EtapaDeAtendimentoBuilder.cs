namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Cenarios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class EtapaDeAtendimentoBuilder
{
    internal static ProcessoSeletivo CriarProcesso(IReadOnlyList<NivelDeAtendimento> niveis, IReadOnlyList<ItemDeAvaliacaoDeclarado> itens)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Processo {Guid.CreateVersion7()}", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas([CriarEtapa(niveis, itens)]).IsSuccess.Should().BeTrue();
        return processo;
    }

    internal static EtapaProcesso CriarEtapa(IReadOnlyList<NivelDeAtendimento> niveis, IReadOnlyList<ItemDeAvaliacaoDeclarado> itens)
    {
        Result<TipoEtapaSnapshot> tipo = TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ENTREVISTA", "Entrevista", true, true, false);
        tipo.IsSuccess.Should().BeTrue();
        Result<EtapaProcesso> etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa("Entrevista", CaraterEtapa.Classificatoria, tipo.Value!, 1, null),
            new DeclaracaoDaAvaliacaoDaEtapa(1m, null, 10m, null, FormaDeAvaliacao.Humana, null, niveis),
            itens);
        etapa.IsSuccess.Should().BeTrue();
        return etapa.Value!;
    }
    internal static void RedeclararItens(EtapaProcesso etapa, IReadOnlyList<ItemDeAvaliacaoDeclarado> itens)
    {
        IdentificacaoDaEtapa identificacao = new(etapa.Nome, etapa.Carater, etapa.TipoEtapa, etapa.Ordem, etapa.FaseCodigo);
        DeclaracaoDaAvaliacaoDaEtapa avaliacao = new(etapa.Peso, etapa.NotaMinima, etapa.NotaMaxima,
            etapa.PassoDoLancamento, etapa.FormaDeAvaliacao, etapa.RegraConsolidacao, etapa.EscalaDeAtendimento);
        etapa.AtualizarDados(identificacao, avaliacao, itens).IsSuccess.Should().BeTrue();
    }

}
