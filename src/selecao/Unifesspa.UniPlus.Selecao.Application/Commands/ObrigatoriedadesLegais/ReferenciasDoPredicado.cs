namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Confere as referências a cadastros que os predicados de
/// <c>ObrigatoriedadeLegal</c> carregam por valor — código de tipo de etapa, de
/// modalidade e de tipo de documento —, devolvendo o predicado ou o
/// erro de domínio da primeira referência que não existe.
/// </summary>
/// <remarks>
/// <para>A validação vive aqui, e não no agregado, porque exige I/O: o domínio
/// valida forma, o handler resolve existência (ADR-0125). Criação e atualização
/// compartilham esta classe porque a regra é a mesma — uma referência aceita numa
/// e recusada na outra seria só um jeito de a regra inválida entrar pela porta dos
/// fundos.</para>
/// <para>Os campos dos predicados são não-anuláveis em compile-time apenas:
/// <c>System.Text.Json</c> não impõe NRT em runtime e a validação de fronteira só
/// garante que o predicado existe, sem descer aos campos do subtipo polimórfico.
/// Um <c>null</c> vindo do payload precisa virar 422 aqui, não
/// <c>NullReferenceException</c> mais adiante.</para>
/// </remarks>
public sealed class ReferenciasDoPredicado(
    ITipoEtapaReader tipoEtapaReader,
    IModalidadeReader modalidadeReader,
    ITipoDocumentoReader tipoDocumentoReader,
    ITipoDeficienciaReader tipoDeficienciaReader,
    IRegraCatalogoReader regraCatalogoReader)
{
    /// <summary>
    /// Devolve o predicado com todas as referências conferidas, ou a falha da
    /// primeira referência inexistente. Predicados sem referência a cadastro
    /// atravessam inalterados.
    /// </summary>
    /// <remarks>
    /// A forma vem primeiro: código em branco vira busca por string vazia, que o cadastro
    /// legitimamente não encontra — a recusa sairia como "código não existe" e esconderia
    /// de quem cadastra que o campo não foi preenchido.
    /// </remarks>
    public async Task<Result<PredicadoObrigatoriedade>> NormalizarEValidarAsync(
        PredicadoObrigatoriedade predicado,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        Result forma = ObrigatoriedadeLegal.ValidarFormaDoPredicado(predicado);

        return forma.IsFailure
            ? Result<PredicadoObrigatoriedade>.Failure(forma.Error!)
            : await NormalizarReferenciasAsync(predicado, cancellationToken).ConfigureAwait(false);
    }

    private Task<Result<PredicadoObrigatoriedade>> NormalizarReferenciasAsync(
        PredicadoObrigatoriedade predicado,
        CancellationToken cancellationToken) => predicado switch
        {
            EtapaObrigatoria etapa => NormalizarEtapaAsync(etapa, cancellationToken),
            DocumentoObrigatorioParaModalidade documento => NormalizarDocumentoParaModalidadeAsync(documento, cancellationToken),
            ModalidadesMinimas modalidades => NormalizarModalidadesMinimasAsync(modalidades, cancellationToken),
            AtendimentoDisponivel atendimento => NormalizarAtendimentoDisponivelAsync(atendimento, cancellationToken),
            DesempateDeveIncluir desempate => NormalizarDesempateDeveIncluirAsync(desempate, cancellationToken),
            _ => Task.FromResult(Result<PredicadoObrigatoriedade>.Success(predicado)),
        };

    internal static DomainError TipoEtapaNaoEncontradoOuInativo() => new(
        "ObrigatoriedadeLegal.TipoEtapaNaoEncontradoOuInativo",
        "Tipo de etapa não encontrado ou não está ativo.");

    internal static DomainError ModalidadeNaoEncontrada() => new(
        "ObrigatoriedadeLegal.ModalidadeNaoEncontrada",
        "Modalidade não encontrada entre as modalidades vivas do cadastro.");

    internal static DomainError TipoDocumentoNaoEncontrado() => new(
        "ObrigatoriedadeLegal.TipoDocumentoNaoEncontrado",
        "Tipo de documento não encontrado entre os tipos vivos do cadastro.");

    internal static DomainError TipoDeficienciaNaoEncontrada() => new(
        "ObrigatoriedadeLegal.TipoDeficienciaNaoEncontrada",
        "Tipo de deficiência não encontrado entre os tipos vivos do cadastro.");

    internal static DomainError CriterioDesempateNaoEncontrado() => new(
        "ObrigatoriedadeLegal.CriterioDesempateNaoEncontrado",
        "Critério de desempate não encontrado no catálogo de regras.");

    /// <summary>
    /// Confere cada código de tipo de deficiência exigido contra o cadastro vivo.
    /// </summary>
    /// <remarks>
    /// O predicado referencia o tipo pelo <b>código</b>, não pelo nome: o nome é rótulo
    /// editorial e muda sem aviso, e uma regra escrita com o rótulo antigo deixaria de casar
    /// depois de uma renomeação cosmética — reprovando processos conformes sem que nada no
    /// cadastro sinalizasse o motivo.
    /// </remarks>
    private async Task<Result<PredicadoObrigatoriedade>> NormalizarAtendimentoDisponivelAsync(
        AtendimentoDisponivel predicado,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TipoDeficienciaView> vivos = await tipoDeficienciaReader
            .ListarVivosAsync(cancellationToken).ConfigureAwait(false);
        List<string> necessidades = [.. predicado.Necessidades.Select(Normalizar)];

        return necessidades.Any(codigo => vivos.All(v => !string.Equals(v.Codigo, codigo, StringComparison.Ordinal)))
            ? Result<PredicadoObrigatoriedade>.Failure(TipoDeficienciaNaoEncontrada())
            : Result<PredicadoObrigatoriedade>.Success(predicado with { Necessidades = necessidades });
    }

    /// <summary>
    /// Confere o código do critério contra as regras de desempate do catálogo.
    /// </summary>
    /// <remarks>
    /// <para>Sem esta conferência, um código inexistente reprovava toda publicação com a
    /// mensagem "critério de desempate ausente" — que descreve o processo seletivo e
    /// manda o operador procurar o defeito no lugar errado, quando ele está na regra.</para>
    /// </remarks>
    private async Task<Result<PredicadoObrigatoriedade>> NormalizarDesempateDeveIncluirAsync(
        DesempateDeveIncluir predicado,
        CancellationToken cancellationToken)
    {
        string codigo = predicado.Criterio ?? string.Empty;
        IReadOnlyList<RegraCatalogo> regras = await regraCatalogoReader
            .ListarPorTipoAsync(TipoRegra.CriterioDesempate, cancellationToken).ConfigureAwait(false);

        return regras.Any(r => string.Equals(r.Codigo, codigo, StringComparison.Ordinal))
            ? Result<PredicadoObrigatoriedade>.Success(predicado with { Criterio = codigo })
            : Result<PredicadoObrigatoriedade>.Failure(CriterioDesempateNaoEncontrado());
    }

    private async Task<Result<PredicadoObrigatoriedade>> NormalizarEtapaAsync(
        EtapaObrigatoria predicado,
        CancellationToken cancellationToken)
    {
        string codigo = Normalizar(predicado.TipoEtapaCodigo);
        bool ativo = await tipoEtapaReader.ObterAtivoPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false) is not null;

        return ativo
            ? Result<PredicadoObrigatoriedade>.Success(predicado with { TipoEtapaCodigo = codigo })
            : Result<PredicadoObrigatoriedade>.Failure(TipoEtapaNaoEncontradoOuInativo());
    }

    private async Task<Result<PredicadoObrigatoriedade>> NormalizarDocumentoParaModalidadeAsync(
        DocumentoObrigatorioParaModalidade predicado,
        CancellationToken cancellationToken)
    {
        string modalidade = Normalizar(predicado.Modalidade);
        string tipoDocumento = predicado.TipoDocumento ?? string.Empty;

        if (!await ModalidadeEstaVivaAsync(modalidade, cancellationToken).ConfigureAwait(false))
        {
            return Result<PredicadoObrigatoriedade>.Failure(ModalidadeNaoEncontrada());
        }

        return await tipoDocumentoReader.ObterVivoPorCodigoAsync(tipoDocumento, cancellationToken).ConfigureAwait(false) is null
            ? Result<PredicadoObrigatoriedade>.Failure(TipoDocumentoNaoEncontrado())
            : Result<PredicadoObrigatoriedade>.Success(predicado with { Modalidade = modalidade, TipoDocumento = tipoDocumento });
    }

    /// <remarks>
    /// A lista inteira é conferida item a item: basta um código órfão para a
    /// exigência mínima nunca ser satisfeita pelo edital que a deveria cumprir.
    /// </remarks>
    private async Task<Result<PredicadoObrigatoriedade>> NormalizarModalidadesMinimasAsync(
        ModalidadesMinimas predicado,
        CancellationToken cancellationToken)
    {
        List<string> codigos = [.. predicado.Codigos.Select(Normalizar)];

        foreach (string codigo in codigos)
        {
            if (!await ModalidadeEstaVivaAsync(codigo, cancellationToken).ConfigureAwait(false))
            {
                return Result<PredicadoObrigatoriedade>.Failure(ModalidadeNaoEncontrada());
            }
        }

        return Result<PredicadoObrigatoriedade>.Success(predicado with { Codigos = codigos });
    }

    private async Task<bool> ModalidadeEstaVivaAsync(string codigo, CancellationToken cancellationToken) =>
        await modalidadeReader.ObterVivaPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false) is not null;

    private static string Normalizar(string? codigo) => codigo ?? string.Empty;

}
