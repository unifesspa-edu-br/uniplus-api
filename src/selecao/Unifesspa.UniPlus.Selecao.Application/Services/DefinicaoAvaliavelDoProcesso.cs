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
/// campo, os valores selecionáveis com a descrição de cada um e a apresentação dos fatos que as
/// regras citam e nenhum formulário do processo pergunta.
/// </summary>
public sealed record DefinicaoAvaliavel(
    DefinicaoFormulario Definicao,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas,
    IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis,
    IReadOnlyDictionary<string, ApresentacaoDoFato> ApresentacaoDosPressupostos);

/// <summary>
/// Monta a definição avaliável dos formulários do processo, a mesma para a pré-visualização e para o
/// formulário que se publica (ADR-0139): os formulários, os itens, os grupos e os termos, as derivações
/// conferidas contra o domínio em que contribuem, os agregados sobre os grupos e a oferta de valores.
/// </summary>
public static class DefinicaoAvaliavelDoProcesso
{
    /// <summary>
    /// A definição da configuração viva, com o catálogo vivo. O
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

        return ResolverOfertaDaConfiguracaoViva(processo, porCodigo);
    }

    private static Result<DefinicaoAvaliavel> ResolverOfertaDaConfiguracaoViva(ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> porCodigo)
    {
        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> oferta =
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, porCodigo);
        if (oferta.IsFailure)
        {
            return Result<DefinicaoAvaliavel>.Failure(oferta.Error!);
        }

        return ComOfertaDaConfiguracaoViva(processo, porCodigo, oferta.Value!);
    }

    private static Result<DefinicaoAvaliavel> ComOfertaDaConfiguracaoViva(ProcessoSeletivo processo,
        IReadOnlyDictionary<string, FatoCandidatoView> porCodigo,
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> oferta)
    {
        Result<IReadOnlyList<DerivacaoDoFato>> derivacoes = Derivacoes(processo, porCodigo);
        if (derivacoes.IsFailure)
        {
            return Result<DefinicaoAvaliavel>.Failure(derivacoes.Error!);
        }

        DefinicaoFormulario definicao = DefinicaoDoProcesso.Montar(
            processo.Formularios, processo.FatosColetados, processo.GruposColetados, processo.TermosExigidos,
            derivacoes.Value!, AgregadosDosGrupos(processo.GruposColetados, porCodigo));

        Result<IReadOnlyDictionary<string, ApresentacaoDoFato>> apresentacao = ApresentarPressupostos(definicao, processo, porCodigo);
        return apresentacao.IsFailure ? Result<DefinicaoAvaliavel>.Failure(apresentacao.Error!)
            : Result<DefinicaoAvaliavel>.Success(new(definicao, Ofertados(oferta), oferta, apresentacao.Value!));
    }

    private static Result<IReadOnlyDictionary<string, ApresentacaoDoFato>> ApresentarPressupostos(DefinicaoFormulario definicao,
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> porCodigo)
    {
        Dictionary<string, ApresentacaoDoFato> apresentacao = new(StringComparer.Ordinal);
        foreach (string codigo in PressupostosNaoPerguntados(definicao, processo.Formularios, processo.FatosColetados))
        {
            if (!porCodigo.TryGetValue(codigo, out FatoCandidatoView? fato))
            {
                return Result<IReadOnlyDictionary<string, ApresentacaoDoFato>>.Failure(new DomainError(
                    "ProcessoSeletivo.PressupostoSemCadastro",
                    $"O fato '{codigo}', que as regras de um formulário citam, não está no catálogo de fatos do candidato."));
            }

            apresentacao[codigo] = VocabularioDeFatos.Apresentacao(fato);
        }

        return Result<IReadOnlyDictionary<string, ApresentacaoDoFato>>.Success(apresentacao);
    }

    /// <summary>
    /// A definição da versão congelada, sem o catálogo vivo (ADR-0070): a oferta e os agregados são
    /// os congelados na versão, e cada derivação é conferida contra o que ela própria contribui — a
    /// publicação já conferiu cada contribuição contra o domínio do fato. Com <paramref name="ofertasDeCurso"/>,
    /// as derivações que a oferta de vagas define — a modalidade do candidato — saem só da união das
    /// distribuições dessas ofertas; sem elas, da união de todas, que é o que a apresentação do formulário
    /// e a publicação leem.
    /// </summary>
    public static Result<DefinicaoAvaliavel> DoCongelado(EnvelopeReidratado envelope, IReadOnlyCollection<Guid>? ofertasDeCurso = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        ConfiguracaoPublicada configuracaoPublicada = envelope.Configuracao;
        Result<IReadOnlyList<DerivacaoDoFato>> derivacoes = DerivacoesDoCongelado(configuracaoPublicada, ofertasDeCurso);
        return derivacoes.IsFailure ? Result<DefinicaoAvaliavel>.Failure(derivacoes.Error!)
            : MontarDoCongelado(envelope, derivacoes.Value!);
    }

    private static Result<IReadOnlyList<DerivacaoDoFato>> DerivacoesDoCongelado(ConfiguracaoPublicada configuracaoPublicada, IReadOnlyCollection<Guid>? ofertasDeCurso)
    {
        List<DerivacaoDoFato> derivacoes = [];
        foreach (ConfiguracaoDerivacaoFato configuracao in configuracaoPublicada.DerivacoesEfetivas(ofertasDeCurso).OrderBy(static c => c.CodigoFato, StringComparer.Ordinal))
        {
            Result<DerivacaoDoFato> derivacao = configuracao.ParaDerivacaoDoFato(Contribuicoes(configuracao));
            if (derivacao.IsFailure)
            {
                return Result<IReadOnlyList<DerivacaoDoFato>>.Failure(derivacao.Error!);
            }

            derivacoes.Add(derivacao.Value!);
        }

        return Result<IReadOnlyList<DerivacaoDoFato>>.Success(derivacoes);
    }

    private static Result<DefinicaoAvaliavel> MontarDoCongelado(EnvelopeReidratado envelope, IReadOnlyList<DerivacaoDoFato> derivacoes)
    {
        ConfiguracaoPublicada configuracaoPublicada = envelope.Configuracao;
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores =
            envelope.DadosDeCatalogo.ValoresSelecionaveisCongelados ?? new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal);
        DefinicaoFormulario definicao = DefinicaoDoProcesso.Montar(
            configuracaoPublicada.Coleta.Formularios, [.. configuracaoPublicada.Coleta.Itens], configuracaoPublicada.Coleta.GruposColetados, configuracaoPublicada.Coleta.TermosExigidos, derivacoes, envelope.DadosDeCatalogo.AgregadosDosGrupos!);

        return Result<DefinicaoAvaliavel>.Success(
            new DefinicaoAvaliavel(definicao, Ofertados(valores), valores, envelope.DadosDeCatalogo.ApresentacaoDosPressupostos!));
    }

    /// <summary>
    /// Os fatos que as regras de algum formulário citam e que nenhum formulário do processo pergunta —
    /// os derivados do sistema, os agregados sobre grupo e o que outra fase produz.
    /// </summary>
    private static IEnumerable<string> PressupostosNaoPerguntados(
        DefinicaoFormulario definicao, IEnumerable<FormularioProcesso> formularios, IEnumerable<FatoColetado> fatos)
    {
        HashSet<string> perguntados = new(fatos.Select(static f => f.FatoCodigo), StringComparer.Ordinal);
        return formularios
            .SelectMany(f => RecorteDaFinalidade.De(definicao, DefinicaoDoProcesso.CodigoDaEtapa(f.Finalidade, null)).Pressupostos)
            .Where(c => !perguntados.Contains(c))
            .Distinct(StringComparer.Ordinal);
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
    private static Result<IReadOnlyList<DerivacaoDoFato>> Derivacoes(
        ProcessoSeletivo processo, IReadOnlyDictionary<string, FatoCandidatoView> catalogo)
    {
        Dictionary<string, DominioDeValores> dominios = VocabularioDeFatos.DominiosDinamicos(processo, catalogo.Values);
        List<DerivacaoDoFato> derivacoes = [];
        return DerivacoesOrdenadas(processo.DerivacoesEfetivas(), catalogo, dominios, derivacoes);
    }

    private static Result<IReadOnlyList<DerivacaoDoFato>> DerivacoesOrdenadas(
        IEnumerable<ConfiguracaoDerivacaoFato> configuracoes, IReadOnlyDictionary<string, FatoCandidatoView> catalogo,
        IReadOnlyDictionary<string, DominioDeValores> dominios, List<DerivacaoDoFato> derivacoes)
    {
        foreach (ConfiguracaoDerivacaoFato configuracao in configuracoes.OrderBy(static c => c.CodigoFato, StringComparer.Ordinal))
        {
            IReadOnlyCollection<string> dominio = DominioDeContribuicao(configuracao, catalogo, dominios);
            Result<DerivacaoDoFato> derivacao = configuracao.ParaDerivacaoDoFato(dominio);
            if (derivacao.IsFailure)
            {
                return Result<IReadOnlyList<DerivacaoDoFato>>.Failure(derivacao.Error!);
            }

            derivacoes.Add(derivacao.Value!);
        }

        return Result<IReadOnlyList<DerivacaoDoFato>>.Success(derivacoes);
    }

    private static IReadOnlyCollection<string> DominioDeContribuicao(ConfiguracaoDerivacaoFato configuracao,
        IReadOnlyDictionary<string, FatoCandidatoView> catalogo, IReadOnlyDictionary<string, DominioDeValores> dominios) =>
        catalogo.TryGetValue(configuracao.CodigoFato, out FatoCandidatoView? fato)
            && VocabularioDeFatos.DominioDeContribuicao(fato, dominios) is { } doCatalogo ? doCatalogo : Contribuicoes(configuracao);

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
