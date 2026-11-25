namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Mapeamento da <see cref="RevisaoDasConcessoesDoPerfil"/> para a resposta: o resumo do perfil e a revisão com a lista.</summary>
public static class RevisaoDasConcessoesMapping
{
    private const string AguardandoDecisoes = "aguardando_decisoes";

    public static RevisaoDoPerfilDto ToResumo(RevisaoDasConcessoesDoPerfil revisao, DateTimeOffset agora, int pendencias)
    {
        ArgumentNullException.ThrowIfNull(revisao);

        return new RevisaoDoPerfilDto(revisao.Id, revisao.TerminaEm, Situacao(revisao, agora, pendencias));
    }

    public static RevisaoDasConcessoesDto ToDto(RevisaoDasConcessoesDoPerfil revisao, Perfil perfil, ContextoDaRevisao contexto, Identidade? leitor)
    {
        ArgumentNullException.ThrowIfNull(revisao);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(contexto);

        return new RevisaoDasConcessoesDto(
            revisao.Id,
            revisao.PerfilId,
            revisao.NumeroDaVersaoNova,
            perfil.Versoes.First(v => v.Id == revisao.VersaoAlvoId).Numero,
            revisao.AbertaEm, revisao.TerminaEm, revisao.EncerradaEm,
            Situacao(revisao, contexto.Agora, revisao.Pendencias(contexto.Concessoes, contexto.Agora)),
            revisao.Forma?.ToCodigo(),
            revisao.EnvioDaGestao is not null,
            Itens(revisao, perfil, contexto, leitor));
    }

    // Encerrada a revisão, a lista é a das linhas que ela registrou: a concessão que surge depois, de uma versão
    // posterior, não pertence a ela, ainda que seja elegível pelo critério de uma revisão aberta.
    private static List<ConcessaoDaRevisaoDto> Itens(RevisaoDasConcessoesDoPerfil revisao, Perfil perfil, ContextoDaRevisao contexto, Identidade? leitor) =>
    [
        .. contexto.Concessoes
            .Where(c => revisao.LinhaDe(c.Id) is not null || (!revisao.Encerrada && revisao.Elegivel(c, contexto.Agora)))
            .Select(c => Item(revisao, perfil, contexto, c, leitor)),
    ];

    private static string Situacao(RevisaoDasConcessoesDoPerfil revisao, DateTimeOffset agora, int pendencias) =>
        revisao.Encerrada ? "encerrada" : revisao.EstaAberta(agora, pendencias) ? "aberta" : "encerrando";

    private static ConcessaoDaRevisaoDto Item(
        RevisaoDasConcessoesDoPerfil revisao, Perfil perfil, ContextoDaRevisao contexto, Concessao concessao, Identidade? leitor)
    {
        MigracaoDaConcessao? linha = revisao.LinhaDe(concessao.Id);
        ComposicaoDoEnvio composicao = contexto.ComposicaoDe(concessao);

        return new ConcessaoDaRevisaoDto(
            concessao.Id,
            concessao.BeneficiarioNome,
            concessao.EscopoTipo.ToCodigo(), concessao.UnidadeId,
            concessao.ValidaAte,
            perfil.Versoes.FirstOrDefault(v => v.Id == concessao.VersaoId)?.Numero,
            composicao.Acrescimo,
            linha?.ExigeProtecaoDeDados ?? composicao.ExigeProtecaoDeDados,
            linha?.Situacao.Codigo() ?? AguardandoDecisoes,
            leitor is not null && concessao.Beneficiario == leitor);
    }
}
