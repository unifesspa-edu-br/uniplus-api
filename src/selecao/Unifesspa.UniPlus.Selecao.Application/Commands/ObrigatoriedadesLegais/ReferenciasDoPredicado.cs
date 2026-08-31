namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using System.Text;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Normaliza e confere as referências a cadastros que os predicados de
/// <c>ObrigatoriedadeLegal</c> carregam por valor — código de tipo de etapa, de
/// modalidade e de tipo de documento —, devolvendo o predicado já normalizado ou o
/// erro de domínio da primeira referência que não existe.
/// </summary>
/// <remarks>
/// <para>A validação vive aqui, e não no agregado, porque exige I/O: o domínio
/// valida forma, o handler resolve existência (ADR-0125). Criação e atualização
/// compartilham esta classe porque a regra é a mesma — uma referência aceita numa
/// e recusada na outra seria só um jeito de a regra inválida entrar pela porta dos
/// fundos.</para>
/// <para>A normalização (<c>Trim</c> + NFC) acontece <b>antes de persistir</b>, não
/// só na consulta ao leitor: <c>AvaliadorConformidadeLegal</c> compara por
/// igualdade ordinal exata contra o código congelado no processo seletivo, então um
/// código gravado com espaço supérfluo ou em forma decomposta passaria no cadastro
/// e nunca mais casaria na avaliação — a cláusula legal viraria letra morta sem que
/// nada sinalizasse.</para>
/// <para>Os campos dos predicados são não-anuláveis em compile-time apenas:
/// <c>System.Text.Json</c> não impõe NRT em runtime e a validação de fronteira só
/// garante que o predicado existe, sem descer aos campos do subtipo polimórfico.
/// Um <c>null</c> vindo do payload precisa virar 422 aqui, não
/// <c>NullReferenceException</c> mais adiante.</para>
/// </remarks>
internal static class ReferenciasDoPredicado
{
    /// <summary>
    /// Devolve o predicado com todas as referências normalizadas, ou a falha da
    /// primeira referência inexistente. Predicados sem referência a cadastro
    /// atravessam inalterados.
    /// </summary>
    public static async Task<Result<PredicadoObrigatoriedade>> NormalizarEValidarAsync(
        PredicadoObrigatoriedade predicado,
        ITipoEtapaReader tipoEtapaReader,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        ArgumentNullException.ThrowIfNull(tipoEtapaReader);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);

        // Forma primeiro: código em branco vira busca por string vazia, que o cadastro
        // legitimamente não encontra — a recusa sairia como "código não existe" e
        // esconderia de quem cadastra que o campo não foi preenchido.
        Result forma = ObrigatoriedadeLegal.ValidarFormaDoPredicado(predicado);
        if (forma.IsFailure)
        {
            return Result<PredicadoObrigatoriedade>.Failure(forma.Error!);
        }

        return predicado switch
        {
            EtapaObrigatoria etapa =>
                await NormalizarEtapaAsync(etapa, tipoEtapaReader, cancellationToken).ConfigureAwait(false),

            DocumentoObrigatorioParaModalidade documento =>
                await NormalizarDocumentoParaModalidadeAsync(
                    documento, modalidadeReader, tipoDocumentoReader, cancellationToken).ConfigureAwait(false),

            ModalidadesMinimas modalidades =>
                await NormalizarModalidadesMinimasAsync(modalidades, modalidadeReader, cancellationToken).ConfigureAwait(false),

            _ => Result<PredicadoObrigatoriedade>.Success(predicado),
        };
    }

    internal static DomainError TipoEtapaNaoEncontradoOuInativo(string codigo) => new(
        "ObrigatoriedadeLegal.TipoEtapaNaoEncontradoOuInativo",
        $"Tipo de etapa '{codigo}' não encontrado ou não está ativo.");

    internal static DomainError ModalidadeNaoEncontrada(string codigo) => new(
        "ObrigatoriedadeLegal.ModalidadeNaoEncontrada",
        $"Modalidade '{codigo}' não encontrada entre as modalidades vivas do cadastro.");

    internal static DomainError TipoDocumentoNaoEncontrado(string codigo) => new(
        "ObrigatoriedadeLegal.TipoDocumentoNaoEncontrado",
        $"Tipo de documento '{codigo}' não encontrado entre os tipos vivos do cadastro.");

    private static async Task<Result<PredicadoObrigatoriedade>> NormalizarEtapaAsync(
        EtapaObrigatoria predicado,
        ITipoEtapaReader tipoEtapaReader,
        CancellationToken cancellationToken)
    {
        string codigo = Normalizar(predicado.TipoEtapaCodigo);

        bool ativo = await tipoEtapaReader
            .ObterAtivoPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false) is not null;

        return ativo
            ? Result<PredicadoObrigatoriedade>.Success(predicado with { TipoEtapaCodigo = codigo })
            : Result<PredicadoObrigatoriedade>.Failure(TipoEtapaNaoEncontradoOuInativo(codigo));
    }

    private static async Task<Result<PredicadoObrigatoriedade>> NormalizarDocumentoParaModalidadeAsync(
        DocumentoObrigatorioParaModalidade predicado,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        CancellationToken cancellationToken)
    {
        string modalidade = Normalizar(predicado.Modalidade);
        string tipoDocumento = NormalizarComoOCadastroDeTipoDocumentoGrava(predicado.TipoDocumento);

        if (!await ModalidadeEstaVivaAsync(modalidade, modalidadeReader, cancellationToken).ConfigureAwait(false))
        {
            return Result<PredicadoObrigatoriedade>.Failure(ModalidadeNaoEncontrada(modalidade));
        }

        if (await tipoDocumentoReader
                .ObterVivoPorCodigoAsync(tipoDocumento, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result<PredicadoObrigatoriedade>.Failure(TipoDocumentoNaoEncontrado(tipoDocumento));
        }

        return Result<PredicadoObrigatoriedade>.Success(
            predicado with { Modalidade = modalidade, TipoDocumento = tipoDocumento });
    }

    private static async Task<Result<PredicadoObrigatoriedade>> NormalizarModalidadesMinimasAsync(
        ModalidadesMinimas predicado,
        IModalidadeReader modalidadeReader,
        CancellationToken cancellationToken)
    {
        // Lista inteira normalizada e conferida item a item: basta um código órfão
        // para a exigência mínima nunca ser satisfeita pelo edital que a deveria
        // cumprir.
        List<string> codigos = new(predicado.Codigos?.Count ?? 0);

        foreach (string codigoBruto in predicado.Codigos ?? [])
        {
            string codigo = Normalizar(codigoBruto);
            if (!await ModalidadeEstaVivaAsync(codigo, modalidadeReader, cancellationToken).ConfigureAwait(false))
            {
                return Result<PredicadoObrigatoriedade>.Failure(ModalidadeNaoEncontrada(codigo));
            }

            codigos.Add(codigo);
        }

        return Result<PredicadoObrigatoriedade>.Success(predicado with { Codigos = codigos });
    }

    private static async Task<bool> ModalidadeEstaVivaAsync(
        string codigo,
        IModalidadeReader modalidadeReader,
        CancellationToken cancellationToken) =>
        await modalidadeReader.ObterVivaPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>
    /// Normalização de código cujo cadastro grava em NFC — tipo de etapa e modalidade.
    /// </summary>
    private static string Normalizar(string? codigo) =>
        (codigo?.Trim() ?? string.Empty).Normalize(NormalizationForm.FormC);

    /// <summary>
    /// Normalização do código de tipo de documento, que hoje é <b>só</b> <c>Trim</c>.
    /// </summary>
    /// <remarks>
    /// O cadastro de tipo de documento grava o código aparado e nada mais — sem NFC e sem
    /// formato fechado, ao contrário do tipo de etapa e da modalidade. Compor aqui o que a
    /// escrita não compõe faria a busca procurar um texto que o banco não guarda: um código
    /// gravado em forma decomposta seria dado como inexistente, recusando um tipo que existe
    /// e classificando como órfã uma regra que casa. Quando o código ganhar value object com
    /// formato fechado e os registros forem migrados, a normalização passa a valer dos dois
    /// lados e esta distinção desaparece.
    /// </remarks>
    private static string NormalizarComoOCadastroDeTipoDocumentoGrava(string? codigo) =>
        codigo?.Trim() ?? string.Empty;
}
