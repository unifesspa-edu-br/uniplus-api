namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Carrega, rastreado para mutação, o tipo de documento ou o fato sob o circuito de aprovação. O item inexistente ou
/// removido responde o não encontrado do próprio cadastro.
/// </summary>
public sealed class ItensSobAprovacao
{
    private static readonly DomainError TipoNaoEncontrado = new(TipoDocumentoErrorCodes.NaoEncontrado, "Tipo de documento não encontrado.");
    private static readonly DomainError FatoNaoEncontrado = new(FatoCandidatoErrorCodes.NaoEncontrado, "Fato do candidato não encontrado.");

    private readonly ITipoDocumentoRepository _tipos;
    private readonly IFatoCandidatoRepository _fatos;

    public ItensSobAprovacao(ITipoDocumentoRepository tipos, IFatoCandidatoRepository fatos)
    {
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);

        _tipos = tipos;
        _fatos = fatos;
    }

    internal async Task<Result<IItemSobAprovacao>> ObterAsync(ItemDaProtecao item, CancellationToken cancellationToken)
    {
        if (item.Cadastro == CadastroDoCatalogo.Fato)
        {
            FatoCandidato? fato = await _fatos.ObterPorIdAsync(item.ItemId, cancellationToken).ConfigureAwait(false);
            return fato is null ? Result<IItemSobAprovacao>.Failure(FatoNaoEncontrado) : Result<IItemSobAprovacao>.Success(new FatoSobAprovacao(fato));
        }

        TipoDocumento? tipo = await _tipos.ObterPorIdAsync(item.ItemId, cancellationToken).ConfigureAwait(false);
        return tipo is null ? Result<IItemSobAprovacao>.Failure(TipoNaoEncontrado) : Result<IItemSobAprovacao>.Success(new TipoSobAprovacao(tipo));
    }
}
