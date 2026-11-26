namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quem guarda o conjunto básico de dados do candidato (UNI-REQ-0151) quando a inscrição tem mais de um
/// formulário: cada fato é coletado por um só formulário do processo, e o conjunto fica em um só dos
/// formulários da inscrição. A falta de dado básico nos formulários da inscrição é recusa da publicação, e não desta regra.
/// </summary>
public static class ConjuntoBasicoNaInscricao
{
    /// <summary>
    /// O formulário declarado guarda o conjunto básico quando é o formulário da inscrição que já tem algum dado
    /// básico; se nenhum tem, é o primeiro dos formulários da inscrição, contado o declarado, pela ordem do código.
    /// </summary>
    public static bool Guarda(ConfiguracaoDeclarada configuracao, FormularioDeclarado declarado)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(declarado);

        IReadOnlyList<FormularioDeclarado> daInscricao = FormulariosDaInscricao.De(
            [.. configuracao.Coleta.FormulariosDeclarados.Where(f => f.Codigo != declarado.Codigo), declarado],
            configuracao.Cronograma.CronogramaFases);
        if (daInscricao.All(f => f.Codigo != declarado.Codigo))
        {
            return false;
        }

        FormularioDeclarado guardiao = daInscricao.FirstOrDefault(f => TemDadoBasico(configuracao, f)) ?? daInscricao[0];
        return guardiao.Codigo == declarado.Codigo;
    }

    private static bool TemDadoBasico(ConfiguracaoDeclarada configuracao, FormularioDeclarado declarado) =>
        configuracao.Coleta.FormularioDe(declarado.Codigo) is { } formulario
        && configuracao.Coleta.Itens.Any(item => item.FormularioId == formulario.Id && ConjuntoBasicoDaInscricao.Fatos.Contains(item.FatoCodigo));
}
