namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As consequências declaradas nas bancas e as exigências analisadas em cada lugar.</summary>
internal static class ConsequenciasDasBancas
{
    internal static IEnumerable<FieldError> Acrescimo(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        BancasParaConsequencias.Ler(configuracao, cronograma, etapas).Where(par => !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, par.Caminho)).Where(par => CoerenciaDaConsequenciaDoDescumprimento.AcrescimoNaoOferecido(configuracao, par.Consequencia))
            .Select(par => CoerenciaDaConsequenciaDoDescumprimento.Acrescimo(par.Caminho, "A banca"));

    internal static IEnumerable<FieldError> SemModalidade(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        BancasComRetirada(configuracao, cronograma, etapas)
            .Where(par => CoerenciaDaConsequenciaDoDescumprimento.SemModalidade(configuracao, modalidade => par.Exigencias.Any(exigencia => exigencia.PodeAlcancarModalidade(modalidade, fatos))))
            .Select(par => CoerenciaDaConsequenciaDoDescumprimento.SemModalidade(par.Caminho));

    internal static IEnumerable<FieldError> SemAmpla(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        BancasComRetirada(configuracao, cronograma, etapas)
            .Where(par => CoerenciaDaConsequenciaDoDescumprimento.SemAmpla(configuracao, modalidade => par.Exigencias.Any(exigencia => exigencia.PodeAlcancarModalidade(modalidade, fatos))))
            .Select(par => CoerenciaDaConsequenciaDoDescumprimento.SemAmpla(par.Caminho));

    /// <summary>
    /// Se a leitura recusou alguma fase ou etapa inteira, ou a lista de bancas de alguma, e as bancas que a conferência
    /// não alcançou ficam sem julgar. A parte recusada em outra propriedade da fase ou da etapa não afeta as bancas.
    /// </summary>
    internal static bool SemJulgar(ConfiguracaoDeclarada configuracao) =>
        configuracao.Origem.PartesRecusadas.Any(static parte => parte.Caminho is "fases" or "etapas" || parte.Caminho.Contains(".bancas", StringComparison.Ordinal));

    private static IEnumerable<BancasParaConsequencias.BancaComExigencias> BancasComRetirada(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        BancasParaConsequencias.Ler(configuracao, cronograma, etapas)
            .Where(par => par.Consequencia == ConsequenciaDoDescumprimento.RetiraAModalidade
                && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, par.Caminho)
                && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, "distribuicaoVagas")
                && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, "documentosExigidos"));

}
