namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Guarda as decisões da projeção em <c>HttpContext.Items</c>, que acompanha a requisição por qualquer escopo de
/// injeção em que o handler rode. Sem requisição corrente não há decisão guardada, e nada é permitido.
/// </summary>
public sealed class DecisoesDaRequisicaoHttp : IDecisoesDaRequisicao
{
    private const string Chave = "uniplus.projecao.decisoes";

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Cria o repositório de decisões da requisição.</summary>
    public DecisoesDaRequisicaoHttp(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>As decisões guardadas na requisição, ou nenhuma permitida quando não há.</summary>
    internal static DecisoesDaProjecao Ler(HttpContext? http) =>
        http is not null && http.Items.TryGetValue(Chave, out object? guardadas) && guardadas is DecisoesDaProjecao decisoes
            ? decisoes
            : DecisoesDaProjecao.Vazia;

    /// <inheritdoc />
    public void Guardar(DecisoesDaProjecao decisoes)
    {
        ArgumentNullException.ThrowIfNull(decisoes);

        if (_httpContextAccessor.HttpContext is not { } http)
        {
            return;
        }

        http.Items[Chave] = decisoes.Unir(Ler(http));
    }

    /// <inheritdoc />
    public bool Permite(NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao) =>
        Ler(_httpContextAccessor.HttpContext).Permite(natureza, classificacao);
}
