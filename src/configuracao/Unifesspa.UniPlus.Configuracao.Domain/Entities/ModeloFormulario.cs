namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Uma etapa do modelo: uma seção com itens, ou um bloco que o sistema monta.</summary>
public sealed record EtapaDoModelo(
    string Codigo, int Ordem, TipoEtapaFormulario Tipo, BlocoSistema Bloco, string Titulo, string? Descricao, string? Aviso, PredicadoDnf? Exibicao)
{
    public IReadOnlyCollection<string> FatosCitados => Exibicao?.FatosCitados ?? [];
}

/// <summary>
/// Um item do modelo: o fato que o campo coleta, com as regras do item — exibição, obrigatoriedade,
/// restrições de valor e impedimento — sobre fatos anteriores (UNI-REQ-0145). O formato do campo de
/// texto é o do fato no catálogo; o impedimento cita também a resposta do próprio campo.
/// </summary>
public sealed record ItemDoModelo(
    string FatoCodigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    TipoRenderizacao TipoRenderizacao,
    string? Formato,
    string? Ajuda,
    Obrigatoriedade Obrigatoriedade,
    PredicadoDnf? Exibicao,
    IReadOnlyList<RestricaoValor> Restricoes,
    bool PedirConfirmacao,
    Impedimento? Impedimento = null)
{
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Concat(Restricoes.SelectMany(static r => r.FatosCitados))
            .Concat((Impedimento?.FatosCitados ?? []).Where(f => !string.Equals(f, FatoCodigo, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// Um grupo repetível do modelo (ADR-0138, UNI-REQ-0146): a lista de ocorrências de um mesmo conjunto
/// de campos de fatos de membro, como a composição familiar, com mínimo e, quando declarado, máximo
/// de ocorrências, e se o próprio candidato é um dos membros. A seção é a do grupo; os campos não
/// declaram seção própria.
/// </summary>
public sealed record GrupoDoModelo(
    string Codigo,
    int Ordem,
    string? EtapaCodigo,
    string Rotulo,
    int Minimo,
    int? Maximo,
    PredicadoDnf? Exibicao,
    Obrigatoriedade Obrigatoriedade,
    IReadOnlyList<ItemDoModelo> Subitens,
    bool IncluiCandidato = false)
{
    /// <summary>Os fatos que as regras do próprio grupo citam — a exibição e a obrigatoriedade.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Distinct(StringComparer.Ordinal)];
}

/// <summary>Um termo exigido pelo modelo: a versão do catálogo escolhida e as condições de exibição e de obrigatoriedade.</summary>
public sealed record TermoDoModelo(string Codigo, int Ordem, Guid TermoId, Guid VersaoId, PredicadoDnf? Exibicao, Obrigatoriedade Obrigatoriedade)
{
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// O conteúdo do modelo, lido e gravado inteiro: o título, as etapas, os itens, os termos, os fatos
/// pressupostos — coletados pela inscrição, que o modelo de outra finalidade pode citar (UNI-REQ-0144)
/// — e os grupos repetíveis.
/// </summary>
public sealed record ConteudoDoModelo(
    string? Titulo,
    IReadOnlyList<EtapaDoModelo> Etapas,
    IReadOnlyList<ItemDoModelo> Itens,
    IReadOnlyList<TermoDoModelo> Termos,
    IReadOnlyList<string> Pressupostos,
    IReadOnlyList<GrupoDoModelo> Grupos);

/// <summary>
/// Modelo de formulário composto pelo administrador para um tipo de processo — ou para todos — e uma
/// finalidade (UNI-REQ-0144, ADR-0137). O processo parte de um modelo e recebe uma cópia por valor
/// (ADR-0061): mudar o modelo depois não muda o processo.
/// </summary>
/// <remarks>
/// O modelo segue as mesmas regras do formulário do processo, pelo mesmo código de
/// <c>Unifesspa.UniPlus.Regras</c>: a forma do cabeçalho, das etapas, dos itens e dos termos, o teto de
/// itens, a estrutura por finalidade e o grafo de coleta. A conferência contra o catálogo de fatos e de
/// termos é de quem tem o catálogo; o modelo recebe dele só as dependências dos derivados por regra.
/// O código e a finalidade são imutáveis; vários modelos ativos convivem por tipo e finalidade.
/// </remarks>
public sealed class ModeloFormulario : EntityBase, IAuditableEntity
{
    public const int CodigoMaxLength = 60;
    public const int NomeMaxLength = 200;
    public const int DescricaoMaxLength = 1000;
    public const int TipoProcessoCodigoMaxLength = 64;

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public FinalidadeFormulario Finalidade { get; private set; }

    /// <summary>O tipo de processo a que o modelo se destina; nulo quando serve a todos.</summary>
    public string? TipoProcessoCodigo { get; private set; }

    public ConteudoDoModelo Conteudo { get; private set; } = new(null, [], [], [], [], []);
    public bool Ativo { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private ModeloFormulario() { }

    /// <summary>
    /// Cria o modelo desativado, acumulando toda violação no mesmo lote (ADR-0125). O modelo só entra
    /// na escolha de processos novos quando o administrador o ativa, depois de montá-lo. A unicidade do
    /// código e a conferência contra o catálogo são de quem chama.
    /// </summary>
    /// <param name="derivacoes">As dependências de cada derivado por regra do catálogo, para o grafo.</param>
    public static Result<ModeloFormulario> Criar(
        string? codigo,
        string? nome,
        string? descricao,
        FinalidadeFormulario finalidade,
        string? tipoProcessoCodigo,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(derivacoes);

        ConteudoDoModelo normalizado = Normalizar(conteudo);
        List<FieldError> erros = ConferirCadastro(codigo, nome, descricao, tipoProcessoCodigo, finalidade);
        erros.AddRange(NoConteudo(ConferirNormalizado(finalidade, normalizado, derivacoes)));
        if (erros.Count > 0)
        {
            return Result<ModeloFormulario>.ValidationFailure(erros);
        }

        ModeloFormulario modelo = new()
        {
            Codigo = Normalizar(codigo!),
            Finalidade = finalidade,
            Ativo = false,
        };
        modelo.Aplicar(nome ?? string.Empty, descricao, tipoProcessoCodigo, normalizado);
        return Result<ModeloFormulario>.Success(modelo);
    }

    /// <summary>Substitui o descritivo e o conteúdo; o código e a finalidade são imutáveis.</summary>
    public Result Atualizar(
        string? nome,
        string? descricao,
        string? tipoProcessoCodigo,
        ConteudoDoModelo conteudo,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(derivacoes);

        ConteudoDoModelo normalizado = Normalizar(conteudo);
        List<FieldError> erros = ConferirDescritivo(nome, descricao, tipoProcessoCodigo);
        erros.AddRange(NoConteudo(ConferirNormalizado(Finalidade, normalizado, derivacoes)));
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Aplicar(nome ?? string.Empty, descricao, tipoProcessoCodigo, normalizado);
        return Result.Success();
    }

    /// <summary>Reativar o que já está ativo é recusado: a operação é auditada, e aceitar o nada gravaria uma mudança que não houve.</summary>
    public Result Ativar()
    {
        if (Ativo)
        {
            return Result.Failure(new DomainError(ModeloFormularioErrorCodes.JaAtivo, "O modelo de formulário já está ativo."));
        }

        Ativo = true;
        return Result.Success();
    }

    /// <summary>Desativar tira o modelo da escolha de processos novos; os processos que já o copiaram não mudam.</summary>
    public Result Desativar()
    {
        if (!Ativo)
        {
            return Result.Failure(new DomainError(ModeloFormularioErrorCodes.JaDesativado, "O modelo de formulário já está desativado."));
        }

        Ativo = false;
        return Result.Success();
    }

    /// <summary>
    /// O modelo como o avaliador de formulário o lê: cada seção, na ordem, com os seus itens e grupos
    /// repetíveis — os blocos que o sistema monta não têm regra, como na definição do processo —; os termos; as derivações por regra do catálogo, que a pré-visualização resolve com as
    /// respostas simuladas; e os agregados do catálogo sobre os grupos do modelo.
    /// </summary>
    public DefinicaoFormulario ParaAvaliacao(IReadOnlyList<RegrasDerivacaoFato> derivacoes, IReadOnlyList<DefinicaoAgregado> agregados)
    {
        ArgumentNullException.ThrowIfNull(derivacoes);
        ArgumentNullException.ThrowIfNull(agregados);
        ILookup<string?, ItemDoModelo> itensPorEtapa = Conteudo.Itens.ToLookup(static i => i.EtapaCodigo, StringComparer.Ordinal);
        ILookup<string?, GrupoDoModelo> gruposPorEtapa = Conteudo.Grupos.ToLookup(static g => g.EtapaCodigo, StringComparer.Ordinal);
        return new DefinicaoFormulario(
            [.. Conteudo.Etapas.Where(static e => e.Tipo == TipoEtapaFormulario.Secao).OrderBy(static e => e.Ordem).Select(e => new DefinicaoEtapa(
                e.Codigo,
                e.Exibicao,
                [.. itensPorEtapa[e.Codigo].OrderBy(static i => i.Ordem).Select(Item)],
                [.. gruposPorEtapa[e.Codigo].OrderBy(static g => g.Ordem).Select(static g => new DefinicaoGrupo(
                    g.Codigo, g.Exibicao, g.Obrigatoriedade, g.Minimo, g.Maximo, [.. g.Subitens.OrderBy(static s => s.Ordem).Select(Item)], g.IncluiCandidato))]))],
            [.. Conteudo.Termos.OrderBy(static t => t.Ordem).Select(static t => new DefinicaoTermo(t.Codigo, t.Exibicao, t.Obrigatoriedade))],
            derivacoes,
            agregados);
    }

    private static DefinicaoItem Item(ItemDoModelo item) =>
        new(item.FatoCodigo, item.Exibicao, item.Obrigatoriedade, item.Restricoes, item.Impedimento, item.Formato);

    /// <summary>
    /// O que o cadastro confere fora do conteúdo — código, descritivo, tipo de processo e finalidade
    /// —, para que essas recusas saiam junto das do conteúdo mesmo quando ele não chega a ser lido.
    /// </summary>
    public static List<FieldError> ConferirCadastro(
        string? codigo, string? nome, string? descricao, string? tipoProcessoCodigo, FinalidadeFormulario finalidade) =>
        [.. ValidarCodigo(codigo), .. ValidarDescritivo(nome, descricao, tipoProcessoCodigo), .. FormaDoCabecalho.ValidarFinalidade(finalidade)];

    /// <summary>O que a edição confere fora do conteúdo: o descritivo e o tipo de processo.</summary>
    public static List<FieldError> ConferirDescritivo(string? nome, string? descricao, string? tipoProcessoCodigo) =>
        ValidarDescritivo(nome, descricao, tipoProcessoCodigo);

    /// <summary>O conteúdo na forma em que é gravado: o que se compara com o catálogo e com o modelo gravado.</summary>
    public static ConteudoDoModelo NaFormaGravada(ConteudoDoModelo conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        return Normalizar(conteudo);
    }

    /// <summary>O código na forma em que é gravado: aparado e em NFC.</summary>
    public static string CodigoNaFormaGravada(string? codigo) => Normalizar(codigo);

    /// <summary>As recusas do conteúdo apontam o campo dentro dele, no caminho da escrita.</summary>
    public static IEnumerable<FieldError> NoConteudo(IEnumerable<FieldError> erros) =>
        erros.Select(static e => e with { Field = string.IsNullOrEmpty(e.Field) ? "conteudo" : $"conteudo.{e.Field}" });

    /// <summary>
    /// O cabeçalho, a forma de cada etapa, item, grupo e termo, o teto de itens e os pressupostos acumulam; a
    /// estrutura por finalidade e o grafo de coleta só são conferidos sobre partes bem formadas, porque
    /// dependem delas.
    /// </summary>
    private static List<FieldError> ConferirNormalizado(
        FinalidadeFormulario finalidade, ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        List<FieldError> erros = [.. TextosNaoGravaveis(conteudo)];
        erros.AddRange(FormaDoCabecalho.ValidarTitulo(conteudo.Titulo));

        for (int i = 0; i < conteudo.Etapas.Count; i++)
        {
            EtapaDoModelo etapa = conteudo.Etapas[i];
            erros.AddRange(FormaDaEtapa.Conferir(etapa.Codigo, etapa.Ordem, etapa.Tipo, etapa.Titulo, etapa.Descricao, etapa.Aviso, etapa.Exibicao is not null)
                .Select(e => e with { Field = $"etapas[{i}].{e.Field}" }));
        }

        List<FieldError> excesso = FormaDoItem.ValidarQuantidade(
            FormaDoItem.QuantidadeNoTeto(conteudo.Itens.Count, conteudo.Grupos.Select(static g => g.Subitens.Count)));
        erros.AddRange(excesso);
        if (excesso.Count == 0)
        {
            for (int i = 0; i < conteudo.Itens.Count; i++)
            {
                erros.AddRange(FormaDoCampo(conteudo.Itens[i], $"itens[{i}]"));
            }

            erros.AddRange(Impedimento.ConferirFinalidade(finalidade, conteudo.Itens.Select(static i => i.Impedimento)));

            for (int i = 0; i < conteudo.Grupos.Count; i++)
            {
                GrupoDoModelo grupo = conteudo.Grupos[i];
                erros.AddRange(FormaDoGrupo.Conferir(
                        grupo.Codigo, grupo.Ordem, grupo.Rotulo, grupo.Minimo, grupo.Maximo,
                        [.. grupo.Subitens.Select(static s => ((string?)s.FatoCodigo, s.EtapaCodigo, s.Impedimento is not null))],
                        grupo.Exibicao?.FatosCitados ?? [], grupo.Obrigatoriedade)
                    .Select(e => e with { Field = $"grupos[{i}].{e.Field}" }));
                if (grupo.IncluiCandidato)
                {
                    erros.AddRange(CandidatoComoMembro.Conferir(
                            grupo.Minimo, [.. grupo.Subitens.Select(static s => (s.FatoCodigo, s.Exibicao is not null, s.Obrigatoriedade, s.Restricoes))])
                        .Select(e => e with { Field = $"grupos[{i}].{e.Field}" }));
                }

                for (int j = 0; j < grupo.Subitens.Count; j++)
                {
                    erros.AddRange(FormaDoCampo(grupo.Subitens[j], $"grupos[{i}].subitens[{j}]"));
                }
            }
        }

        for (int i = 0; i < conteudo.Termos.Count; i++)
        {
            erros.AddRange(FormaDoTermo.ValidarFormaBasica(conteudo.Termos[i].Codigo, conteudo.Termos[i].Ordem)
                .Select(e => e with { Field = $"termos[{i}].{e.Field}" }));
        }

        erros.AddRange(FormaDoTermo.ConferirUnicidade([.. conteudo.Termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]));
        erros.AddRange(ConferirPressupostos(finalidade, conteudo));

        // A estrutura depende da finalidade, recusada no cadastro.
        if (erros.Count > 0 || FormaDoCabecalho.ValidarFinalidade(finalidade).Count > 0)
        {
            return erros;
        }

        EtapaEstrutura[] estrutura = [.. conteudo.Etapas.Select(static e => new EtapaEstrutura(e.Codigo, e.Ordem, e.Tipo, e.Bloco))];
        erros.AddRange(EstruturaFormulario.ValidarEtapas(finalidade, estrutura));
        if (erros.Count > 0)
        {
            return erros;
        }

        erros.AddRange(EstruturaFormulario.ValidarItens(
            estrutura,
            [
                .. conteudo.Itens.Select(static i => new ItemEstrutura(i.FatoCodigo, i.Ordem, i.EtapaCodigo)),
                .. conteudo.Grupos.Select(static g => new ItemEstrutura(g.Codigo, g.Ordem, g.EtapaCodigo)),
            ],
            secaoObrigatoria: true));
        if (erros.Count > 0)
        {
            return erros;
        }

        if (ConferirGrafo(conteudo, derivacoes) is { } recusa)
        {
            erros.Add(new(string.Empty, recusa));
        }

        return erros;
    }

    /// <summary>A forma de um campo do modelo, item ou campo de grupo, no caminho dele.</summary>
    private static IEnumerable<FieldError> FormaDoCampo(ItemDoModelo campo, string caminho) =>
        FormaDoItem.Conferir(
                campo.FatoCodigo, campo.Ordem, campo.Rotulo, campo.TipoRenderizacao, campo.Formato, campo.Ajuda,
                campo.Exibicao?.FatosCitados ?? [], campo.Obrigatoriedade, campo.Restricoes, campo.Impedimento)
            .Select(e => e with { Field = $"{caminho}.{e.Field}" });

    /// <summary>
    /// O grafo de coleta pelas regras do formulário: os pressupostos são os fatos conhecidos antes dele,
    /// e as derivações, as do catálogo. Depois, o campo opcional que alimenta derivação ou negação.
    /// </summary>
    private static DomainError? ConferirGrafo(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ItemDoGrafo[] itens = [.. conteudo.Itens.Select(ParaGrafo)];
        EtapaDoGrafo[] etapas = [.. conteudo.Etapas.Select(static e => new EtapaDoGrafo(e.Codigo, e.Ordem, e.FatosCitados))];
        GrupoDoGrafo[] grupos = [.. conteudo.Grupos.Select(static g => new GrupoDoGrafo(
            g.Codigo, g.Ordem, g.EtapaCodigo, g.FatosCitados, [.. g.Subitens.OrderBy(static s => s.Ordem).Select(ParaGrafo)]))];
        HashSet<string> pressupostos = new(conteudo.Pressupostos, StringComparer.Ordinal);

        if (GrafoDoFormulario.ValidarColeta(itens, etapas, pressupostos, derivacoes, grupos) is { } coleta)
        {
            return coleta;
        }

        DependenciasDoFormulario dependencias = GrafoDoFormulario.Dependencias(itens, pressupostos, derivacoes);
        if (conteudo.Termos.Select(t => GrafoDoFormulario.CitacaoInvalidaDoTermo(new TermoDoGrafo(t.Codigo, t.FatosCitados), dependencias))
                .FirstOrDefault(static e => e is not null) is { } termo)
        {
            return termo;
        }

        return CampoQueAlimentaRegra.PrimeiroOpcional(
            conteudo.Itens.OrderBy(static i => i.Ordem)
                .Concat(conteudo.Grupos.OrderBy(static g => g.Ordem).SelectMany(static g => g.Subitens.OrderBy(static s => s.Ordem)))
                .Select(static i => (i.FatoCodigo, i.Obrigatoriedade.Tipo)),
            CampoQueAlimentaRegra.Fatos(
                DependenciasCitadas(conteudo, derivacoes), CondicoesDasRegras(conteudo),
                conteudo.Itens.Where(static i => i.Impedimento is not null).Select(static i => i.FatoCodigo)));
    }

    private static ItemDoGrafo ParaGrafo(ItemDoModelo campo) => new(campo.FatoCodigo, campo.Ordem, campo.EtapaCodigo, campo.FatosCitados);

    /// <summary>Os campos do modelo, itens e campos de grupo.</summary>
    private static IEnumerable<ItemDoModelo> Campos(ConteudoDoModelo conteudo) =>
        conteudo.Itens.Concat(conteudo.Grupos.SelectMany(static g => g.Subitens));

    /// <summary>
    /// As dependências dos derivados que as regras do modelo citam, direta ou transitivamente — só elas
    /// alimentam regra no formulário, e não todas as derivações do catálogo.
    /// </summary>
    private static List<string> DependenciasCitadas(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        HashSet<string> visitados = new(StringComparer.Ordinal);
        Stack<string> pendentes = new(TodosOsCitados(conteudo));
        List<string> dependencias = [];
        while (pendentes.TryPop(out string? fato))
        {
            if (!visitados.Add(fato) || !derivacoes.TryGetValue(fato, out IReadOnlyCollection<string>? deps))
            {
                continue;
            }

            foreach (string dependencia in deps)
            {
                dependencias.Add(dependencia);
                pendentes.Push(dependencia);
            }
        }

        return dependencias;
    }

    private static IEnumerable<string> TodosOsCitados(ConteudoDoModelo conteudo) =>
        Campos(conteudo).SelectMany(static i => i.FatosCitados)
            .Concat(conteudo.Grupos.SelectMany(static g => g.FatosCitados))
            .Concat(conteudo.Etapas.SelectMany(static e => e.FatosCitados))
            .Concat(conteudo.Termos.SelectMany(static t => t.FatosCitados));

    /// <summary>As condições de todas as regras do modelo, com o operador — a negação é o que importa aqui.</summary>
    private static IEnumerable<(string Fato, Operador Operador)> CondicoesDasRegras(ConteudoDoModelo conteudo)
    {
        IEnumerable<PredicadoDnf?> predicados = Campos(conteudo)
            .SelectMany(static i => new[] { i.Exibicao, i.Obrigatoriedade.Predicado, i.Impedimento?.Quando }
                .Concat(i.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas.Select(static e => e.Quando))))
            .Concat(conteudo.Grupos.SelectMany(static g => new[] { g.Exibicao, g.Obrigatoriedade.Predicado }))
            .Concat(conteudo.Etapas.Select(static e => e.Exibicao))
            .Concat(conteudo.Termos.SelectMany(static t => new[] { t.Exibicao, t.Obrigatoriedade.Predicado }));
        return predicados.OfType<PredicadoDnf>()
            .SelectMany(static p => p.Clausulas)
            .SelectMany(static c => c.Condicoes)
            .Select(static c => (c.Fato, c.Operador));
    }

    /// <summary>
    /// Pressuposto é fato coletado pela inscrição (UNI-REQ-0144): o modelo de inscrição não tem, e o
    /// fato não é pressuposto e item do mesmo modelo.
    /// </summary>
    private static IEnumerable<FieldError> ConferirPressupostos(FinalidadeFormulario finalidade, ConteudoDoModelo conteudo)
    {
        if (finalidade == FinalidadeFormulario.Inscricao && conteudo.Pressupostos.Count > 0)
        {
            yield return new("pressupostos", new DomainError(
                ModeloFormularioErrorCodes.PressupostoNaInscricao,
                "O modelo de inscrição não tem pressupostos: nenhum formulário é respondido antes dele."));
            yield break;
        }

        HashSet<string> vistos = new(StringComparer.Ordinal);
        HashSet<string> itens = new(conteudo.Itens.Select(static i => i.FatoCodigo), StringComparer.Ordinal);
        for (int i = 0; i < conteudo.Pressupostos.Count; i++)
        {
            string fato = conteudo.Pressupostos[i];
            if (fato.Length == 0)
            {
                yield return new($"pressupostos[{i}]", new DomainError(
                    ModeloFormularioErrorCodes.PressupostoEmBranco, "O pressuposto nomeia um fato."));
                continue;
            }

            if (!vistos.Add(fato))
            {
                yield return new($"pressupostos[{i}]", new DomainError(
                    ModeloFormularioErrorCodes.PressupostoRepetido, $"O fato '{fato}' aparece mais de uma vez entre os pressupostos."));
            }

            if (itens.Contains(fato))
            {
                yield return new($"pressupostos[{i}]", new DomainError(
                    ModeloFormularioErrorCodes.PressupostoTambemItem,
                    $"O fato '{fato}' é item do modelo e não pode ser também pressuposto: ou o formulário o coleta, ou o recebe da inscrição."));
            }
        }
    }

    private static List<FieldError> ValidarCodigo(string? codigo)
    {
        List<FieldError> erros = [.. NaoGravavel("codigo", codigo)];
        if (string.IsNullOrWhiteSpace(codigo))
        {
            erros.Add(new("codigo", new DomainError(ModeloFormularioErrorCodes.CodigoObrigatorio, "O código do modelo é obrigatório.")));
        }
        else if (Normalizar(codigo).Length > CodigoMaxLength)
        {
            erros.Add(new("codigo", new DomainError(
                ModeloFormularioErrorCodes.CodigoTamanho, $"O código do modelo deve ter no máximo {CodigoMaxLength} caracteres.")));
        }

        return erros;
    }

    private static List<FieldError> ValidarDescritivo(string? nome, string? descricao, string? tipoProcessoCodigo)
    {
        List<FieldError> erros = [.. NaoGravavel("nome", nome), .. NaoGravavel("descricao", descricao), .. NaoGravavel("tipoProcessoCodigo", tipoProcessoCodigo)];
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(ModeloFormularioErrorCodes.NomeObrigatorio, "O nome do modelo é obrigatório.")));
        }
        else if (nome.Trim().Length > NomeMaxLength)
        {
            erros.Add(new("nome", new DomainError(
                ModeloFormularioErrorCodes.NomeTamanho, $"O nome do modelo deve ter no máximo {NomeMaxLength} caracteres.")));
        }

        if (FormaDoItem.TextoOpcional(descricao) is { Length: > DescricaoMaxLength })
        {
            erros.Add(new("descricao", new DomainError(
                ModeloFormularioErrorCodes.DescricaoTamanho, $"A descrição do modelo deve ter no máximo {DescricaoMaxLength} caracteres.")));
        }

        if (tipoProcessoCodigo is not null && string.IsNullOrWhiteSpace(tipoProcessoCodigo))
        {
            // Só espaços viraria nulo, que é "todos os tipos": o modelo mudaria de alcance sem que ninguém o pedisse.
            erros.Add(new("tipoProcessoCodigo", new DomainError(
                ModeloFormularioErrorCodes.TipoProcessoCodigoEmBranco,
                "O tipo de processo é um código, ou ausente quando o modelo serve a todos os tipos.")));
        }
        else if (FormaDoItem.TextoOpcional(tipoProcessoCodigo) is { Length: > TipoProcessoCodigoMaxLength })
        {
            erros.Add(new("tipoProcessoCodigo", new DomainError(
                ModeloFormularioErrorCodes.TipoProcessoCodigoTamanho,
                $"O código do tipo de processo deve ter no máximo {TipoProcessoCodigoMaxLength} caracteres.")));
        }

        return erros;
    }

    /// <summary>Grava o descritivo e o conteúdo já normalizado, com etapas, itens, grupos, campos de grupo e termos na ordem declarada e as restrições de cada campo na ordem do tipo.</summary>
    private void Aplicar(string nome, string? descricao, string? tipoProcessoCodigo, ConteudoDoModelo conteudo)
    {
        Nome = nome.Trim();
        Descricao = FormaDoItem.TextoOpcional(descricao);
        TipoProcessoCodigo = FormaDoItem.TextoOpcional(tipoProcessoCodigo);
        Conteudo = conteudo with
        {
            Etapas = [.. conteudo.Etapas.OrderBy(static e => e.Ordem)],
            Itens = [.. conteudo.Itens.OrderBy(static i => i.Ordem).Select(ComRestricoesOrdenadas)],
            Termos = [.. conteudo.Termos.OrderBy(static t => t.Ordem)],
            Grupos = [.. conteudo.Grupos.OrderBy(static g => g.Ordem)
                .Select(static g => g with { Subitens = [.. g.Subitens.OrderBy(static s => s.Ordem).Select(ComRestricoesOrdenadas)] })],
        };
    }

    private static ItemDoModelo ComRestricoesOrdenadas(ItemDoModelo campo) =>
        campo with { Restricoes = [.. campo.Restricoes.OrderBy(static r => r.Tipo)] };

    /// <summary>
    /// A forma em que o conteúdo é conferido e gravado: textos aparados, códigos em NFC, ausente como
    /// vazio — a forma do item recusa o vazio. Mantém as posições, inclusive das restrições, para que
    /// cada recusa aponte o índice que veio.
    /// </summary>
    private static ConteudoDoModelo Normalizar(ConteudoDoModelo conteudo) => new(
        FormaDoItem.TextoOpcional(conteudo.Titulo),
        [.. (conteudo.Etapas ?? []).Select(static e => e with
        {
            Codigo = Normalizar(e.Codigo),
            Titulo = (e.Titulo ?? string.Empty).Trim(),
            Descricao = FormaDoItem.TextoOpcional(e.Descricao),
            Aviso = FormaDoItem.TextoOpcional(e.Aviso),
        })],
        [.. (conteudo.Itens ?? []).Select(NormalizarCampo)],
        [.. (conteudo.Termos ?? []).Select(static t => t with { Codigo = Normalizar(t.Codigo) })],
        [.. (conteudo.Pressupostos ?? []).Select(static p => Normalizar(p))],
        [.. (conteudo.Grupos ?? []).Select(static g => g with
        {
            Codigo = Normalizar(g.Codigo),
            EtapaCodigo = g.EtapaCodigo is null ? null : Normalizar(g.EtapaCodigo),
            Rotulo = (g.Rotulo ?? string.Empty).Trim(),
            Subitens = [.. (g.Subitens ?? []).Select(NormalizarCampo)],
        })]);

    private static ItemDoModelo NormalizarCampo(ItemDoModelo campo) => campo with
    {
        FatoCodigo = Normalizar(campo.FatoCodigo),
        EtapaCodigo = campo.EtapaCodigo is null ? null : Normalizar(campo.EtapaCodigo),
        Rotulo = (campo.Rotulo ?? string.Empty).Trim(),
        Formato = FormaDoItem.TextoOpcional(campo.Formato),
        Ajuda = FormaDoItem.TextoOpcional(campo.Ajuda),
        Restricoes = campo.Restricoes ?? [],
    };

    /// <summary>O código aparado e em NFC; o que não se normaliza fica como veio, para a recusa de texto não gravável.</summary>
    private static string Normalizar(string? codigo)
    {
        string aparado = (codigo ?? string.Empty).Trim();
        return TextoNormalizavel.TentarNormalizar(aparado, out string normalizado) ? normalizado : aparado;
    }

    /// <summary>
    /// Os textos do conteúdo que o banco não grava — com caractere nulo, ou que não são Unicode
    /// válido —, inclusive os valores e os fatos citados pelas regras: o conteúdo é um documento
    /// jsonb, e o Postgres o recusaria só na gravação.
    /// </summary>
    private static IEnumerable<FieldError> TextosNaoGravaveis(ConteudoDoModelo conteudo)
    {
        IEnumerable<(string Campo, string? Texto)> textos = new (string, string?)[] { ("titulo", conteudo.Titulo) }
            .Concat(conteudo.Etapas.SelectMany(static (e, i) => new (string, string?)[]
                {
                    ($"etapas[{i}].codigo", e.Codigo), ($"etapas[{i}].titulo", e.Titulo),
                    ($"etapas[{i}].descricao", e.Descricao), ($"etapas[{i}].aviso", e.Aviso),
                }.Concat(TextosDoPredicado($"etapas[{i}].exibicao", e.Exibicao))))
            .Concat(conteudo.Itens.SelectMany(static (item, i) => TextosDoCampo($"itens[{i}]", item)))
            .Concat(conteudo.Grupos.SelectMany(static (g, i) => new (string, string?)[]
                {
                    ($"grupos[{i}].codigo", g.Codigo), ($"grupos[{i}].etapaCodigo", g.EtapaCodigo), ($"grupos[{i}].rotulo", g.Rotulo),
                }
                .Concat(TextosDoPredicado($"grupos[{i}].exibicao", g.Exibicao))
                .Concat(TextosDoPredicado($"grupos[{i}].obrigatoriedade", g.Obrigatoriedade.Predicado))
                .Concat(g.Subitens.SelectMany((s, j) => TextosDoCampo($"grupos[{i}].subitens[{j}]", s)))))
            .Concat(conteudo.Termos.SelectMany(static (t, i) => new (string, string?)[] { ($"termos[{i}].codigo", t.Codigo) }
                .Concat(TextosDoPredicado($"termos[{i}].exibicao", t.Exibicao))
                .Concat(TextosDoPredicado($"termos[{i}].obrigatoriedade", t.Obrigatoriedade.Predicado))))
            .Concat(conteudo.Pressupostos.Select(static (p, i) => ($"pressupostos[{i}]", (string?)p)));
        return textos.SelectMany(static t => NaoGravavel(t.Campo, t.Texto)).DistinctBy(static e => e.Field);
    }

    private static IEnumerable<(string Campo, string? Texto)> TextosDoCampo(string caminho, ItemDoModelo campo) =>
        new (string, string?)[]
        {
            ($"{caminho}.fatoCodigo", campo.FatoCodigo), ($"{caminho}.etapaCodigo", campo.EtapaCodigo),
            ($"{caminho}.rotulo", campo.Rotulo), ($"{caminho}.formato", campo.Formato), ($"{caminho}.ajuda", campo.Ajuda),
        }
        .Concat(TextosDoPredicado($"{caminho}.exibicao", campo.Exibicao))
        .Concat(TextosDoPredicado($"{caminho}.obrigatoriedade", campo.Obrigatoriedade.Predicado))
        .Concat(campo.Restricoes.SelectMany((r, j) => TextosDaRestricao($"{caminho}.restricoes[{j}]", r)))
        .Concat(campo.Impedimento is { } impedimento
            ? TextosDoPredicado($"{caminho}.impedimento.quando", impedimento.Quando).Append(($"{caminho}.impedimento.mensagem", impedimento.Mensagem))
            : []);

    private static IEnumerable<(string Campo, string? Texto)> TextosDaRestricao(string campo, RestricaoValor restricao) => restricao switch
    {
        OpcoesPermitidas opcoes => opcoes.Entradas.SelectMany((e, k) => e.Valores.Select(v => ($"{campo}.entradas[{k}].valores", (string?)v))
            .Concat(TextosDoPredicado($"{campo}.entradas[{k}].quando", e.Quando))),
        OpcoesDasRespostas respostas => respostas.Fatos.Select(f => ($"{campo}.fatos", (string?)f)),
        MunicipiosDaUf daUf => [($"{campo}.fatos", daUf.FatoUf)],
        _ => [],
    };

    private static IEnumerable<(string Campo, string? Texto)> TextosDoPredicado(string campo, PredicadoDnf? predicado) =>
        (predicado?.Clausulas ?? []).SelectMany(static c => c.Condicoes)
            .SelectMany(c => new[] { (campo, (string?)c.Fato) }.Concat(TextosDoValor(c.Valor).Select(v => (campo, (string?)v))));

    private static IEnumerable<string> TextosDoValor(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.String => [valor.GetString()!],
        JsonValueKind.Array => valor.EnumerateArray().SelectMany(TextosDoValor),
        JsonValueKind.Object => valor.EnumerateObject().SelectMany(static p => TextosDoValor(p.Value).Prepend(p.Name)),
        _ => [],
    };

    /// <summary>
    /// O texto que o banco grava: sem caractere nulo e Unicode válido. Quem consulta o banco com um
    /// texto recebido confere isto antes, para a recusa ser a do modelo e não um erro do banco.
    /// </summary>
    public static bool EhGravavel(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return !texto.Contains('\0', StringComparison.Ordinal) && TextoNormalizavel.TentarNormalizar(texto, out _);
    }

    private static IEnumerable<FieldError> NaoGravavel(string campo, string? texto)
    {
        if (texto is not null && !EhGravavel(texto))
        {
            yield return new(campo, new DomainError(
                ModeloFormularioErrorCodes.TextoNaoGravavel, "O texto contém o caractere nulo (U+0000) ou não é Unicode válido."));
        }
    }
}
