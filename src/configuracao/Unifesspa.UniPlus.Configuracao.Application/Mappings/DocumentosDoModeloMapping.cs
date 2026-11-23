namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// O conjunto de documentos do modelo nas duas formas: a de leitura da manutenção
/// (<see cref="ConjuntoDeDocumentosDoModeloDto"/>) e a de entrada da escrita, que o leitor entre módulos traz.
/// </summary>
internal static class DocumentosDoModeloMapping
{
    /// <summary>O conjunto na forma de leitura da manutenção.</summary>
    public static ConjuntoDeDocumentosDoModeloDto ParaLeitura(this ConjuntoDeDocumentosDoModelo conjunto) => new(
        [.. conjunto.DocumentosExigidos.Select(DocumentoParaLeitura)],
        [.. conjunto.GruposDeDocumentos.Select(GrupoParaLeitura)]);

    /// <summary>O conjunto na forma de entrada da escrita.</summary>
    public static ConjuntoDeDocumentosDoModeloInput ParaEntrada(this ConjuntoDeDocumentosDoModelo conjunto) => new(
        [.. conjunto.DocumentosExigidos.Select(DocumentoParaEntrada)],
        [.. conjunto.GruposDeDocumentos.Select(GrupoParaEntrada)]);

    private static GrupoDeDocumentosDoModeloDto GrupoParaLeitura(GrupoDeDocumentosDoModelo grupo) => new(
        grupo.QuantidadeMinima,
        grupo.Indeferimento is { } indeferimento
            ? new IndeferimentoDoGrupoDeDocumentosDoModeloDto(indeferimento.Consequencia, [.. indeferimento.BasesLegais.Select(BaseLegalParaLeitura)])
            : null,
        [.. grupo.Documentos.Select(DocumentoParaLeitura)]);

    private static GrupoDeDocumentosDoModeloInput GrupoParaEntrada(GrupoDeDocumentosDoModelo grupo) => new(
        grupo.QuantidadeMinima,
        grupo.Indeferimento is { } indeferimento
            ? new IndeferimentoDoGrupoDeDocumentosDoModeloInput(indeferimento.Consequencia, [.. indeferimento.BasesLegais.Select(BaseLegalParaEntrada)])
            : null,
        [.. grupo.Documentos.Select(DocumentoParaEntrada)]);

    private static DocumentoExigidoDoModeloDto DocumentoParaLeitura(DocumentoExigidoDoModelo documento) => new(
        documento.FaseCodigo, documento.TipoDocumentoCodigo, documento.Quando is { } quando ? LeituraDeRegras.ToDto(quando) : null,
        IndeferimentoParaLeitura(documento.Indeferimento), RequisitosParaLeitura(documento.Requisitos), PorOpcaoParaLeitura(documento.PorOpcao));

    private static IndeferimentoDoDocumentoDoModeloDto IndeferimentoParaLeitura(IndeferimentoDoDocumentoDoModelo indeferimento) =>
        new(indeferimento.Obrigatorio, indeferimento.Consequencia, [.. indeferimento.BasesLegais.Select(BaseLegalParaLeitura)]);

    private static BaseLegalDoModeloDto BaseLegalParaLeitura(BaseLegalDoModelo baseLegal) =>
        new(baseLegal.Referencia, baseLegal.Abrangencia, baseLegal.Situacao, baseLegal.Observacao);

    private static RequisitosDoDocumentoDoModeloDto RequisitosParaLeitura(RequisitosDoDocumentoDoModelo requisitos) =>
        new(PrazoParaLeitura(requisitos.PrazoDeEmissao), ValidadeParaLeitura(requisitos.Validade));

    private static PrazoDeEmissaoDoModeloDto? PrazoParaLeitura(PrazoDeEmissaoDoModelo? prazo) => prazo is null ? null : new(
        CodigoComRotulo<EscolhaDoPrazoDeEmissao>.De(prazo.Escolha),
        prazo.Valor,
        prazo.Unidade is { } unidade ? CodigoComRotulo<UnidadeDoPrazoDeEmissao>.De(unidade) : null,
        ReferenciaParaLeitura(prazo.Referencia),
        EfeitoParaLeitura(prazo.EfeitoForaDoPrazo));

    private static ValidadeDoDocumentoDoModeloDto? ValidadeParaLeitura(ValidadeDoDocumentoDoModelo? validade) => validade is null ? null : new(
        CodigoComRotulo<EscolhaDaValidade>.De(validade.Escolha),
        ReferenciaParaLeitura(validade.Referencia),
        EfeitoParaLeitura(validade.EfeitoDoVencimento));

    private static DataDeReferenciaDoDocumentoDoModeloDto? ReferenciaParaLeitura(DataDeReferenciaDoDocumentoDoModelo? referencia) =>
        referencia is null ? null : new(CodigoComRotulo<TipoDaDataDeReferencia>.De(referencia.Tipo), referencia.FaseCodigo, referencia.Data);

    private static CodigoComRotulo<EfeitoNaAnaliseDoDocumento>? EfeitoParaLeitura(EfeitoNaAnaliseDoDocumento? efeito) =>
        efeito is { } valor ? CodigoComRotulo<EfeitoNaAnaliseDoDocumento>.De(valor) : null;

    private static ExigenciaPorOpcaoDto? PorOpcaoParaLeitura(ExigenciaPorOpcao? porOpcao) =>
        porOpcao is null ? null : new ExigenciaPorOpcaoDto(porOpcao.FatoCodigo, [.. porOpcao.Exceto]);

    private static DocumentoExigidoDoModeloInput DocumentoParaEntrada(DocumentoExigidoDoModelo documento) => new(
        documento.FaseCodigo, documento.TipoDocumentoCodigo, EntradaDeRegras.ParaEntrada(documento.Quando),
        IndeferimentoParaEntrada(documento.Indeferimento), RequisitosParaEntrada(documento.Requisitos), PorOpcaoParaEntrada(documento.PorOpcao));

    private static IndeferimentoDoDocumentoDoModeloInput IndeferimentoParaEntrada(IndeferimentoDoDocumentoDoModelo indeferimento) =>
        new(indeferimento.Obrigatorio, indeferimento.Consequencia, [.. indeferimento.BasesLegais.Select(BaseLegalParaEntrada)]);

    private static BaseLegalDoModeloInput BaseLegalParaEntrada(BaseLegalDoModelo baseLegal) =>
        new(baseLegal.Referencia, baseLegal.Abrangencia, baseLegal.Situacao, baseLegal.Observacao);

    private static RequisitosDoDocumentoDoModeloInput RequisitosParaEntrada(RequisitosDoDocumentoDoModelo requisitos) =>
        new(PrazoParaEntrada(requisitos.PrazoDeEmissao), ValidadeParaEntrada(requisitos.Validade));

    private static PrazoDeEmissaoDoModeloInput? PrazoParaEntrada(PrazoDeEmissaoDoModelo? prazo) => prazo is null ? null : new(
        prazo.Escolha.Codigo(), prazo.Valor, prazo.Unidade?.Codigo(), ReferenciaParaEntrada(prazo.Referencia), prazo.EfeitoForaDoPrazo?.Codigo());

    private static ValidadeDoDocumentoDoModeloInput? ValidadeParaEntrada(ValidadeDoDocumentoDoModelo? validade) => validade is null ? null : new(
        validade.Escolha.Codigo(), ReferenciaParaEntrada(validade.Referencia), validade.EfeitoDoVencimento?.Codigo());

    private static DataDeReferenciaDoDocumentoDoModeloInput? ReferenciaParaEntrada(DataDeReferenciaDoDocumentoDoModelo? referencia) =>
        referencia is null ? null : new(referencia.Tipo.Codigo(), referencia.Data, referencia.FaseCodigo);

    private static ExigenciaPorOpcaoInput? PorOpcaoParaEntrada(ExigenciaPorOpcao? porOpcao) =>
        porOpcao is null ? null : new ExigenciaPorOpcaoInput(porOpcao.FatoCodigo, [.. porOpcao.Exceto]);
}
