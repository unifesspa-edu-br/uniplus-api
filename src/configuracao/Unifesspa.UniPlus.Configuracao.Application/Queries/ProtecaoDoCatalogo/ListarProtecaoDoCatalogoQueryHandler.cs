namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lê o que aguarda a proteção de dados para quem a aprova, para quem a consulta e para quem mantém os cadastros: o
/// administrador acompanha o que enviou, e a proteção de dados decide. Cada um vê o que a sua permissão alcança — quem
/// aprova ou consulta vê os dois cadastros, quem só mantém um cadastro vê esse. A leitura entra na trilha de acesso pela
/// permissão de classe mais alta que a autorizou, antes de a lista sair.
/// </summary>
public static class ListarProtecaoDoCatalogoQueryHandler
{
    public static async Task<Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>> Handle(
        ListarProtecaoDoCatalogoQuery query,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(fatos);

        if (!LerSituacao(query.Situacao, out SituacaoDaAprovacao? situacao))
        {
            return Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>.ValidationFailure([SituacaoInvalida()]);
        }

        Result<AcessoAoCatalogoDeProtecao> autorizado = await AutorizarAsync(verificador, cancellationToken).ConfigureAwait(false);
        return autorizado.IsFailure
            ? Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>.Failure(autorizado.Error!)
            : Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>.Success(await ListarAsync(autorizado.Value, tipos, fatos, situacao, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Quem pode ler a fila, com a leitura já registrada pela permissão de classe mais alta que a autorizou. Sem nenhuma
    /// permissão do circuito, a recusa já gravou as negativas.
    /// </summary>
    private static async Task<Result<AcessoAoCatalogoDeProtecao>> AutorizarAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        Result<AcessoAoCatalogoDeProtecao> decidido = await AcessoAoCatalogoDeProtecao.VerificarAsync(verificador, cancellationToken).ConfigureAwait(false);
        if (decidido.IsFailure || !decidido.Value.Algum)
        {
            return Result<AcessoAoCatalogoDeProtecao>.Failure(decidido.Error ?? AcessoAoCatalogoDeProtecao.SemPermissao);
        }

        await decidido.Value.RegistrarLeituraAsync(verificador, cancellationToken).ConfigureAwait(false);

        return decidido;
    }

    // O item aprovado não aguarda a proteção de dados: a situação aprovada não filtra a fila.
    private static bool LerSituacao(string? token, out SituacaoDaAprovacao? situacao)
    {
        situacao = null;
        if (token is null)
        {
            return true;
        }

        bool valida = SituacoesDaAprovacao.TryAnalisar(token, out SituacaoDaAprovacao analisada) && analisada != SituacaoDaAprovacao.Aprovada;
        situacao = valida ? analisada : null;

        return valida;
    }

    private static FieldError SituacaoInvalida() =>
        new("situacao", new DomainError(
            ProtecaoDoCatalogoErrorCodes.SituacaoInvalida,
            "A situação deve ser PENDENTE, SOLICITADA, AJUSTE_RECOMENDADO ou REPROVADA: o item aprovado não aguarda a proteção de dados."));

    // Quem aprova ou consulta vê os dois cadastros; quem só mantém um vê o dele. O filtro da situação vale para os dois.
    private static async Task<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>> ListarAsync(
        AcessoAoCatalogoDeProtecao acesso,
        ITipoDocumentoRepository tipos,
        IFatoCandidatoRepository fatos,
        SituacaoDaAprovacao? situacao,
        CancellationToken cancellationToken)
    {
        List<ItemDaProtecaoDoCatalogoDto> itens = [];
        if (acesso.VeOsDoisCadastros || acesso.ManterTipos)
        {
            itens.AddRange((await tipos.ListarSemProtecaoAprovadaAsync(cancellationToken).ConfigureAwait(false)).Select(static t => t.ToItemDaProtecao()));
        }

        if (acesso.VeOsDoisCadastros || acesso.ManterFatos)
        {
            itens.AddRange((await fatos.ListarSemProtecaoAprovadaAsync(cancellationToken).ConfigureAwait(false)).Select(static f => f.ToItemDaProtecao()));
        }

        return [.. itens.Where(i => NaSituacao(i, situacao))];
    }

    private static bool NaSituacao(ItemDaProtecaoDoCatalogoDto item, SituacaoDaAprovacao? situacao) =>
        situacao is null || item.Aprovacao.Situacao == SituacoesDaAprovacao.ParaTokenCanonico(situacao.Value);
}
