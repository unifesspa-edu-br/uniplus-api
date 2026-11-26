namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Cronograma;
using Domain.ProcessosSeletivos.Formularios;
using Domain.Services;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// O formulário apresentado de um código a partir da configuração do processo — a congelada na
/// versão divulgada ou a viva do rascunho —: a apresentação sai das entidades e dos valores
/// selecionáveis, e as regras, do recorte da definição avaliável (ADR-0139). Uma projeção só, para o
/// público e para o rascunho servirem o mesmo formulário. O dia do envio se conta no fuso
/// institucional, o mesmo do período de inscrição.
/// </summary>
public static class ProjecaoDoFormularioApresentado
{
    /// <summary>Nulo quando o processo não tem o formulário do código.</summary>
    /// <param name="cronograma">As fases do processo, que dizem se a fase de cada formulário coleta inscrição.</param>
    public static FormularioApresentado? Projetar(
        CodigoDoFormulario codigo,
        DefinicaoAvaliavel avaliavel,
        ColetaDeclarada coleta,
        CronogramaDoProcesso cronograma,
        DateOnly? dataReferenciaFatos)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(avaliavel);
        ArgumentNullException.ThrowIfNull(coleta);
        ArgumentNullException.ThrowIfNull(cronograma);

        return coleta.FormularioDe(codigo) is { } formulario
            ? Formulario(formulario, avaliavel, coleta, cronograma, dataReferenciaFatos)
            : null;
    }

    private static Dictionary<Guid, DeclaracaoDoFormulario> DeclaracoesDosFormularios(ColetaDeclarada coleta, CronogramaDoProcesso cronograma) =>
        coleta.Formularios.ToDictionary(static f => f.Id, f => f.Declarado.Estrutura(cronograma));

    private static FormularioApresentado Formulario(
        FormularioProcesso formulario,
        DefinicaoAvaliavel avaliavel,
        ColetaDeclarada coleta,
        CronogramaDoProcesso cronograma,
        DateOnly? dataReferenciaFatos)
    {
        CamposApresentados campos = new(avaliavel.ValoresSelecionaveis, DeclaracoesDosFormularios(coleta, cronograma));
        RecorteDaFinalidade recorte = RecorteDaFinalidade.De(avaliavel.Definicao, formulario.Codigo.Valor, avaliavel.Ofertas);

        return new FormularioApresentado(
            formulario.Codigo.Valor,
            formulario.Titulo,
            [.. formulario.Paginas.OrderBy(static e => e.Ordem).Select(e => Secao(formulario.Codigo, e))],
            [.. coleta.TermosExigidos.Where(t => t.FormularioId == formulario.Id).OrderBy(static t => t.Ordem).Select(t => Termo(formulario.Codigo, t))],
            Coleta(formulario, coleta, campos),
            recorte.Regras,
            Pressupostos(formulario, coleta, campos, avaliavel, recorte, dataReferenciaFatos));
    }

    private static PressupostosDoFormulario Pressupostos(
        FormularioProcesso formulario,
        ColetaDeclarada coleta,
        CamposApresentados campos,
        DefinicaoAvaliavel avaliavel,
        RecorteDaFinalidade recorte,
        DateOnly? dataReferenciaFatos)
    {
        DescricaoDosPressupostos descricao = new(
            [.. coleta.Itens.Where(f => f.FormularioId != formulario.Id).Select(campos.Campo)],
            avaliavel.ApresentacaoDosPressupostos,
            avaliavel.Definicao.Agregados);

        return new PressupostosDoFormulario(descricao.Descrever(recorte.Pressupostos), dataReferenciaFatos, FusoInstitucional.ZoneId);
    }

    private static ColetaApresentada Coleta(FormularioProcesso formulario, ColetaDeclarada coleta, CamposApresentados campos) => new(
        [.. coleta.Itens.Where(f => f.FormularioId == formulario.Id).OrderBy(static f => f.Ordem).Select(campos.Campo)],
        [.. coleta.GruposColetados.Where(g => g.FormularioId == formulario.Id).OrderBy(static g => g.Ordem).Select(campos.Grupo)]);

    private static PaginaApresentada Secao(CodigoDoFormulario formulario, PaginaDoFormulario pagina) => new(
        pagina.Codigo,
        pagina.Tipo == TipoPaginaFormulario.Secao ? DefinicaoDoProcesso.CodigoDaPagina(formulario, pagina.Codigo) : null,
        pagina.Ordem,
        pagina.Tipo.Codigo(),
        pagina.Bloco?.Codigo(),
        new ApresentacaoDaPaginaDto(pagina.Titulo, pagina.Descricao, pagina.Aviso));

    private static TermoApresentado Termo(CodigoDoFormulario formulario, TermoExigidoFormulario termo) => new(
        termo.Codigo,
        DefinicaoDoProcesso.CodigoDoTermo(formulario, termo.Codigo),
        termo.Ordem,
        termo.TermoId,
        termo.VersaoId,
        new ConteudoDoTermoDto(termo.Nome, termo.Texto, termo.BaseLegal, CodigoComRotulo<FormaAceite>.De(termo.FormaAceite), termo.HashVersao));

    /// <summary>
    /// Como o campo se apresenta: os valores selecionáveis na ordem de apresentação quando é de seleção, e a ajuda
    /// que depende de o formulário do campo servir à fase de inscrição.
    /// </summary>
    private sealed record CamposApresentados(
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> Valores,
        IReadOnlyDictionary<Guid, DeclaracaoDoFormulario> DeclaracaoPorFormulario)
    {
        public GrupoApresentado Grupo(GrupoColetado grupo) => new(
            grupo.Codigo,
            grupo.Ordem,
            grupo.PaginaCodigo,
            grupo.Rotulo,
            new OcorrenciasDoGrupoDto(grupo.Minimo, grupo.Maximo, grupo.IncluiCandidato),
            [.. grupo.Subitens.OrderBy(static s => s.Ordem).Select(Campo)]);

        public CampoApresentado Campo(FatoColetado fato) => new(
            fato.FatoCodigo,
            fato.Ordem,
            fato.PaginaCodigo,
            new ApresentacaoDoCampoDto(
                fato.Rotulo,
                fato.TipoDeCampo.Codigo(),
                ConjuntoBasicoDaInscricao.AjudaDoCampo(DeclaracaoPorFormulario.GetValueOrDefault(fato.FormularioId.GetValueOrDefault()), fato.FatoCodigo, fato.Ajuda),
                fato.PedirConfirmacao),
            ConjuntoBasicoDaInscricao.FormatoDaResposta(fato.FatoCodigo, fato.Formato),
            Valores.GetValueOrDefault(fato.FatoCodigo) is { } doCampo
                ? [.. doCampo.OrderBy(static v => v.Ordem).ThenBy(static v => v.Codigo, StringComparer.Ordinal)
                    .Select(static v => new ValorSelecionavel(v.Codigo, v.Descricao, v.Ordem, v.Orientacao))]
                : null);
    }
}
