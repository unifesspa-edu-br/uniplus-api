namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Cenarios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ProcessoComEtapaBuilder
{
    internal static ProcessoSeletivo Criar()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "Processo de exemplo", TipoProcessoSnapshot.Criar(Guid.NewGuid(), "SELECAO", "Seleção").Value!,
            OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de seleção", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            IdentificadorLegivel.Criar("processo-exemplo").Value);
        processo.DefinirEtapas([Etapa()]).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static EtapaProcesso Etapa()
    {
        TipoEtapaSnapshot tipo = TipoEtapaSnapshot.Criar(Guid.NewGuid(), "PROVA", "Prova", true, true, false).Value!;
        return EtapaProcesso.Criar(
            ChaveLocal.Criar("etapa_prova_2").Value!,
            new IdentificacaoDaEtapa("Prova", CaraterEtapa.Classificatoria, tipo, 1, null),
            new DeclaracaoDaAvaliacaoDaEtapa(1m, null, 10m, null, FormaDeAvaliacao.Objetiva, null, null)).Value!;
    }
}
