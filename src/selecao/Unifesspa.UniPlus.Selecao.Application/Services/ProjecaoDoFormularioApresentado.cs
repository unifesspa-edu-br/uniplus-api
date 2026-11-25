namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// O formulário apresentado de uma finalidade a partir da configuração do processo — a congelada na
/// versão divulgada ou a viva do rascunho —: a apresentação sai das entidades e dos valores
/// selecionáveis, e as regras, do recorte da definição avaliável (ADR-0139). Uma projeção só, para o
/// público e para o rascunho servirem o mesmo formulário. O dia do envio se conta no fuso
/// institucional, o mesmo do período de inscrição.
/// </summary>
public static class ProjecaoDoFormularioApresentado
{
    /// <summary>Nulo quando o processo não tem formulário da finalidade.</summary>
    public static FormularioApresentado? Projetar(
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

        return formularios.FirstOrDefault(f => f.Finalidade == finalidade) is { } formulario
            ? Formulario(finalidade, formulario, avaliavel, fatos, grupos, termos, dataReferenciaFatos)
            : null;
    }

    private static FormularioApresentado Formulario(
        FinalidadeFormulario finalidade,
        FormularioProcesso formulario,
        DefinicaoAvaliavel avaliavel,
        IReadOnlyCollection<FatoColetado> fatos,
        IReadOnlyCollection<GrupoColetado> grupos,
        IReadOnlyCollection<TermoExigidoFormulario> termos,
        DateOnly? dataReferenciaFatos)
    {
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores = avaliavel.ValoresSelecionaveis;
        RecorteDaFinalidade recorte = RecorteDaFinalidade.De(avaliavel.Definicao, DefinicaoDoProcesso.CodigoDaPagina(finalidade, null), avaliavel.Ofertas);

        return new FormularioApresentado(
            finalidade.Codigo(),
            formulario.Titulo,
            [.. formulario.Paginas.OrderBy(static e => e.Ordem).Select(e => Secao(finalidade, e))],
            [.. termos.Where(t => t.Finalidade == finalidade).OrderBy(static t => t.Ordem).Select(t => Termo(finalidade, t))],
            Coleta(finalidade, fatos, grupos, valores),
            recorte.Regras,
            Pressupostos(finalidade, fatos, avaliavel, recorte, dataReferenciaFatos));
    }

    private static PressupostosDoFormulario Pressupostos(
        FinalidadeFormulario finalidade,
        IReadOnlyCollection<FatoColetado> fatos,
        DefinicaoAvaliavel avaliavel,
        RecorteDaFinalidade recorte,
        DateOnly? dataReferenciaFatos)
    {
        DescricaoDosPressupostos descricao = new(
            [.. fatos.Where(f => f.Finalidade != finalidade).Select(f => Campo(f, avaliavel.ValoresSelecionaveis))],
            avaliavel.ApresentacaoDosPressupostos,
            avaliavel.Definicao.Agregados);

        return new PressupostosDoFormulario(descricao.Descrever(recorte.Pressupostos), dataReferenciaFatos, FusoInstitucional.ZoneId);
    }

    private static ColetaApresentada Coleta(
        FinalidadeFormulario finalidade,
        IReadOnlyCollection<FatoColetado> fatos,
        IReadOnlyCollection<GrupoColetado> grupos,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) => new(
        [.. fatos.Where(f => f.Finalidade == finalidade).OrderBy(static f => f.Ordem).Select(f => Campo(f, valores))],
        [.. grupos.Where(g => g.Finalidade == finalidade).OrderBy(static g => g.Ordem).Select(g => Grupo(g, valores))]);

    private static PaginaApresentada Secao(FinalidadeFormulario finalidade, PaginaDoFormulario pagina) => new(
        pagina.Codigo,
        pagina.Tipo == TipoPaginaFormulario.Secao ? DefinicaoDoProcesso.CodigoDaPagina(finalidade, pagina.Codigo) : null,
        pagina.Ordem,
        pagina.Tipo.Codigo(),
        pagina.Bloco?.Codigo(),
        new ApresentacaoDaPaginaDto(pagina.Titulo, pagina.Descricao, pagina.Aviso));

    private static TermoApresentado Termo(FinalidadeFormulario finalidade, TermoExigidoFormulario termo) => new(
        termo.Codigo,
        DefinicaoDoProcesso.CodigoDoTermo(finalidade, termo.Codigo),
        termo.Ordem,
        termo.TermoId,
        termo.VersaoId,
        new ConteudoDoTermoDto(termo.Nome, termo.Texto, termo.BaseLegal, CodigoComRotulo<FormaAceite>.De(termo.FormaAceite), termo.HashVersao));

    private static GrupoApresentado Grupo(
        GrupoColetado grupo, IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) => new(
        grupo.Codigo,
        grupo.Ordem,
        grupo.PaginaCodigo,
        grupo.Rotulo,
        new OcorrenciasDoGrupoDto(grupo.Minimo, grupo.Maximo, grupo.IncluiCandidato),
        [.. grupo.Subitens.OrderBy(static s => s.Ordem).Select(s => Campo(s, valores))]);

    /// <summary>O campo, com os valores selecionáveis na ordem de apresentação quando é de seleção.</summary>
    private static CampoApresentado Campo(
        FatoColetado fato, IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) => new(
        fato.FatoCodigo,
        fato.Ordem,
        fato.PaginaCodigo,
        new ApresentacaoDoCampoDto(
            fato.Rotulo,
            fato.TipoDeCampo.Codigo(),
            ConjuntoBasicoDaInscricao.AjudaDoCampo(fato.Finalidade!.Value, fato.FatoCodigo, fato.Ajuda),
            fato.PedirConfirmacao),
        ConjuntoBasicoDaInscricao.FormatoDaResposta(fato.FatoCodigo, fato.Formato),
        valores.GetValueOrDefault(fato.FatoCodigo) is { } doCampo
            ? [.. doCampo.OrderBy(static v => v.Ordem).ThenBy(static v => v.Codigo, StringComparer.Ordinal)
                .Select(static v => new ValorSelecionavel(v.Codigo, v.Descricao, v.Ordem, v.Orientacao))]
            : null);
}
