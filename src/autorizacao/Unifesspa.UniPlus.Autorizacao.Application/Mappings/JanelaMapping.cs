namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Mapeamento da <see cref="JanelaDeMigracao"/> para a resposta: o resumo do perfil e a janela com a lista.</summary>
public static class JanelaMapping
{
    private const string AguardandoLote = "aguardando_lote";

    public static JanelaDoPerfilDto ToResumo(JanelaDeMigracao janela, DateTimeOffset agora, int pendencias)
    {
        ArgumentNullException.ThrowIfNull(janela);

        return new JanelaDoPerfilDto(janela.Id, janela.Prazo, Situacao(janela, agora, pendencias));
    }

    public static JanelaDeMigracaoDto ToDto(JanelaDeMigracao janela, Perfil perfil, ContextoDaMigracao contexto, Identidade? leitor)
    {
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(contexto);

        return new JanelaDeMigracaoDto(
            janela.Id,
            janela.PerfilId,
            janela.NumeroDaVersaoNova,
            perfil.Versoes.First(v => v.Id == janela.VersaoAlvoId).Numero,
            janela.AbertaEm, janela.Prazo, janela.EncerradaEm,
            Situacao(janela, contexto.Agora, janela.Pendencias(contexto.Concessoes, contexto.Agora)),
            janela.Forma?.ToCodigo(),
            janela.LoteDaGestao is not null,
            Itens(janela, perfil, contexto, leitor));
    }

    // Encerrada a janela, a lista é a das linhas que ela registrou: a concessão que surge depois, de uma versão
    // posterior, não pertence a ela, ainda que seja elegível pelo critério de uma janela aberta.
    private static List<ItemDaJanelaDto> Itens(JanelaDeMigracao janela, Perfil perfil, ContextoDaMigracao contexto, Identidade? leitor) =>
    [
        .. contexto.Concessoes
            .Where(c => janela.LinhaDe(c.Id) is not null || (!janela.Encerrada && janela.Elegivel(c, contexto.Agora)))
            .Select(c => Item(janela, perfil, contexto, c, leitor)),
    ];

    private static string Situacao(JanelaDeMigracao janela, DateTimeOffset agora, int pendencias) =>
        janela.Encerrada ? "encerrada" : janela.EstaAberta(agora, pendencias) ? "aberta" : "encerrando";

    private static ItemDaJanelaDto Item(
        JanelaDeMigracao janela, Perfil perfil, ContextoDaMigracao contexto, Concessao concessao, Identidade? leitor)
    {
        MigracaoDaConcessao? linha = janela.LinhaDe(concessao.Id);
        ComposicaoDoLote composicao = contexto.ComposicaoDe(concessao);

        return new ItemDaJanelaDto(
            concessao.Id,
            concessao.BeneficiarioNome,
            concessao.EscopoTipo.ToCodigo(), concessao.UnidadeId,
            concessao.ValidaAte,
            perfil.Versoes.FirstOrDefault(v => v.Id == concessao.VersaoId)?.Numero,
            composicao.Acrescimo,
            linha?.ExigeProtecaoDeDados ?? composicao.ExigeProtecaoDeDados,
            linha?.Situacao.ToCodigo() ?? AguardandoLote,
            leitor is not null && concessao.Beneficiario == leitor);
    }
}
