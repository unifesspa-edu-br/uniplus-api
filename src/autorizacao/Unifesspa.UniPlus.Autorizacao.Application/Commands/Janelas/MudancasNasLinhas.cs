namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que uma operação mudou nas linhas da migração: as concessões que migraram, foram recusadas, passaram a aguardar a
/// proteção de dados ou foram revogadas pelo fim da janela. A revogação pelo fim só conta a que o fim produziu: a feita
/// à mão antes do corte não entra.
/// </summary>
public sealed record MudancasNasLinhas(
    IReadOnlyList<MudancaNaLinha> Migradas,
    IReadOnlyList<MudancaNaLinha> Recusadas,
    IReadOnlyList<MudancaNaLinha> Aguardando,
    IReadOnlyList<MudancaNaLinha> RevogadasPeloFim)
{
    public static MudancasNasLinhas Entre(EstadoDaJanelaAntesDaOperacao antes, JanelaDeMigracao janela, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(contexto);

        List<MigracaoDaConcessao> alteradas = [.. janela.Migracoes.Where(migracao => !antes.Linhas.TryGetValue(migracao.ConcessaoId, out SituacaoDaMigracao situacaoAnterior) || situacaoAnterior != migracao.Situacao)];

        return new MudancasNasLinhas(
            Linhas(alteradas, SituacaoDaMigracao.Migrada, contexto, static _ => true),
            Linhas(alteradas, SituacaoDaMigracao.Recusada, contexto, static _ => true),
            Linhas(alteradas, SituacaoDaMigracao.AguardandoProtecaoDeDados, contexto, static _ => true),
            ApanharRevogadasPeloFim(alteradas, antes, janela, contexto));
    }

    private static List<MudancaNaLinha> ApanharRevogadasPeloFim(
        List<MigracaoDaConcessao> alteradas, EstadoDaJanelaAntesDaOperacao antes, JanelaDeMigracao janela, ContextoDaMigracao contexto) =>
        Linhas(alteradas, SituacaoDaMigracao.NaoMigrada, contexto, concessao => concessao.RevogadaPelaJanelaId == janela.Id && !antes.Revogadas.Contains(concessao.Id));

    private static List<MudancaNaLinha> Linhas(
        List<MigracaoDaConcessao> alteradas, SituacaoDaMigracao situacao, ContextoDaMigracao contexto, Func<Concessao, bool> concessaoValida) =>
        [.. alteradas
            .Where(migracao => migracao.Situacao == situacao)
            .Select(migracao => (Linha: migracao, Concessao: contexto.Concessao(migracao.ConcessaoId)))
            .Where(par => par.Concessao is not null && concessaoValida(par.Concessao))
            .Select(par => new MudancaNaLinha(par.Concessao!, par.Linha.Id, contexto.ComposicaoDe(par.Concessao!).Acrescimo))];
}
