namespace Unifesspa.UniPlus.Authorization.Projecao;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O núcleo do projetor (ADR-0153): depois de decidido o acesso ao resource, decide cada par (natureza, classificação)
/// pela permissão que o módulo declara, omite o que não foi permitido e registra só a decisão permitida. A negativa é
/// omissão silenciosa, porque quem perguntou foi o sistema e registrá-la produziria tentativas que ninguém fez.
/// </summary>
public sealed class ProjetorDeItens
{
    private readonly IVerificadorDeAcesso _verificador;
    private readonly IDecisoesDaRequisicao _requisicao;

    /// <summary>Cria o projetor.</summary>
    public ProjetorDeItens(IVerificadorDeAcesso verificador, IDecisoesDaRequisicao requisicao)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(requisicao);

        _verificador = verificador;
        _requisicao = requisicao;
    }

    /// <summary>
    /// Decide, sem registrar, a permissão de cada par pedido sobre o resource, com a sensibilidade do dado que a permissão
    /// libera. Os pares sem permissão no módulo e os negados ficam de fora; as decisões permitidas são guardadas na
    /// requisição para a serialização defensiva e são registradas por <see cref="RegistrarAsync"/> quando exercidas.
    /// </summary>
    public async Task<DecisoesDaProjecao> DecidirAsync(
        IPortaDePermissaoPorPar porta,
        ResourceContext resource,
        IEnumerable<(NaturezaDoItem Natureza, ClassificacaoDeProtecao Classificacao)> pares,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(porta);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(pares);

        Dictionary<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoDeAcesso> permitidas = [];
        foreach ((NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao) in pares
            .Select(static par => (par.Natureza, Classificacao: ClassificacoesDeProtecao.Resolvida(par.Classificacao)))
            .Distinct())
        {
            if (porta.Requisito(natureza, classificacao) is not { } requisito)
            {
                continue;
            }

            DecisaoDeAcesso decisao = await _verificador
                .DecidirSemRegistrarAsync(requisito, resource.ComSensibilidade(requisito.Sensibilidade), cancellationToken)
                .ConfigureAwait(false);
            if (decisao.Resultado == ResultadoDoAcesso.Permitido)
            {
                permitidas[(natureza, classificacao)] = decisao;
            }
        }

        DecisoesDaProjecao decisoes = new(permitidas);
        _requisicao.Guardar(decisoes);

        return decisoes;
    }

    /// <summary>
    /// Registra, numa só gravação, toda decisão permitida de classificação, inclusive a de dado ou arquivo público e
    /// interno: o registro diz "acesso autorizado à classe", e a regra de sensibilidade vale só para o que a borda
    /// registra por conta própria. A falha de gravação lança <see cref="RegistroDeAcessoNaoGravadoException"/> e a
    /// requisição é recusada.
    /// </summary>
    public Task RegistrarAsync(DecisoesDaProjecao decisoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decisoes);

        DecisaoDeAcesso[] aRegistrar = [.. decisoes.Permitidas];

        return aRegistrar.Length == 0 ? Task.CompletedTask : _verificador.RegistrarAsync(aRegistrar, cancellationToken);
    }
}
