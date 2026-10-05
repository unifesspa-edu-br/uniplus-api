namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Os formulários do processo prontos para o avaliador: a definição, a oferta de valores de cada
/// campo e os valores selecionáveis com a descrição de cada um.
/// </summary>
public sealed record DefinicaoAvaliavel(
    DefinicaoFormulario Definicao,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas,
    IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis);

/// <summary>
/// Monta a definição avaliável dos formulários do processo, a mesma para a pré-visualização e para o
/// formulário que se publica (ADR-0139): os formulários, os itens, os grupos e os termos, as derivações
/// conferidas contra o domínio em que contribuem, os agregados sobre os grupos e a oferta de valores.
/// </summary>
public static class DefinicaoAvaliavelDoProcesso
{
    /// <summary>
    /// A definição da configuração viva — rascunho ou sessão de retificação —, com o catálogo vivo. O
    /// campo que o catálogo deixou de aceitar é recusado antes, como na publicação.
    /// </summary>
    public static Result<DefinicaoAvaliavel> DaConfiguracaoViva(ProcessoSeletivo processo, IReadOnlyCollection<FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        Dictionary<string, FatoCandidatoView> porCodigo = catalogo.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);
        if (ConferenciaDeColetabilidadeDeFatos.Conferir(processo, porCodigo) is { IsFailure: true } naoColetavel)
        {
            return Result<DefinicaoAvaliavel>.Failure(naoColetavel.Error!);
        }

        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> oferta =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, porCodigo);
        if (oferta.IsFailure)
        {
            return Result<DefinicaoAvaliavel>.Failure(oferta.Error!);
        }

        Result<IReadOnlyList<RegrasDerivacaoFato>> derivacoes = Derivacoes(processo, porCodigo);
        if (derivacoes.IsFailure)
        {
            return Result<DefinicaoAvaliavel>.Failure(derivacoes.Error!);
        }

        DefinicaoFormulario definicao = DefinicaoDoProcesso.Montar(
            processo.Formularios, processo.FatosColetados, processo.GruposColetados, processo.TermosExigidos,
            derivacoes.Value!, AgregadosDosGrupos(processo.GruposColetados, porCodigo));

        return Result<DefinicaoAvaliavel>.Success(new DefinicaoAvaliavel(definicao, Ofertados(oferta.Value!), oferta.Value!));
    }

    /// <summary>
    /// A definição da versão congelada, sem o catálogo vivo (ADR-0070): a oferta e os agregados são
    /// os congelados na versão, e cada derivação é conferida contra o que ela própria contribui — a
    /// publicação já conferiu cada contribuição contra o domínio do fato.
    /// </summary>
    public static Result<DefinicaoAvaliavel> DoCongelado(EnvelopeReidratado envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        GrafoConfiguracao grafo = envelope.Grafo;
        List<RegrasDerivacaoFato> derivacoes = [];
        foreach (ConfiguracaoDerivacaoFato configuracao in grafo.RegrasDerivacao.OrderBy(static c => c.CodigoFato, StringComparer.Ordinal))
        {
            Result<RegrasDerivacaoFato> regras = configuracao.ParaRegrasDerivacao(Contribuicoes(configuracao));
            if (regras.IsFailure)
            {
                return Result<DefinicaoAvaliavel>.Failure(regras.Error!);
            }

            derivacoes.Add(regras.Value!);
        }

        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores =
            envelope.ValoresSelecionaveisCongelados ?? new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal);
        DefinicaoFormulario definicao = DefinicaoDoProcesso.Montar(
            grafo.Formularios, grafo.FatosColetados, grafo.GruposColetados, grafo.TermosExigidos, derivacoes, envelope.AgregadosDosGrupos);

        return Result<DefinicaoAvaliavel>.Success(new DefinicaoAvaliavel(definicao, Ofertados(valores), valores));
    }

    /// <summary>Os códigos ofertados por fato, a partir dos valores selecionáveis.</summary>
    public static Dictionary<string, IReadOnlySet<string>> Ofertados(
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        return valores
            .Where(static v => v.Value is not null)
            .ToDictionary(
                static v => v.Key,
                static v => (IReadOnlySet<string>)v.Value!.Select(static o => o.Codigo).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// As derivações do processo, cada uma conferida contra o domínio em que contribui: o dinâmico
    /// do processo, o do catálogo ou, quando o servidor não enumera a fonte, o das próprias regras.
    /// </summary>
    private static Result<IReadOnlyList<RegrasDerivacaoFato>> Derivacoes(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        Dictionary<string, DominioDeValores> dominios = VocabularioDeFatos.DominiosDinamicos(processo, catalogo.Values);
        List<RegrasDerivacaoFato> derivacoes = [];
        foreach (ConfiguracaoDerivacaoFato configuracao in processo.RegrasDerivacao.OrderBy(static c => c.CodigoFato, StringComparer.Ordinal))
        {
            IReadOnlyCollection<string> dominio =
                catalogo.TryGetValue(configuracao.CodigoFato, out FatoCandidatoView? fato) && VocabularioDeFatos.DominioDeContribuicao(fato, dominios) is { } doCatalogo
                    ? doCatalogo
                    : Contribuicoes(configuracao);
            Result<RegrasDerivacaoFato> regras = configuracao.ParaRegrasDerivacao(dominio);
            if (regras.IsFailure)
            {
                return Result<IReadOnlyList<RegrasDerivacaoFato>>.Failure(regras.Error!);
            }

            derivacoes.Add(regras.Value!);
        }

        return Result<IReadOnlyList<RegrasDerivacaoFato>>.Success(derivacoes);
    }

    /// <summary>Os códigos que as regras da derivação contribuem.</summary>
    private static string[] Contribuicoes(ConfiguracaoDerivacaoFato configuracao) =>
        [.. configuracao.Regras.Select(static r => r.Contribui).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Os agregados do catálogo cujo fato de membro é campo de um grupo do processo — os que a
    /// publicação congela junto dos grupos.
    /// </summary>
    public static IReadOnlyList<DefinicaoAgregado> AgregadosDosGrupos(
        IReadOnlyCollection<GrupoColetado> grupos, IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(catalogo);

        Dictionary<string, string> grupoDoCampo = grupos
            .SelectMany(static g => g.Subitens.Select(s => (Campo: s.FatoCodigo, Grupo: g.Codigo)))
            .ToDictionary(static p => p.Campo, static p => p.Grupo, StringComparer.Ordinal);
        return [.. VocabularioDeFatos.MembroPorAgregado(catalogo.Values)
            .Where(a => grupoDoCampo.ContainsKey(a.Value) && catalogo.ContainsKey(a.Value))
            .OrderBy(static a => a.Key, StringComparer.Ordinal)
            .Select(a => (Agregado: a.Key, Membro: a.Value, Operacao: AgregadoDeGrupo.OperacaoDoDominio(catalogo[a.Value].Dominio)))
            .Where(static a => a.Operacao != OperacaoAgregado.Nenhuma)
            .Select(a => new DefinicaoAgregado(a.Agregado, grupoDoCampo[a.Membro], a.Membro, a.Operacao))];
    }
}
