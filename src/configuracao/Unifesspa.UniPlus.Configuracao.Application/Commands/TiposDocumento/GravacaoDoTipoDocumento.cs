namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação do cadastro de tipos de documento: confere a categoria entre as vivas do cadastro e o código entre os
/// tipos vivos, e grava traduzindo as corridas em recusa. A categoria é código de outro cadastro, sem chave estrangeira:
/// quem responde pela existência é esta gravação, não o agregado nem o banco. A violação do índice único parcial do
/// código, entre a conferência e a gravação, responde como código já existente, com o descarte das alterações para o
/// Wolverine não gravar de novo fora da captura (ADR-0119).
/// </summary>
public sealed class GravacaoDoTipoDocumento(
    ITipoDocumentoRepository tipos,
    ICategoriaDocumentoRepository categorias,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<Guid>> IncluirAsync(TipoDocumento tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        Result cadastro = await ConferirCadastroAsync(tipo.Codigo, tipo.Categoria, anterior: null, cancellationToken)
            .ConfigureAwait(false);
        if (cadastro.IsFailure)
        {
            return Result<Guid>.ValidationFailure(cadastro.Errors);
        }

        await tipos.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);
        Result gravado = await GravarAsync(cancellationToken).ConfigureAwait(false);

        return gravado.IsFailure ? Result<Guid>.ValidationFailure(gravado.Errors) : Result<Guid>.Success(tipo.Id);
    }

    /// <summary>
    /// Recusa os campos antes de buscar o tipo: a validação vence o "não encontrado". A categoria só é conferida contra
    /// o cadastro quando muda, porque a referência é autônoma depois da escrita e exigir a categoria viva a cada edição
    /// travaria a manutenção de um tipo cuja categoria foi removida ou renomeada.
    /// </summary>
    public async Task<Result> EditarAsync(Guid id, DeclaracaoDoTipoDocumento declaracao, CancellationToken cancellationToken)
    {
        Result<CamposDoTipoDocumento> campos = TipoDocumento.ValidarDeclaracao(declaracao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        TipoDocumento? tipo = await tipos.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);

        return tipo is null
            ? Result.Failure(NaoEncontrado())
            : await AplicarEdicaoAsync(tipo, declaracao, campos.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> AplicarEdicaoAsync(
        TipoDocumento tipo, DeclaracaoDoTipoDocumento declaracao, CamposDoTipoDocumento campos, CancellationToken cancellationToken)
    {
        Result cadastro = await ConferirCadastroAsync(campos.Codigo, campos.Categoria, tipo, cancellationToken)
            .ConfigureAwait(false);
        if (cadastro.IsFailure)
        {
            return cadastro;
        }

        Result editado = tipo.Atualizar(declaracao);

        return editado.IsFailure ? editado : await GravarAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> ConferirCadastroAsync(
        CodigoTipoDocumento codigo, string categoria, TipoDocumento? anterior, CancellationToken cancellationToken)
    {
        bool categoriaMudou = anterior is null || !string.Equals(categoria, anterior.Categoria, StringComparison.Ordinal);
        if (categoriaMudou && !await categorias.CodigoExisteEntreVivosAsync(categoria, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.ValidationFailure([new("categoria", CategoriaNaoEncontrada(categoria))]);
        }

        bool codigoMudou = anterior is null || anterior.Codigo != codigo;
        if (codigoMudou && await tipos.CodigoExisteEntreVivosAsync(codigo, anterior?.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoJaExiste());
        }

        return Result.Success();
    }

    private async Task<Result> GravarAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await MutacaoDoTipo.SalvarAsync(unitOfWork, ConflitoDeConcorrencia(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(CodigoJaExiste());
        }
    }

    private static DomainError CodigoJaExiste() =>
        new(TipoDocumentoErrorCodes.CodigoJaExiste, "Já existe um tipo de documento vivo com o código informado.");

    private static DomainError CategoriaNaoEncontrada(string categoria) =>
        new(TipoDocumentoErrorCodes.CategoriaNaoEncontrada, $"Não existe categoria de documento viva com o código '{categoria}'.");

    private static DomainError NaoEncontrado() =>
        new(TipoDocumentoErrorCodes.NaoEncontrado, "Tipo de documento não encontrado.");

    private static DomainError ConflitoDeConcorrencia() =>
        new(TipoDocumentoErrorCodes.ConflitoDeConcorrencia, "O tipo de documento foi alterado concorrentemente. Tente novamente.");
}
