namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O catálogo de fatos na forma em que o modelo é conferido: os fatos, o vocabulário dos
/// predicados, o domínio dos categóricos que o catálogo não enumera, as dependências dos derivados
/// por regra e o formato dos fatos de texto.
/// </summary>
internal sealed record CatalogoDoModelo(
    IReadOnlyDictionary<string, FatoDoCatalogo> Fatos,
    IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario,
    IReadOnlyDictionary<string, DominioDeValores> DominiosDinamicos,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Derivacoes,
    IReadOnlyDictionary<string, string> Formatos)
{
    public static CatalogoDoModelo De(IReadOnlyList<FatoCandidato> fatos) => new(
        VocabularioDoCatalogo.ParaRegras(fatos),
        VocabularioDoCatalogo.DescritoresDoFormulario(fatos),
        VocabularioDoCatalogo.DominiosDinamicos(fatos),
        VocabularioDoCatalogo.Derivacoes(fatos),
        VocabularioDoCatalogo.Formatos(fatos));
}

/// <summary>O que o modelo gravado já vincula no catálogo: o vínculo existente não é recusado ao ser desativado.</summary>
internal sealed record VinculosDoModelo(VinculosDeFatos Fatos, IReadOnlySet<(Guid TermoId, Guid VersaoId)> Termos)
{
    public static VinculosDoModelo Nenhum { get; } = new(
        new VinculosDeFatos(new HashSet<string>(), new HashSet<(string, string)>()), new HashSet<(Guid, Guid)>());
}

/// <summary>
/// A conferência do conteúdo do modelo contra o catálogo, pelas mesmas regras do formulário do
/// processo (<see cref="ConferenciaNoCatalogo"/>, <see cref="CoerenciaDoCampo"/>,
/// <see cref="PredicadoDnfValidador"/>). Cobre o fato que cada item e cada campo de grupo coleta, o tipo do campo, as
/// regras, as restrições, os pressupostos, a versão dos termos e o vínculo novo a fato ou valor
/// desativado. O item de forma inválida não é conferido: a recusa dele é a de forma, do modelo.
/// </summary>
/// <remarks>
/// Os valores das fontes do processo — opções declaradas, modalidades e municípios do bônus — só
/// existem no processo. Aqui são aceitos pela forma, e a cópia do modelo no processo os confere de
/// novo.
/// </remarks>
internal static class ConferenciaDoModelo
{
    public static List<FieldError> Conferir(
        ConteudoLido conteudo,
        CatalogoDoModelo catalogo,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        VinculosDoModelo existentes)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(existentes);

        List<FieldError> erros = [];
        foreach ((int indice, ItemDoModelo item) in conteudo.Itens)
        {
            erros.AddRange(ConferirCampo(item, catalogo, ConferenciaNoCatalogo.FatoDoItem).Select(e => e with { Field = Prefixar($"itens[{indice}]", e.Field) }));
        }

        foreach ((int indice, GrupoDoModelo grupo) in conteudo.Grupos)
        {
            if (ConferirPredicado($"grupos[{indice}].exibicao", grupo.Exibicao, catalogo) is { } exibicao)
            {
                erros.Add(exibicao);
            }

            if (ConferirPredicado($"grupos[{indice}].predicadoObrigatoriedade", grupo.Obrigatoriedade.Predicado, catalogo) is { } obrigatoriedade)
            {
                erros.Add(obrigatoriedade);
            }

            for (int j = 0; j < grupo.Subitens.Count; j++)
            {
                erros.AddRange(ConferirCampo(grupo.Subitens[j], catalogo, ConferenciaNoCatalogo.FatoDoSubitem)
                    .Select(e => e with { Field = Prefixar($"grupos[{indice}].subitens[{j}]", e.Field) }));
            }
        }

        foreach ((int indice, EtapaDoModelo etapa) in conteudo.Etapas)
        {
            if (ConferirPredicado($"etapas[{indice}].exibicao", etapa.Exibicao, catalogo) is { } recusa)
            {
                erros.Add(recusa);
            }
        }

        foreach ((int indice, TermoDoModelo termo) in conteudo.Termos)
        {
            erros.AddRange(ConferirTermo(termo, catalogo, versoes, existentes.Termos).Select(e => e with { Field = Prefixar($"termos[{indice}]", e.Field) }));
        }

        for (int i = 0; i < conteudo.Pressupostos.Count; i++)
        {
            string pressuposto = ModeloFormulario.CodigoNaFormaGravada(conteudo.Pressupostos[i]);
            if (!string.IsNullOrWhiteSpace(conteudo.Pressupostos[i])
                && ConferenciaNoCatalogo.FatoDoItem(pressuposto, catalogo.Fatos) is { } recusa)
            {
                erros.Add(new($"pressupostos[{i}]", recusa, pressuposto));
            }
        }

        // Os vínculos são os do conteúdo como será gravado: um código com espaço é o mesmo fato.
        VinculosDeFatos propostos = Vinculos(ModeloFormulario.NaFormaGravada(conteudo.ParaConteudo()));
        if (ConferenciaNoCatalogo.VinculoNovo(catalogo.Fatos, existentes.Fatos, propostos) is { IsFailure: true } vinculo)
        {
            erros.AddRange(vinculo.Errors);
        }

        return erros;
    }

    /// <summary>O que o modelo gravado já vincula: os fatos e valores, e a versão de cada termo.</summary>
    public static VinculosDoModelo VinculosGravados(ConteudoDoModelo conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        return new(Vinculos(conteudo), conteudo.Termos.Select(static t => (t.TermoId, t.VersaoId)).ToHashSet());
    }

    /// <summary>
    /// O que o conteúdo vincula no catálogo: os fatos dos campos — itens e campos de grupo — e dos
    /// pressupostos, os fatos e valores citados pelas regras, os fatos cujas respostas formam opções
    /// e os valores das opções permitidas de cada campo.
    /// </summary>
    public static VinculosDeFatos Vinculos(ConteudoDoModelo conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ItemDoModelo[] campos = [.. conteudo.Itens, .. conteudo.Grupos.SelectMany(static g => g.Subitens)];
        IEnumerable<PredicadoDnf> predicados = campos
            .SelectMany(static i => new[] { i.Exibicao, i.Obrigatoriedade.Predicado, i.Impedimento?.Quando }
                .Concat(i.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas.Select(static e => e.Quando))))
            .Concat(conteudo.Grupos.SelectMany(static g => new[] { g.Exibicao, g.Obrigatoriedade.Predicado }))
            .Concat(conteudo.Etapas.Select(static e => e.Exibicao))
            .Concat(conteudo.Termos.SelectMany(static t => new[] { t.Exibicao, t.Obrigatoriedade.Predicado }))
            .Concat(conteudo.Documentos.Select(static d => d.Quando))
            .OfType<PredicadoDnf>();
        IEnumerable<(string Fato, JsonElement Valor)> condicoes = predicados
            .SelectMany(static p => p.Clausulas).SelectMany(static c => c.Condicoes).Select(static c => (c.Fato, c.Valor));
        IEnumerable<(string Fato, string Valor)> opcoes = campos
            .SelectMany(static i => i.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas)
                .SelectMany(e => e.Valores.Select(v => (i.FatoCodigo, v))));
        IEnumerable<string> fatos = campos.Select(static i => i.FatoCodigo)
            .Concat(conteudo.Pressupostos)
            .Concat(campos.SelectMany(static i => i.Restricoes.SelectMany(static r => r.FatosCitados)));
        return VinculosDeFatos.De(fatos, condicoes, opcoes);
    }

    /// <summary>
    /// O campo do modelo contra o catálogo: o fato coletável — como item ou como campo de grupo,
    /// conforme <paramref name="recusaDeColetavel"/> —, o tipo do campo, as regras e as restrições.
    /// </summary>
    private static IEnumerable<FieldError> ConferirCampo(
        ItemDoModelo item, CatalogoDoModelo catalogo, Func<string, IReadOnlyDictionary<string, FatoDoCatalogo>, DomainError?> recusaDeColetavel)
    {
        if (FormaDoItem.ValidarFormaBasica(item.FatoCodigo, item.Ordem, item.Rotulo, item.TipoRenderizacao).Count > 0)
        {
            yield break;
        }

        string codigo = ModeloFormulario.CodigoNaFormaGravada(item.FatoCodigo);
        if (recusaDeColetavel(codigo, catalogo.Fatos) is { } naoColetavel)
        {
            yield return new("fatoCodigo", naoColetavel, codigo);
            yield break;
        }

        FatoDoCatalogo fato = catalogo.Fatos[codigo];
        if (CoerenciaDoCampo.Validar(codigo, item.TipoRenderizacao, fato.Dominio, fato.Cardinalidade, fato.FonteValores) is { } incoerencia)
        {
            yield return new("tipoRenderizacao", incoerencia, codigo);
        }

        if (ConferirPredicado("precondicao", item.Exibicao, catalogo) is { } exibicao)
        {
            yield return exibicao;
        }

        if (ConferirPredicado("predicadoObrigatoriedade", item.Obrigatoriedade.Predicado, catalogo) is { } obrigatoriedade)
        {
            yield return obrigatoriedade;
        }

        if (ConferirPredicado("impedimento.quando", item.Impedimento?.Quando, catalogo) is { } impedimento)
        {
            yield return impedimento;
        }

        foreach (FieldError restricao in ConferenciaNoCatalogo.SemanticaDasRestricoes(
            fato, item.TipoRenderizacao, item.Restricoes, catalogo.Fatos, catalogo.Vocabulario, catalogo.DominiosDinamicos))
        {
            yield return restricao;
        }
    }

    private static IEnumerable<FieldError> ConferirTermo(
        TermoDoModelo termo,
        CatalogoDoModelo catalogo,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        IReadOnlySet<(Guid TermoId, Guid VersaoId)> termosExistentes)
    {
        if (ConferirPredicado("exibicao", termo.Exibicao, catalogo) is { } exibicao)
        {
            yield return exibicao;
        }

        if (ConferirPredicado("predicadoObrigatoriedade", termo.Obrigatoriedade.Predicado, catalogo) is { } obrigatoriedade)
        {
            yield return obrigatoriedade;
        }

        // A versão já escolhida pelo modelo continua com ele, mesmo que o catálogo tenha excluído o
        // termo depois: a recusa é só do vínculo novo (ADR-0136).
        if (!termosExistentes.Contains((termo.TermoId, termo.VersaoId))
            && (!versoes.TryGetValue(termo.VersaoId, out VersaoTermoConsentimentoView? versao) || versao.TermoId != termo.TermoId))
        {
            yield return new("versaoId", new DomainError(
                ModeloFormularioErrorCodes.TermoVersaoNaoEncontrada,
                "A versão informada não existe no catálogo de termos, ou pertence a outro termo."),
                $"{termo.TermoId}/{termo.VersaoId}");
        }
    }

    /// <summary>
    /// A regra que cita fato calculado de atributos do candidato é recusada antes da semântica; a
    /// citação de fato fora do formulário é do grafo do modelo. A recusa leva o fato (e o valor, quando
    /// é do valor) que a causa.
    /// </summary>
    private static FieldError? ConferirPredicado(string campo, PredicadoDnf? predicado, CatalogoDoModelo catalogo)
    {
        if (predicado is null)
        {
            return null;
        }

        if (ConferenciaNoCatalogo.AtributoCitado(predicado.FatosCitados, catalogo.Fatos) is { } atributo)
        {
            return new(campo, ConferenciaNoCatalogo.CitacaoDeAtributoDoCandidato([atributo], catalogo.Fatos)!, atributo);
        }

        return PredicadoDnfValidador.ValidarComCausa(predicado, catalogo.Vocabulario, null, catalogo.DominiosDinamicos)
            is ({ IsFailure: true } recusa, { } causa)
            ? new(campo, recusa.Error!, causa)
            : null;
    }

    private static string Prefixar(string prefixo, string? campo) => string.IsNullOrEmpty(campo) ? prefixo : $"{prefixo}.{campo}";
}
