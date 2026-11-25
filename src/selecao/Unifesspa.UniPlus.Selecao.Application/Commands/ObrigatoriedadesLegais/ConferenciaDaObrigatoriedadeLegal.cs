namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As duas conferências que antecedem a gravação de uma obrigatoriedade legal, na ordem que o cliente precisa
/// para corrigir o pedido: primeiro os campos (regras do domínio, sem I/O, todas as violações juntas) e só depois
/// as referências a cadastros, que exigem consulta. Um campo em branco nunca chega a virar busca por texto vazio.
/// </summary>
public sealed class ConferenciaDaObrigatoriedadeLegal(
    ITipoProcessoReader tipoProcessoReader,
    ReferenciasDoPredicado referenciasDoPredicado)
{
    /// <summary>Confere os campos do conteúdo pelas regras do domínio, devolvendo todas as violações.</summary>
    public static Result ConferirCampos(IObrigatoriedadeLegalDoComando conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        return ObrigatoriedadeLegal.ValidarCampos(
            conteudo.TipoProcessoCodigo,
            CodigoLidoDoPedido.Ler<CategoriaObrigatoriedade>(conteudo.Categoria),
            conteudo.RegraCodigo,
            conteudo.Predicado,
            conteudo.ComoTextoNormativo(),
            conteudo.VigenciaInicio,
            conteudo.VigenciaFim);
    }

    /// <summary>
    /// Confere o tipo de processo e depois as referências do predicado, devolvendo o predicado conferido. Pressupõe campos já conferidos.
    /// </summary>
    public async Task<Result<PredicadoObrigatoriedade>> ConferirReferenciasAsync(
        IObrigatoriedadeLegalDoComando conteudo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        string tipoProcesso = conteudo.TipoProcessoCodigo!;
        if (!await TipoDeProcessoServeAsync(tipoProcesso, cancellationToken).ConfigureAwait(false))
        {
            return Result<PredicadoObrigatoriedade>.Failure(RecusasDaObrigatoriedadeLegal.TipoProcessoNaoEncontradoOuInativo());
        }

        return await referenciasDoPredicado
            .ConferirAsync(conteudo.Predicado!, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> TipoDeProcessoServeAsync(string codigo, CancellationToken cancellationToken) =>
        string.Equals(codigo, TodosOsTiposDeProcesso.Codigo, StringComparison.Ordinal)
        || await tipoProcessoReader.ObterAtivoPorCodigoAsync(codigo, cancellationToken).ConfigureAwait(false) is not null;
}
