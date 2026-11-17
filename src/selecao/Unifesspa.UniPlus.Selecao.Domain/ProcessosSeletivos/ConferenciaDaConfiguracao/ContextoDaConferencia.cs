namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que toda família lê para julgar: a configuração declarada, os cadastros e as declarações do cronograma e das etapas.</summary>
internal sealed record ContextoDaConferencia(
    ConfiguracaoDeclarada Configuracao, CadastrosDaConfiguracao Cadastros, CronogramaDoProcesso Cronograma, EtapasDoProcesso Etapas)
{
    internal static ContextoDaConferencia De(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros) =>
        new(
            configuracao,
            cadastros,
            CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases, configuracao.Origem),
            EtapasDoProcesso.Das(configuracao.Avaliacao.Etapas, configuracao.Origem));
}
