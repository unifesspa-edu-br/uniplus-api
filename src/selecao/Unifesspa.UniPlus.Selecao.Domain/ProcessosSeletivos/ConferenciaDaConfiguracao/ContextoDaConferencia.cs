namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que toda família lê para julgar: o estado da configuração, os cadastros e as declarações do cronograma e das etapas.</summary>
internal sealed record ContextoDaConferencia(
    EstadoDaConfiguracao Estado, CadastrosDaConfiguracao Cadastros, CronogramaDoProcesso Cronograma, EtapasDoProcesso Etapas)
{
    internal static ContextoDaConferencia De(EstadoDaConfiguracao estado, CadastrosDaConfiguracao cadastros) =>
        new(estado, cadastros, CronogramaDoProcesso.Do(estado.CronogramaFases), EtapasDoProcesso.Das(estado.Etapas));
}
