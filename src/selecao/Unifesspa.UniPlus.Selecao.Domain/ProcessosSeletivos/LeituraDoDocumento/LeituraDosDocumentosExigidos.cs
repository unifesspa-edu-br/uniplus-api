namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Lê as exigências de documentos, acumulando as recusas, e adota as que as fábricas do domínio aceitam. A ordem é a enviada: a
/// exigência recusada sai da lista, e as seguintes conservam a posição enviada, que as causas apontam. A chave repetida já foi
/// recusada pela forma; se chegar aqui, vale a primeira.
/// </summary>
internal static class LeituraDosDocumentosExigidos
{
    internal static DocumentosLidos Ler(
        IReadOnlyList<ExigenciaNoDocumento> exigencias, CadastrosCitadosNoDocumento cadastros, FasesLidas fases, EtapasLidas etapas)
    {
        ArgumentNullException.ThrowIfNull(exigencias);
        ArgumentNullException.ThrowIfNull(cadastros);
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        ExigenciaLida[] lidas = [.. exigencias.Select(exigencia => LeituraDeUmDocumentoExigido.Ler(exigencia, cadastros, fases, etapas))];
        ExigenciaLida[] adotadas = [.. lidas.Where(static lida => lida.Documento is not null)];

        return new(
            [.. adotadas.Select(static lida => (lida.Documento!, lida.PosicaoNoDocumento))],
            ChavesDasPartesLidas.PelaChave(adotadas.Select(static lida => (lida.Documento!.Chave.Valor, lida.Documento!))),
            ChavesDasPartesLidas.Recusadas(lidas.Where(static lida => lida.Documento is null && lida.Chave is not null).Select(static lida => lida.Chave!)),
            [.. lidas.SelectMany(PartesRecusadasDasExigencias.DaExigencia)]);
    }
}
