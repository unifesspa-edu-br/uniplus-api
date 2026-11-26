namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O que a criação e a edição do modelo têm em comum antes de gravar: ler o conteúdo e conferi-lo
/// contra o catálogo de fatos, de termos e de tipos de processo (<see cref="GravacaoDoModelo"/> grava).
/// </summary>
internal sealed class EscritaDoModelo(
    IFatoCandidatoRepository fatoRepository, ITermoConsentimentoReader termoReader, ITipoProcessoReader tipoProcessoReader)
{
    /// <summary>
    /// O conteúdo lido, as recusas da leitura e as recusas acumuladas da leitura e do catálogo (ADR-0125). O modelo de
    /// inscrição coleta o conjunto básico na seção reservada, como o formulário do processo: o item
    /// básico omitido entra como está gravado no modelo, e o enviado tem de ser igual a ele. O tipo
    /// de processo é conferido só quando muda: desativar o tipo recusa vínculo novo, e o modelo que
    /// já o usava continua com ele.
    /// </summary>
    public async Task<(ConteudoLido Conteudo, IReadOnlyList<FieldError> RecusasDaLeitura, CatalogoDoModelo Catalogo, List<FieldError> Erros)> LerEConferirAsync(
        ConteudoDoModeloInput? entrada,
        FinalidadeFormulario? finalidade,
        string? tipoProcessoCodigo,
        ModeloGravado gravado,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidato> fatos = await fatoRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        CatalogoDoModelo catalogo = CatalogoDoModelo.De(fatos);
        (ConteudoDoModeloInput? mesclada, List<FieldError> daSecao) = ConjuntoBasicoDoModelo.ComASecaoDosDadosBasicos(entrada, finalidade, gravado.Conteudo);
        LeituraDoConteudo leitura = new(catalogo.Formatos);
        ConteudoLido conteudo = leitura.Ler(mesclada);

        List<FieldError> erros = await ConferirNoCatalogoAsync(
            conteudo, catalogo, [.. daSecao, .. leitura.Recusas], gravado.Vinculos, cancellationToken).ConfigureAwait(false);
        if (await TipoDeProcessoNovoInexistenteAsync(tipoProcessoCodigo, gravado.TipoProcessoCodigo, cancellationToken).ConfigureAwait(false) is { } inexistente)
        {
            erros.Add(inexistente);
        }

        return (conteudo, leitura.Recusas, catalogo, erros);
    }

    private async Task<List<FieldError>> ConferirNoCatalogoAsync(
        ConteudoLido conteudo, CatalogoDoModelo catalogo, List<FieldError> daLeitura, VinculosDoModelo existentes, CancellationToken cancellationToken)
    {
        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await termoReader
            .ListarVersoesAsync([.. conteudo.Termos.Select(static t => t.Termo.VersaoId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        return [.. ConferenciaDoConteudoDoModelo.NoConteudo(
            daLeitura.Concat(ConferenciaDoModelo.Conferir(conteudo, catalogo, versoes.ToDictionary(static v => v.VersaoId), existentes)))];
    }

    private async Task<FieldError?> TipoDeProcessoNovoInexistenteAsync(
        string? informado, string? anterior, CancellationToken cancellationToken)
    {
        string? tipo = CodigoGravavelDoTipoDeProcesso(informado);
        if (tipo is null || string.Equals(tipo, anterior, StringComparison.Ordinal))
        {
            return null;
        }

        return await tipoProcessoReader.ObterAtivoPorCodigoAsync(tipo, cancellationToken).ConfigureAwait(false) is null
            ? new FieldError($"{DescritivoDoModeloInput.Caminho}.tipoProcessoCodigo", new DomainError(
                ModeloFormularioErrorCodes.TipoProcessoInexistente, "O tipo de processo não existe ou está desativado."))
            : null;
    }

    private static string? CodigoGravavelDoTipoDeProcesso(string? informado) =>
        informado is not null && informado.Length <= ModeloFormulario.TipoProcessoCodigoMaxLength && FormatoDoCodigo.TemFormatoValido(informado)
            ? informado : null;
}
