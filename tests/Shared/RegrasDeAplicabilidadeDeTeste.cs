namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra de aplicabilidade mínima que a natureza da modalidade pede no cadastro: a ampla é
/// incondicional, a cota da lei exige a opção pelas cotas e a ação afirmativa a recusa.
/// </summary>
internal static class RegrasDeAplicabilidadeDeTeste
{
    public static PredicadoDnf DaNatureza(NaturezaLegalModalidade natureza) => natureza switch
    {
        NaturezaLegalModalidade.CotaReservada => Opcao(true),
        NaturezaLegalModalidade.AcaoAfirmativa => Opcao(false),
        _ => PredicadoDnf.SemClausula,
    };

    /// <summary>
    /// O que a opção pelas cotas cita — o egresso de escola pública e a opção pelas vagas de escola
    /// pública — passa a ser derivado sem condição, ao lado do que o processo já deriva: o teste que
    /// oferta cota da lei e não mede a inscrição não precisa montar as perguntas.
    /// </summary>
    public static void DerivarOQueAOpcaoPelasCotasCita(ProcessoSeletivo processo, PrecondicaoIfMatch? precondicao = null)
    {
        ArgumentNullException.ThrowIfNull(processo);
        Result definido = processo.DefinirRegrasDerivacao(
            [
                .. processo.RegrasDerivacao.Where(static r => !OpcaoPelasCotasDaLei.DependenciasDaOpcao.Contains(r.CodigoFato)),
                .. OpcaoPelasCotasDaLei.DependenciasDaOpcao.Select(static fato =>
                    ConfiguracaoDerivacaoFato.Criar(fato, [RegraDerivacaoConfigurada.Criar(0, null, null).Value!]).Value!),
            ],
            precondicao ?? PrecondicaoIfMatch.Ausente);
        if (definido.IsFailure)
        {
            throw new InvalidOperationException($"As derivações de teste não puderam ser definidas: {definido.Error!.Message}");
        }
    }

    private static PredicadoDnf Opcao(bool optou) => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(optou)).Value!)]).Value!;
}
