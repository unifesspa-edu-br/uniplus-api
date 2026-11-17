namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// As conferências que antecedem a gravação da Instituição, na ordem em que recusam: o payload inteiro
/// (validação sempre vence I/O e 404), depois o que depende do repositório. Na criação, a Instituição
/// já existir viva (ADR-0055) e, em ambas, o vínculo com a Unidade raiz, que deve ser uma reitoria viva
/// (UNI-REQ-0007). A Instituição só é montada depois de todas as recusas.
/// </summary>
public sealed class ConferenciaDoCadastroDaInstituicao
{
    private readonly IInstituicaoRepository _instituicoes;
    private readonly IUnidadeRepository _unidades;

    public ConferenciaDoCadastroDaInstituicao(IInstituicaoRepository instituicoes, IUnidadeRepository unidades)
    {
        ArgumentNullException.ThrowIfNull(instituicoes);
        ArgumentNullException.ThrowIfNull(unidades);

        _instituicoes = instituicoes;
        _unidades = unidades;
    }

    internal async Task<Result<Instituicao>> ConferirNovaAsync(
        CriarInstituicaoCommand command, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        (Result validacao, ReferenciaEnderecoGeo? endereco) = PrevalidacaoDaInstituicao.Executar(command, agora);
        if (validacao.IsFailure)
        {
            return Result<Instituicao>.ValidationFailure(validacao.Errors);
        }

        DomainError? recusa = await RecusaDaNovaAsync(command.UnidadeRaizId, cancellationToken).ConfigureAwait(false);
        return recusa is null
            ? Instituicao.Criar(
                command.Identificacao(), command.Cidade().Carimbada(CidadeDaSede.Nenhuma, agora), endereco, command.UnidadeRaizId)
            : Result<Instituicao>.Failure(recusa);
    }

    internal async Task<Result<Instituicao>> ConferirAtualizacaoAsync(
        AtualizarInstituicaoCommand command, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        (Result validacao, _) = PrevalidacaoDaInstituicao.Executar(command, agora);
        if (validacao.IsFailure)
        {
            return Result<Instituicao>.ValidationFailure(validacao.Errors);
        }

        Instituicao? instituicao = await _instituicoes.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = instituicao is null
            ? InstituicaoHandlerErros.NaoEncontrada()
            : await RecusaDoVinculoAsync(command.UnidadeRaizId, cancellationToken).ConfigureAwait(false);

        return recusa is null ? Result<Instituicao>.Success(instituicao!) : Result<Instituicao>.Failure(recusa);
    }

    /// <summary>
    /// A recusa do vínculo já lido: <see langword="null"/> quando a Unidade é uma reitoria viva.
    /// </summary>
    internal static DomainError? RecusaDoVinculo(Unidade? unidade)
    {
        if (unidade is null)
        {
            return new DomainError(
                InstituicaoErrorCodes.UnidadeRaizNaoEncontrada,
                "A Unidade informada como raiz não foi encontrada ou foi removida.");
        }

        return unidade.Tipo == TipoUnidade.Reitoria
            ? null
            : new DomainError(
                InstituicaoErrorCodes.UnidadeRaizNaoEhReitoria,
                "A Unidade informada como raiz da Instituição deve ser do tipo reitoria.");
    }

    private async Task<DomainError?> RecusaDaNovaAsync(Guid? unidadeRaizId, CancellationToken cancellationToken)
    {
        if (await _instituicoes.ExisteAlgumaVivaAsync(cancellationToken).ConfigureAwait(false))
        {
            return InstituicaoHandlerErros.JaExisteInstituicaoViva();
        }

        return await RecusaDoVinculoAsync(unidadeRaizId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DomainError?> RecusaDoVinculoAsync(Guid? unidadeRaizId, CancellationToken cancellationToken)
    {
        if (unidadeRaizId is not { } id)
        {
            return null;
        }

        Unidade? unidade = await _unidades.ObterPorIdParaLeituraAsync(id, cancellationToken).ConfigureAwait(false);
        return RecusaDoVinculo(unidade);
    }
}
