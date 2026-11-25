namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A configuração declarada de um processo em rascunho que só tem o que o teste declara, para testar uma regra sem montar
/// o processo conforme: o teste parte da configuração vazia e acrescenta só o que a regra lê.
/// </summary>
internal static class ConfiguracaoDeclaradaDeTeste
{
    internal static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS 2026 — SiSU",
        TiposDeProcessoDeTeste.Sisu,
        OrigemCandidatos.InscricaoPropria,
        Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
        IdentificadoresDeTeste.Novo());

    internal static ConfiguracaoDeclarada Vazia() => NovoProcesso().Declaracao();

    internal static ConfiguracaoDeclarada ComEtapas(params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(etapas).IsSuccess.Should().BeTrue();

        return processo.Declaracao();
    }

    internal static ConfiguracaoDeclarada ComOrigem(this ConfiguracaoDeclarada configuracao, OrigemNoDocumento origem) => new(
        configuracao.Identidade, configuracao.Cronograma, configuracao.Avaliacao, configuracao.Vagas,
        configuracao.Exigencias, configuracao.Coleta, configuracao.DivulgacaoETaxa)
    {
        Origem = origem,
    };
}
