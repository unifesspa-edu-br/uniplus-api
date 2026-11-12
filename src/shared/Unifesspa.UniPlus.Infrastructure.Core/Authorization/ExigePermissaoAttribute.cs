namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Exige uma permissão do catálogo antes do <i>model binding</i> (ADR-0151). Preserva
/// os três desfechos da borda: permitido segue; negado responde <c>403</c>; identidade
/// incompleta responde <c>401</c>.
/// </summary>
/// <typeparam name="TPermissao">Enum de permissões do módulo.</typeparam>
/// <remarks>
/// Roda como filtro de autorização, que precede o <i>binding</i> — um filtro de ação
/// só roda depois dele, e uma rejeição de acesso não pode depender de o corpo ser
/// válido. O C# não admite parâmetro de atributo do tipo <typeparamref name="TPermissao"/>
/// (CS0181), então o construtor recebe o valor como <see cref="Enum"/>; o tipo é
/// conferido ao construir o atributo, e a conferência de todos os usos é de um teste.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExigePermissaoAttribute<TPermissao> : Attribute, IAsyncAuthorizationFilter, IExigeAutorizacaoAlemDoLogin
    where TPermissao : struct, Enum
{
    private readonly string? _resourceTipo;

    /// <summary>Cria a exigência.</summary>
    /// <param name="permissao">Permissão exigida, do enum <typeparamref name="TPermissao"/>.</param>
    public ExigePermissaoAttribute(object permissao)
    {
        Definicao = Obter(permissao, nameof(permissao));
    }

    /// <summary>
    /// Tipo do resource que a decisão enxerga, quando a concessão pode ser restrita
    /// por tipo. Omitido, o alvo é a própria operação.
    /// </summary>
    public string? ResourceTipo
    {
        get => _resourceTipo;
        init
        {
            if (value is not null)
            {
                _ = ConstruirResource(value, Definicao);
            }

            _resourceTipo = value;
        }
    }

    /// <summary>
    /// Permissão alternativa, do mesmo enum, que basta quando o solicitante a tem em ao menos
    /// uma unidade. Serve à leitura de apoio de quem configura algo na sua unidade e consulta um
    /// cadastro institucional, sem receber a escrita institucional. Não se combina com
    /// <see cref="OuPermissao"/>.
    /// </summary>
    public object? OuEmAlgumaUnidade
    {
        get => AlternativaEmUnidade?.Codigo;
        init
        {
            ExigirUmaSoAlternativa();
            AlternativaEmUnidade = Obter(value, nameof(value));
        }
    }

    /// <summary>
    /// Permissão alternativa, do mesmo enum, avaliada sobre o mesmo resource quando a principal nega. Serve à consulta
    /// de quem lê sem decidir, como o Encarregado de dados, na rota que a permissão de administrar também serve. O
    /// registro grava a que autorizou ou, se nenhuma autorizou, as duas negativas. Não se combina com
    /// <see cref="OuEmAlgumaUnidade"/>.
    /// </summary>
    public object? OuPermissao
    {
        get => AlternativaNoResource?.Codigo;
        init
        {
            ExigirUmaSoAlternativa();
            AlternativaNoResource = Obter(value, nameof(value));
        }
    }

    /// <summary>Definição da permissão exigida.</summary>
    internal DefinicaoDePermissao Definicao { get; }

    private DefinicaoDePermissao? AlternativaEmUnidade { get; init; }

    private DefinicaoDePermissao? AlternativaNoResource { get; init; }

    /// <summary>
    /// Avalia sem registrar para gravar só o que governou o desfecho: a negativa da principal não é registrada quando a
    /// alternativa permite. O impedimento por inscrição não é negativa, e a alternativa não o supera.
    /// </summary>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IVerificadorDeAcesso acesso = context.HttpContext.RequestServices.GetRequiredService<IVerificadorDeAcesso>();
        CancellationToken cancellationToken = context.HttpContext.RequestAborted;

        DecisaoDeAcesso principal = await acesso.AvaliarAsync(Definicao.Requisito, ResourceDe(Definicao), cancellationToken).ConfigureAwait(false);
        List<DecisaoDeAcesso> decisoes = [principal];
        if (principal.Resultado == ResultadoDoAcesso.Negado)
        {
            decisoes.AddRange(await AvaliarAlternativaAsync(acesso, cancellationToken).ConfigureAwait(false));
        }

        await acesso.RegistrarDesfechoAsync(decisoes, IntencaoDoRegistro.Pedido, cancellationToken).ConfigureAwait(false);
        context.Result = RespostaDoAcesso.Para(Desfecho(decisoes), static () => new ForbidResult());
    }

    private async Task<IReadOnlyList<DecisaoDeAcesso>> AvaliarAlternativaAsync(IVerificadorDeAcesso acesso, CancellationToken cancellationToken)
    {
        if (AlternativaEmUnidade is not null)
        {
            return await acesso.AvaliarEmAlgumaUnidadeAsync(AlternativaEmUnidade.Requisito, cancellationToken).ConfigureAwait(false);
        }

        return AlternativaNoResource is null
            ? []
            : [await acesso.AvaliarAsync(AlternativaNoResource.Requisito, ResourceDe(AlternativaNoResource), cancellationToken).ConfigureAwait(false)];
    }

    // Alguma permitida autoriza; sem nenhuma, vale o desfecho da última avaliada.
    private static ResultadoDoAcesso Desfecho(List<DecisaoDeAcesso> decisoes) =>
        decisoes.Exists(static d => d is DecisaoPermitida) ? ResultadoDoAcesso.Permitido : decisoes[^1].Resultado;

    // Cada permissão leva ao resource a própria sensibilidade, para que a decisão e o registro da alternativa tragam a
    // classe dela, e não a da principal.
    private ResourceContext? ResourceDe(DefinicaoDePermissao definicao) =>
        _resourceTipo is null ? null : ConstruirResource(_resourceTipo, definicao);

    private static ResourceContext ConstruirResource(string tipo, DefinicaoDePermissao definicao)
    {
        Result<ResourceContext> resource = ResourceContext.From(tipo, definicao.Requisito.Sensibilidade);

        return resource.Value
            ?? throw new ArgumentException(resource.Error!.Message, nameof(tipo));
    }

    private void ExigirUmaSoAlternativa()
    {
        if (AlternativaEmUnidade is not null || AlternativaNoResource is not null)
        {
            throw new InvalidOperationException("A exigência aceita uma só alternativa: em alguma unidade ou outra permissão.");
        }
    }

    private static DefinicaoDePermissao Obter(object? permissao, string parametro) =>
        permissao is TPermissao tipada
            ? CatalogoDePermissoes<TPermissao>.Obter(tipada)
            : throw new ArgumentException(
                $"A permissão deve ser um {typeof(TPermissao).Name}, e não {permissao?.GetType().Name ?? "nulo"}.", parametro);
}
