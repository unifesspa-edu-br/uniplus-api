namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Cenarios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ConfiguracaoPublicavelBuilder
{
    internal static EntradaCanonicalizacao Criar(EntradaCanonicalizacao entrada, string nome)
    {
        ProcessoSeletivo original = entrada.Processo;
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, original.TipoProcesso, original.OrigemCandidatos, original.UnidadeAdministradoraOrigemId,
            original.UnidadeAdministradora, original.Localidade, original.IdentificadorLegivel!.Value);
        processo.DefinirEtapas([.. original.Etapas]).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([.. original.CronogramaFases], []).IsSuccess.Should().BeTrue();
        processo.DefinirAtendimentoEspecializado(original.AtendimentoEspecializado!).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([.. original.DistribuicaoVagas], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(original.Classificacao!).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeCurso(original.NumeroDeOpcoesDeCurso!.Value).IsSuccess.Should().BeTrue();
        return entrada with { Processo = processo };
    }
}
