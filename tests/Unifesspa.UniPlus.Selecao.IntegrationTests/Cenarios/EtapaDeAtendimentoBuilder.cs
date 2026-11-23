namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Cenarios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class EtapaDeAtendimentoBuilder
{
    internal static ProcessoSeletivo CriarProcesso(IReadOnlyList<NivelDeAtendimento> niveis)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"Processo {Guid.CreateVersion7()}", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        processo.DefinirEtapas([CriarEtapa(niveis)]).IsSuccess.Should().BeTrue();
        return processo;
    }

    internal static EtapaProcesso CriarEtapa(IReadOnlyList<NivelDeAtendimento> niveis)
    {
        Result<TipoEtapaSnapshot> tipo = TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "ENTREVISTA", "Entrevista", true, true, false);
        tipo.IsSuccess.Should().BeTrue();
        Result<EtapaProcesso> etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa("Entrevista", CaraterEtapa.Classificatoria, tipo.Value!, 1, null),
            new DeclaracaoDaAvaliacaoDaEtapa(1m, null, 10m, null, FormaDeAvaliacao.Humana, null, niveis),
            [new(null, "Item", 10m)]);
        etapa.IsSuccess.Should().BeTrue();
        return etapa.Value!;
    }
}
