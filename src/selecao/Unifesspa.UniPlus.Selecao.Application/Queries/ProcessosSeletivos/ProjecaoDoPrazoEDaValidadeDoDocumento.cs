namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Projeta para o contrato REST o prazo de emissão e a validade de uma exigência de documento.</summary>
internal static class ProjecaoDoPrazoEDaValidadeDoDocumento
{
    internal static PrazoDeEmissaoDto? Projetar(PrazoDeEmissao? prazo) => prazo is null ? null : new(
        CodigoComRotulo<EscolhaDoPrazoDeEmissao>.De(prazo.Escolha),
        prazo.Valor,
        prazo.Unidade is { } unidade ? CodigoComRotulo<UnidadeDoPrazoDeEmissao>.De(unidade) : null,
        Projetar(prazo.Referencia),
        ProjetarEfeito(prazo.EfeitoForaDoPrazo));

    internal static ValidadeDoDocumentoDto? Projetar(ValidadeDoDocumento? validade) => validade is null ? null : new(
        CodigoComRotulo<EscolhaDaValidade>.De(validade.Escolha),
        Projetar(validade.Referencia),
        ProjetarEfeito(validade.EfeitoDoVencimento));

    private static DataDeReferenciaDoDocumentoDto? Projetar(DataDeReferenciaDoDocumento? referencia) => referencia is null ? null : new(
        CodigoComRotulo<TipoDaDataDeReferencia>.De(referencia.Tipo), referencia.Data, referencia.FaseId);

    private static CodigoComRotulo<EfeitoNaAnaliseDoDocumento>? ProjetarEfeito(EfeitoNaAnaliseDoDocumento? efeito) =>
        efeito is { } valor ? CodigoComRotulo<EfeitoNaAnaliseDoDocumento>.De(valor) : null;
}
