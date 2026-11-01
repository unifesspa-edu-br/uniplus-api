namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lê o que aguarda a proteção de dados para quem a aprova e para quem mantém os cadastros: o administrador
/// acompanha o que enviou, e a proteção de dados decide. Cada um vê o que a sua permissão alcança — quem só aprova vê os dois
/// cadastros, quem só mantém um cadastro vê esse.
/// </summary>
public static class ListarProtecaoDoCatalogoQueryHandler
{
    public static async Task<Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>> Handle(
        ListarProtecaoDoCatalogoQuery query,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);

        SituacaoDaAprovacao? situacao = null;
        if (query.Situacao is not null)
        {
            if (!SituacoesDaAprovacao.TryAnalisar(query.Situacao, out SituacaoDaAprovacao analisada) || analisada == SituacaoDaAprovacao.Aprovada)
            {
                return Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>.ValidationFailure([new FieldError("situacao", new DomainError(
                    ProtecaoDoCatalogoErrorCodes.SituacaoInvalida,
                    "A situação deve ser PENDENTE, SOLICITADA, AJUSTE_RECOMENDADO ou REPROVADA: o item aprovado não aguarda a proteção de dados."))]);
            }

            situacao = analisada;
        }

        AcessoAoCatalogoDeProtecao acesso = query.Acesso;
        if (!acesso.Algum)
        {
            return Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>.Failure(AcessoAoCatalogoDeProtecao.SemPermissao);
        }

        List<ItemDaProtecaoDoCatalogoDto> itens = [];
        if (acesso.Aprovar || acesso.ManterTipos)
        {
            itens.AddRange((await tipos.ListarSemProtecaoAprovadaAsync(cancellationToken).ConfigureAwait(false))
                .Select(static t => t.ToItemDaProtecao()));
        }

        if (acesso.Aprovar || acesso.ManterFatos)
        {
            itens.AddRange((await fatos.ListarSemProtecaoAprovadaAsync(cancellationToken).ConfigureAwait(false))
                .Select(static f => f.ToItemDaProtecao()));
        }

        return Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>.Success(
            [.. itens.Where(i => situacao is null || i.Aprovacao.Situacao == SituacoesDaAprovacao.ParaTokenCanonico(situacao.Value))]);
    }
}
