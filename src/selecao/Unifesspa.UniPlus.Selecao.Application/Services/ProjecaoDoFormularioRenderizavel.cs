namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Services;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formulário renderizável de uma finalidade a partir da configuração do processo — a congelada na
/// versão divulgada ou a viva do rascunho —: a apresentação sai das entidades e dos valores
/// selecionáveis, e as regras, do recorte da definição avaliável (ADR-0139). Uma projeção só, para o
/// público e para o rascunho servirem o mesmo formulário.
/// </summary>
public static class ProjecaoDoFormularioRenderizavel
{
    /// <summary>Nulo quando o processo não tem formulário da finalidade.</summary>
    public static FormularioRenderizavel? Projetar(
        FinalidadeFormulario finalidade,
        DefinicaoAvaliavel avaliavel,
        IReadOnlyCollection<FormularioProcesso> formularios,
        IReadOnlyCollection<FatoColetado> fatos,
        IReadOnlyCollection<GrupoColetado> grupos,
        IReadOnlyCollection<TermoExigidoFormulario> termos,
        DateOnly? dataReferenciaFatos)
    {
        ArgumentNullException.ThrowIfNull(avaliavel);
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(termos);

        if (formularios.FirstOrDefault(f => f.Finalidade == finalidade) is not { } formulario)
        {
            return null;
        }

        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores = avaliavel.ValoresSelecionaveis;

        return FormularioRenderizavel.Montar(
            EstruturaFormulario.ParaToken(finalidade),
            formulario.Titulo,
            [.. formulario.Etapas.OrderBy(static e => e.Ordem).Select(e => Secao(finalidade, e))],
            [.. termos.Where(t => t.Finalidade == finalidade).OrderBy(static t => t.Ordem).Select(t => Termo(finalidade, t))],
            [.. fatos.Where(f => f.Finalidade == finalidade).OrderBy(static f => f.Ordem).Select(f => Campo(f, valores))],
            [.. grupos.Where(g => g.Finalidade == finalidade).OrderBy(static g => g.Ordem).Select(g => Grupo(g, valores))],
            RecorteDaFinalidade.De(avaliavel.Definicao, DefinicaoDoProcesso.CodigoDaEtapa(finalidade, null), avaliavel.Ofertas),
            fatos.Where(f => f.Finalidade != finalidade).Select(f => Campo(f, valores)),
            avaliavel.Definicao.Agregados,
            dataReferenciaFatos);
    }

    private static SecaoRenderizavel Secao(FinalidadeFormulario finalidade, EtapaFormulario etapa) => new(
        etapa.Codigo,
        etapa.Tipo == TipoEtapaFormulario.Secao ? DefinicaoDoProcesso.CodigoDaEtapa(finalidade, etapa.Codigo) : null,
        etapa.Ordem,
        EstruturaFormulario.ParaToken(etapa.Tipo),
        EstruturaFormulario.ParaToken(etapa.Bloco),
        etapa.Titulo,
        etapa.Descricao,
        etapa.Aviso);

    private static TermoRenderizavel Termo(FinalidadeFormulario finalidade, TermoExigidoFormulario termo) => new(
        termo.Codigo,
        DefinicaoDoProcesso.CodigoDoTermo(finalidade, termo.Codigo),
        termo.Ordem,
        termo.TermoId,
        termo.VersaoId,
        termo.Nome,
        termo.Texto,
        termo.BaseLegal,
        termo.FormaAceite,
        termo.HashVersao);

    private static GrupoRenderizavel Grupo(
        GrupoColetado grupo, IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) => new(
        grupo.Codigo,
        grupo.Ordem,
        grupo.EtapaCodigo,
        grupo.Rotulo,
        grupo.Minimo,
        grupo.Maximo,
        grupo.IncluiCandidato,
        [.. grupo.Subitens.OrderBy(static s => s.Ordem).Select(s => Campo(s, valores))]);

    /// <summary>O campo, com os valores selecionáveis na ordem de apresentação quando é de seleção.</summary>
    private static CampoRenderizavel Campo(
        FatoColetado fato, IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) => new(
        fato.FatoCodigo,
        fato.Ordem,
        fato.Rotulo,
        fato.TipoRenderizacao.ToCodigo(),
        valores.GetValueOrDefault(fato.FatoCodigo) is { } doCampo
            ? [.. doCampo.OrderBy(static v => v.Ordem).ThenBy(static v => v.Codigo, StringComparer.Ordinal)
                .Select(static v => new ValorSelecionavel(v.Codigo, v.Descricao, v.Ordem))]
            : null,
        fato.EtapaCodigo,
        fato.Formato,
        fato.Ajuda,
        fato.PedirConfirmacao);
}
