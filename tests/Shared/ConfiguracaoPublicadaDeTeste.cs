namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Configurações congeladas montadas pelos testes a partir dos blocos do processo.</summary>
internal static class ConfiguracaoPublicadaDeTeste
{
    internal static LocalidadeRegente Localidade { get; } = LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!;

    internal static ConfiguracaoPublicada DoProcesso(ProcessoSeletivo processo)
    {
        ConfiguracaoDeclarada declarada = processo.Declaracao();
        return new(declarada.Cronograma, declarada.Avaliacao, declarada.Vagas, declarada.Exigencias,
            declarada.Coleta, declarada.DivulgacaoETaxa, declarada.Identidade.IdentificadorLegivel);
    }

    internal static ConfiguracaoPublicada ComVagas(ConfiguracaoPublicada configuracao, VagasDeclaradas vagas) => new(
        configuracao.Cronograma, configuracao.Avaliacao, vagas, configuracao.Exigencias,
        configuracao.Coleta, configuracao.DivulgacaoETaxa, configuracao.IdentificadorLegivel);

    internal static ConfiguracaoPublicada ComColeta(ConfiguracaoPublicada configuracao, ColetaDeclarada coleta) => new(
        configuracao.Cronograma, configuracao.Avaliacao, configuracao.Vagas, configuracao.Exigencias,
        coleta, configuracao.DivulgacaoETaxa, configuracao.IdentificadorLegivel);

    internal static IReadOnlyList<FatoColetado> CamposDaColeta(IEnumerable<FatoColetado> itens, IReadOnlyList<GrupoColetado> grupos) =>
        [.. itens, .. grupos.SelectMany(static g => g.Subitens)];
}
