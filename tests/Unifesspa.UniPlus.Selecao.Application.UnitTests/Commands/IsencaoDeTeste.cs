namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O processo que passa a cobrar a taxa com fundamento de isenção, sem escolher nenhum motivo de decisão.</summary>
internal static class IsencaoDeTeste
{
    /// <summary>
    /// Sob a sessão de retificação, ou antes de publicar, pela edição; no processo publicado sem sessão, direto no
    /// estado, porque o processo publicado não se edita.
    /// </summary>
    public static void HabilitarSemMotivos(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        ConfiguracaoTaxaInscricao taxa = ConfiguracaoTaxaInscricao.Criar(true, 100m, [FundamentoIsencaoCodigo.CadastroUnico]).Value!;
        if (processo.Status == StatusProcesso.Publicado && processo.Rascunho is null)
        {
            typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.ConfiguracaoTaxaInscricao))!.SetValue(processo, taxa);
            return;
        }

        processo.DefinirTaxaInscricao(taxa, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
    }
}
