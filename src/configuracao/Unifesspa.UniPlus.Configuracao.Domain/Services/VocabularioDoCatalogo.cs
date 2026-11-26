namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que um predicado sobre fatos do catálogo sabe avaliar, na Configuração: o descritor de cada fato
/// citável e o domínio dos categóricos cujos valores o catálogo não enumera. Compartilhado pelas regras
/// padrão dos derivados e pelos modelos de formulário, que conferem as regras contra o mesmo catálogo.
/// </summary>
public static class VocabularioDoCatalogo
{
    /// <summary>
    /// Os fatos que um predicado sabe avaliar: booleano, numérico e categórico do candidato; o
    /// categórico global só quando já tem valores. É o vocabulário das regras padrão dos derivados,
    /// que não citam fato de membro de grupo.
    /// </summary>
    public static Dictionary<string, DescritorFatoCandidato> Descritores(IEnumerable<FatoCandidato> fatos) =>
        Descritores(fatos, static fato => fato.Escopo == EscopoFato.Candidato);

    /// <summary>
    /// O vocabulário das regras do formulário: o do candidato e os fatos de membro de grupo, que o
    /// campo do grupo cita na mesma ocorrência. A citação de membro fora do grupo é do grafo do
    /// formulário.
    /// </summary>
    public static Dictionary<string, DescritorFatoCandidato> DescritoresDoFormulario(IEnumerable<FatoCandidato> fatos) =>
        Descritores(fatos, static _ => true);

    private static Dictionary<string, DescritorFatoCandidato> Descritores(IEnumerable<FatoCandidato> fatos, Func<FatoCandidato, bool> noVocabulario)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        Dictionary<string, FatoCandidato> porCodigo = PorCodigo(fatos);
        Dictionary<string, DescritorFatoCandidato> vocabulario = new(StringComparer.Ordinal);
        foreach (FatoCandidato fato in porCodigo.Values.Where(noVocabulario))
        {
            if (DescritorDe(fato, porCodigo) is { } descritor)
            {
                vocabulario[fato.Codigo] = descritor;
            }
        }

        return vocabulario;
    }

    private static DescritorFatoCandidato? DescritorDe(FatoCandidato fato, IReadOnlyDictionary<string, FatoCandidato> porCodigo)
    {
        if (TipoNoVocabulario(fato) is not { } tipo)
        {
            return null;
        }

        IReadOnlyList<string>? valores = tipo == TipoDominioFato.CategoricoEstatico
            ? [.. ValoresDe(fato, porCodigo).Select(static valor => valor.Codigo)]
            : null;
        return DescritorFatoCandidato.Criar(fato.Codigo, tipo, valores) is { IsSuccess: true } descritor ? descritor.Value : null;
    }

    /// <summary>
    /// O domínio do fato na matriz de operadores das regras, ou nulo quando o fato não é citável em regra configurada
    /// (texto, data e endereço).
    /// </summary>
    public static TipoDominioFato? TipoNoVocabulario(FatoCandidato fato) =>
        fato switch
        {
            { Dominio: TipoDoDado.SimOuNao } => TipoDominioFato.Booleano,
            { Dominio: TipoDoDado.Numerico } => TipoDominioFato.Numerico,
            { Dominio: TipoDoDado.Opcao, FonteValores: FonteValoresFato.Catalogo } => TipoDominioFato.CategoricoEstatico,
            { Dominio: TipoDoDado.Opcao } => TipoDominioFato.CategoricoDinamico,
            _ => null,
        };

    /// <summary>

    /// <summary>
    /// O domínio dos categóricos cujos valores o catálogo não enumera. UF e município de residência
    /// vêm do Geo e são conferidos como em qualquer processo; as opções do processo, as modalidades
    /// e os municípios do FADA só existem no processo, que confere a regra de novo quando a copia.
    /// Os tipos de deficiência vivem em outro cadastro, e a Seleção confere contra ele o código que a
    /// regra cita quando aplica o modelo.
    /// </summary>
    public static Dictionary<string, DominioDeValores> DominiosDinamicos(IEnumerable<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        Dictionary<string, DominioDeValores> dominios = new(StringComparer.Ordinal);
        foreach (FatoCandidato fato in fatos.Where(static fato => fato.Dominio == TipoDoDado.Opcao))
        {
            if (DominioPelaFonte(fato.FonteValores) is { } dominio)
            {
                dominios[fato.Codigo] = dominio;
            }
        }

        return dominios;
    }

    private static DominioDeValores? DominioPelaFonte(FonteValoresFato? fonte) =>
        fonte switch
        {
            FonteValoresFato.UnidadesFederativas => DominioDeValores.Enumerado(ReferenciaCidadeGeo.Ufs),
            FonteValoresFato.Municipios => DominioDeValores.PorFormato(ReferenciaCidadeGeo.EhCodigoMunicipioValido),
            FonteValoresFato.OpcoesDoProcesso or FonteValoresFato.Modalidade or FonteValoresFato.MunicipiosDoFada
                or FonteValoresFato.TiposDeDeficiencia or FonteValoresFato.MunicipiosProvaPresencial
                => DominioDeValores.PorFormato(static _ => true),
            _ => null,
        };

    /// <summary>
    /// Como cada fato citado e não perguntado se apresenta: o nome e o tipo de campo que o domínio
    /// admite. O código que o catálogo não tem fica de fora.
    /// </summary>
    public static Dictionary<string, ApresentacaoDoFato> ApresentacaoDos(IEnumerable<FatoCandidato> fatos, IEnumerable<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(codigos);
        Dictionary<string, FatoCandidato> porCodigo = PorCodigo(fatos);
        return codigos
            .Where(porCodigo.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(static codigo => codigo, codigo => Apresentacao(porCodigo[codigo]), StringComparer.Ordinal);
    }

    private static ApresentacaoDoFato Apresentacao(FatoCandidato fato) =>
        ApresentacaoDoFato.DoCatalogo(
            fato.Codigo,
            fato.Nome,
            fato.Dominio,
            fato.Cardinalidade,
            fato.FonteValores);

    /// <summary>O catálogo como as regras do formulário o conferem, por código (<see cref="FatoDoCatalogo"/>).</summary>
    public static Dictionary<string, FatoDoCatalogo> ParaRegras(IEnumerable<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        Dictionary<string, FatoCandidato> porCodigo = PorCodigo(fatos);
        return porCodigo.Values.ToDictionary(static fato => fato.Codigo, fato => FatoParaRegras(fato, porCodigo), StringComparer.Ordinal);
    }

    private static FatoDoCatalogo FatoParaRegras(FatoCandidato fato, IReadOnlyDictionary<string, FatoCandidato> porCodigo) =>
        new(
            fato.Codigo,
            fato.Nome,
            fato.Dominio,
            fato.Cardinalidade,
            fato.Origem,
            fato.Binding,
            fato.FonteValores,
            fato.Escopo,
            fato.Ativo,
            [.. ValoresDe(fato, porCodigo).Select(static valor => new ValorDoCatalogo(valor.Codigo, valor.Ativo))],
            fato.Aprovacao.EstaAprovada);

    /// <summary>
    /// Os valores do fato: os próprios, ou, no agregado sobre grupo repetível, os do fato de membro
    /// que ele resume (ADR-0138) — o agregado não tem valores próprios. A regra única para quem lê o
    /// domínio efetivo de um fato: o vocabulário, a conferência das regras e o contrato que a Seleção
    /// lê. A manutenção do administrador mostra o que cada fato guarda; os valores do agregado são
    /// mantidos no fato de membro.
    /// </summary>
    public static IReadOnlyCollection<FatoValorDominio> ValoresDe(FatoCandidato fato, IReadOnlyDictionary<string, FatoCandidato> porCodigo)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(porCodigo);
        return fato.FatoDeMembroAgregado is { } membro && porCodigo.TryGetValue(membro, out FatoCandidato? doMembro)
            ? doMembro.ValoresDominioDeclarados
            : fato.ValoresDominioDeclarados;
    }

    /// <summary>
    /// Os agregados do catálogo sobre os grupos do modelo: os que agregam um campo de algum grupo, com
    /// a operação que o domínio do fato de membro dá (ADR-0138), em ordem de código.
    /// </summary>
    public static IReadOnlyList<DefinicaoAgregado> AgregadosDosGrupos(IEnumerable<FatoCandidato> fatos, IEnumerable<GrupoDoModelo> grupos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(grupos);
        Dictionary<string, FatoCandidato> porCodigo = PorCodigo(fatos);
        Dictionary<string, string> grupoDoCampo = grupos
            .SelectMany(static grupo => grupo.Subitens.Select(subitem => (Campo: subitem.FatoCodigo, Grupo: grupo.Codigo)))
            .ToDictionary(static campoEGrupo => campoEGrupo.Campo, static campoEGrupo => campoEGrupo.Grupo, StringComparer.Ordinal);
        return [.. porCodigo.Values
            .Where(fato => fato.FatoDeMembroAgregado is { } membro && grupoDoCampo.ContainsKey(membro) && porCodigo.ContainsKey(membro))
            .OrderBy(static fato => fato.Codigo, StringComparer.Ordinal)
            .Select(fato => (Agregado: fato.Codigo, Membro: fato.FatoDeMembroAgregado!,
                Operacao: AgregadoDeGrupo.OperacaoDoDominio(porCodigo[fato.FatoDeMembroAgregado!].Dominio)))
            .Where(static definicao => definicao.Operacao is not null)
            .Select(definicao => new DefinicaoAgregado(definicao.Agregado, grupoDoCampo[definicao.Membro], definicao.Membro, definicao.Operacao!.Value))];
    }

    /// <summary>
    /// As opções que o catálogo oferece a cada campo de seleção do modelo, na ordem de apresentação: os
    /// valores ativos do fato — os do fato de membro, no agregado — ou as UFs. Nulas quando as opções
    /// só existem no processo (as que ele declara, as modalidades, os municípios do FADA) ou vêm do
    /// Geo pela UF, e nos campos que não são categóricos.
    /// </summary>
    public static Dictionary<string, IReadOnlyList<ValorSelecionavel>?> OpcoesDoModelo(IEnumerable<FatoCandidato> fatos, ConteudoDoModelo conteudo)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(conteudo);
        Dictionary<string, FatoCandidato> porCodigo = PorCodigo(fatos);
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = new(StringComparer.Ordinal);
        foreach (string campo in conteudo.Campos().Select(static item => item.FatoCodigo))
        {
            opcoes[campo] = porCodigo.TryGetValue(campo, out FatoCandidato? fato) ? OpcoesDoCatalogo(fato, porCodigo) : null;
        }

        return opcoes;
    }

    private static List<ValorSelecionavel>? OpcoesDoCatalogo(FatoCandidato fato, IReadOnlyDictionary<string, FatoCandidato> porCodigo)
    {
        if (fato.Dominio != TipoDoDado.Opcao)
        {
            return null;
        }

        return fato.FonteValores switch
        {
            FonteValoresFato.Catalogo => OpcoesDosValoresAtivos(fato, porCodigo),
            FonteValoresFato.UnidadesFederativas => OpcoesDasUnidadesFederativas(),
            _ => null,
        };
    }

    private static List<ValorSelecionavel> OpcoesDosValoresAtivos(FatoCandidato fato, IReadOnlyDictionary<string, FatoCandidato> porCodigo) =>
        [.. ValoresDe(fato, porCodigo)
            .Where(static valor => valor.Ativo)
            .OrderBy(static valor => valor.Ordem)
            .ThenBy(static valor => valor.Codigo, StringComparer.Ordinal)
            .Select(static valor => new ValorSelecionavel(valor.Codigo, valor.Descricao, valor.Ordem, valor.Orientacao))];

    private static List<ValorSelecionavel> OpcoesDasUnidadesFederativas() =>
        [.. ReferenciaCidadeGeo.UnidadesFederativas.Select(static (uf, ordem) => new ValorSelecionavel(uf.Sigla, uf.Nome, ordem))];

    /// <summary>
    /// Os códigos que cada campo de seleção do modelo oferta, das opções do catálogo: a resposta fora
    /// deles não vale, na pré-visualização e no formulário apresentado.
    /// </summary>
    public static Dictionary<string, IReadOnlySet<string>> Ofertas(IReadOnlyDictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        return opcoes
            .Where(static campo => campo.Value is not null)
            .ToDictionary(static campo => campo.Key, static campo => (IReadOnlySet<string>)campo.Value!.Select(static valor => valor.Codigo).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static Dictionary<string, FatoCandidato> PorCodigo(IEnumerable<FatoCandidato> fatos) =>
        fatos.ToDictionary(static fato => fato.Codigo, StringComparer.Ordinal);

    /// <summary>
    /// As dependências de cada derivado que o catálogo sabe derivar: os fatos citados pelas regras
    /// padrão do derivado por regra, e os declarados pelo mecanismo do derivado do sistema. O derivado
    /// por regra sem regra padrão não entra — quem o deriva é o processo.
    /// </summary>
    public static Dictionary<string, IReadOnlyCollection<string>> Derivacoes(IEnumerable<FatoCandidato> fatos) =>
        DerivadosDoSistema.ComAsDoSistema(DerivacoesDosFatos(fatos).Select(static derivacao => KeyValuePair.Create(derivacao.CodigoFato, derivacao.DependenciasDeclaradas)));

    /// <summary>O formato da resposta de cada fato de texto, em token canônico.</summary>
    public static Dictionary<string, string> Formatos(IEnumerable<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        return fatos
            .Where(static fato => fato.Formato is not null)
            .ToDictionary(static fato => fato.Codigo, static fato => fato.Formato!.Value.Codigo(), StringComparer.Ordinal);
    }

    /// <summary>
    /// As regras de derivação que o catálogo sabe aplicar: as regras padrão de cada derivado por
    /// regra, booleano ou categórico, com as dependências citadas por elas. O catálogo as conferiu ao
    /// gravá-las, então uma recusa aqui é defeito.
    /// </summary>
    public static IReadOnlyList<DerivacaoDoFato> DerivacoesDosFatos(IEnumerable<FatoCandidato> fatos)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        return [.. fatos
            .Where(static fato => fato.RegrasPadrao.Count > 0 && VinculoDeFato.Usa(fato.Binding, MecanismoDoVinculo.RegraDeDerivacao))
            .Select(static fato =>
            {
                string[] dependencias = [.. fato.RegrasPadrao.SelectMany(static regra => regra.FatosCitados).Distinct(StringComparer.Ordinal)];
                Result<DerivacaoDoFato> derivacao = fato.Dominio == TipoDoDado.SimOuNao
                    ? DerivacaoDoFato.CriarBooleana(fato.Codigo, fato.RegrasPadrao, dependencias)
                    : DerivacaoDoFato.Criar(fato.Codigo, fato.RegrasPadrao, dependencias, [.. fato.ValoresDominioDeclarados.Select(static valor => valor.Codigo)]);
                return derivacao.IsSuccess
                    ? derivacao.Value!
                    : throw new InvalidOperationException($"As regras padrão gravadas de '{fato.Codigo}' não formam uma derivação: {derivacao.Error!.Message}");
            })];
    }
}
