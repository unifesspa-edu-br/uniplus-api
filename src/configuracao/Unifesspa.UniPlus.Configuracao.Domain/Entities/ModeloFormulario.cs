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
/// Um item do modelo: o fato que o campo coleta, com as regras do item — exibição, obrigatoriedade e
/// restrições de valor — sobre fatos anteriores (UNI-REQ-0145). O formato do campo de texto é o do
/// fato no catálogo.
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
    bool PedirConfirmacao)
{
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Concat(Restricoes.SelectMany(static r => r.FatosCitados))
            .Distinct(StringComparer.Ordinal)];
}

/// <summary>Um termo exigido pelo modelo: a versão do catálogo escolhida e as condições de exibição e de obrigatoriedade.</summary>
public sealed record TermoDoModelo(string Codigo, int Ordem, Guid TermoId, Guid VersaoId, PredicadoDnf? Exibicao, Obrigatoriedade Obrigatoriedade)
{
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? []).Concat(Obrigatoriedade.FatosCitados).Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// O conteúdo do modelo, lido e gravado inteiro: o título, as etapas, os itens, os termos e os fatos
/// pressupostos — coletados pela inscrição, que o modelo de outra finalidade pode citar (UNI-REQ-0144).
/// </summary>
public sealed record ConteudoDoModelo(
    string? Titulo,
    IReadOnlyList<EtapaDoModelo> Etapas,
    IReadOnlyList<ItemDoModelo> Itens,
    IReadOnlyList<TermoDoModelo> Termos,
    IReadOnlyList<string> Pressupostos);

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

    public ConteudoDoModelo Conteudo { get; private set; } = new(null, [], [], [], []);
    public bool Ativo { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private ModeloFormulario() { }

    /// <summary>
    /// Cria o modelo, ativo, acumulando toda violação no mesmo lote (ADR-0125). A unicidade do código
    /// e a conferência contra o catálogo são de quem chama.
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
        List<FieldError> erros = ValidarCodigo(codigo);
        erros.AddRange(ValidarDescritivo(nome, descricao, tipoProcessoCodigo));
        erros.AddRange(ConferirNormalizado(finalidade, normalizado, derivacoes));
        if (erros.Count > 0)
        {
            return Result<ModeloFormulario>.ValidationFailure(erros);
        }

        ModeloFormulario modelo = new()
        {
            Codigo = Normalizar(codigo!),
            Finalidade = finalidade,
            Ativo = true,
        };
        modelo.Aplicar(nome!, descricao, tipoProcessoCodigo, normalizado);
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
        List<FieldError> erros = ValidarDescritivo(nome, descricao, tipoProcessoCodigo);
        erros.AddRange(ConferirNormalizado(Finalidade, normalizado, derivacoes));
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        Aplicar(nome!, descricao, tipoProcessoCodigo, normalizado);
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
    /// O conteúdo pelas regras do formulário, conferido na forma em que é gravado: textos aparados e
    /// códigos em NFC — senão dois códigos que só diferem por espaço passariam como distintos e seriam
    /// gravados repetidos.
    /// </summary>
    public static List<FieldError> ConferirConteudo(
        FinalidadeFormulario finalidade, ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(derivacoes);
        return ConferirNormalizado(finalidade, Normalizar(conteudo), derivacoes);
    }

    /// <summary>
    /// O cabeçalho, a forma de cada etapa, item e termo, o teto de itens e os pressupostos acumulam; a
    /// estrutura por finalidade e o grafo de coleta só são conferidos sobre partes bem formadas, porque
    /// dependem delas.
    /// </summary>
    private static List<FieldError> ConferirNormalizado(
        FinalidadeFormulario finalidade, ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        List<FieldError> erros = [.. TextosNaoGravaveis(conteudo)];
        erros.AddRange(FormaDoCabecalho.Validar(finalidade, conteudo.Titulo));

        for (int i = 0; i < conteudo.Etapas.Count; i++)
        {
            EtapaDoModelo etapa = conteudo.Etapas[i];
            erros.AddRange(FormaDaEtapa.Conferir(etapa.Codigo, etapa.Ordem, etapa.Tipo, etapa.Titulo, etapa.Descricao, etapa.Aviso, etapa.Exibicao is not null)
                .Select(e => e with { Field = $"etapas[{i}].{e.Field}" }));
        }

        List<FieldError> excesso = FormaDoItem.ValidarQuantidade(conteudo.Itens.Count);
        erros.AddRange(excesso);
        if (excesso.Count == 0)
        {
            for (int i = 0; i < conteudo.Itens.Count; i++)
            {
                ItemDoModelo item = conteudo.Itens[i];
                erros.AddRange(FormaDoItem.Conferir(
                        item.FatoCodigo, item.Ordem, item.Rotulo, item.TipoRenderizacao, item.Formato, item.Ajuda,
                        item.Exibicao?.FatosCitados ?? [], item.Obrigatoriedade, item.Restricoes)
                    .Select(e => e with { Field = $"itens[{i}].{e.Field}" }));
            }
        }

        for (int i = 0; i < conteudo.Termos.Count; i++)
        {
            erros.AddRange(FormaDoTermo.ValidarFormaBasica(conteudo.Termos[i].Codigo, conteudo.Termos[i].Ordem)
                .Select(e => e with { Field = $"termos[{i}].{e.Field}" }));
        }

        erros.AddRange(FormaDoTermo.ConferirUnicidade([.. conteudo.Termos.Select(static t => ((string?, int)?)(t.Codigo, t.Ordem))]));
        erros.AddRange(ConferirPressupostos(finalidade, conteudo));

        if (erros.Count > 0)
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
            estrutura, [.. conteudo.Itens.Select(static i => new ItemEstrutura(i.FatoCodigo, i.Ordem, i.EtapaCodigo))], secaoObrigatoria: true));
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

    /// <summary>
    /// O grafo de coleta pelas regras do formulário: os pressupostos são os fatos conhecidos antes dele,
    /// e as derivações, as do catálogo. Depois, o campo opcional que alimenta derivação ou negação.
    /// </summary>
    private static DomainError? ConferirGrafo(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ItemDoGrafo[] itens = [.. conteudo.Itens.Select(static i => new ItemDoGrafo(i.FatoCodigo, i.Ordem, i.EtapaCodigo, i.FatosCitados))];
        EtapaDoGrafo[] etapas = [.. conteudo.Etapas.Select(static e => new EtapaDoGrafo(e.Codigo, e.Ordem, e.FatosCitados))];
        HashSet<string> pressupostos = new(conteudo.Pressupostos, StringComparer.Ordinal);

        if (GrafoDoFormulario.ValidarColeta(itens, etapas, pressupostos, derivacoes) is { } coleta)
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
            conteudo.Itens.OrderBy(static i => i.Ordem).Select(static i => (i.FatoCodigo, i.Obrigatoriedade.Tipo)),
            CampoQueAlimentaRegra.Fatos(DependenciasCitadas(conteudo, derivacoes), CondicoesDasRegras(conteudo)));
    }

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
        conteudo.Itens.SelectMany(static i => i.FatosCitados)
            .Concat(conteudo.Etapas.SelectMany(static e => e.FatosCitados))
            .Concat(conteudo.Termos.SelectMany(static t => t.FatosCitados));

    /// <summary>As condições de todas as regras do modelo, com o operador — a negação é o que importa aqui.</summary>
    private static IEnumerable<(string Fato, Operador Operador)> CondicoesDasRegras(ConteudoDoModelo conteudo)
    {
        IEnumerable<PredicadoDnf?> predicados = conteudo.Itens
            .SelectMany(static i => new[] { i.Exibicao, i.Obrigatoriedade.Predicado }
                .Concat(i.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas.Select(static e => e.Quando))))
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

        if (FormaDoItem.TextoOpcional(tipoProcessoCodigo) is { Length: > TipoProcessoCodigoMaxLength })
        {
            erros.Add(new("tipoProcessoCodigo", new DomainError(
                ModeloFormularioErrorCodes.TipoProcessoCodigoTamanho,
                $"O código do tipo de processo deve ter no máximo {TipoProcessoCodigoMaxLength} caracteres.")));
        }

        return erros;
    }

    /// <summary>Grava o descritivo e o conteúdo já normalizado, com etapas, itens e termos na ordem declarada e as restrições de cada item na ordem do tipo.</summary>
    private void Aplicar(string nome, string? descricao, string? tipoProcessoCodigo, ConteudoDoModelo conteudo)
    {
        Nome = nome.Trim();
        Descricao = FormaDoItem.TextoOpcional(descricao);
        TipoProcessoCodigo = FormaDoItem.TextoOpcional(tipoProcessoCodigo);
        Conteudo = conteudo with
        {
            Etapas = [.. conteudo.Etapas.OrderBy(static e => e.Ordem)],
            Itens = [.. conteudo.Itens.OrderBy(static i => i.Ordem).Select(static i => i with { Restricoes = [.. i.Restricoes.OrderBy(static r => r.Tipo)] })],
            Termos = [.. conteudo.Termos.OrderBy(static t => t.Ordem)],
        };
    }

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
        [.. (conteudo.Itens ?? []).Select(static i => i with
        {
            FatoCodigo = Normalizar(i.FatoCodigo),
            EtapaCodigo = i.EtapaCodigo is null ? null : Normalizar(i.EtapaCodigo),
            Rotulo = (i.Rotulo ?? string.Empty).Trim(),
            Formato = FormaDoItem.TextoOpcional(i.Formato),
            Ajuda = FormaDoItem.TextoOpcional(i.Ajuda),
            Restricoes = i.Restricoes ?? [],
        })],
        [.. (conteudo.Termos ?? []).Select(static t => t with { Codigo = Normalizar(t.Codigo) })],
        [.. (conteudo.Pressupostos ?? []).Select(static p => Normalizar(p))]);

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
            .Concat(conteudo.Itens.SelectMany(static (item, i) => new (string, string?)[]
                {
                    ($"itens[{i}].fatoCodigo", item.FatoCodigo), ($"itens[{i}].etapaCodigo", item.EtapaCodigo),
                    ($"itens[{i}].rotulo", item.Rotulo), ($"itens[{i}].formato", item.Formato), ($"itens[{i}].ajuda", item.Ajuda),
                }
                .Concat(TextosDoPredicado($"itens[{i}].exibicao", item.Exibicao))
                .Concat(TextosDoPredicado($"itens[{i}].obrigatoriedade", item.Obrigatoriedade.Predicado))
                .Concat(item.Restricoes.SelectMany((r, j) => TextosDaRestricao($"itens[{i}].restricoes[{j}]", r)))))
            .Concat(conteudo.Termos.SelectMany(static (t, i) => new (string, string?)[] { ($"termos[{i}].codigo", t.Codigo) }
                .Concat(TextosDoPredicado($"termos[{i}].exibicao", t.Exibicao))
                .Concat(TextosDoPredicado($"termos[{i}].obrigatoriedade", t.Obrigatoriedade.Predicado))))
            .Concat(conteudo.Pressupostos.Select(static (p, i) => ($"pressupostos[{i}]", (string?)p)));
        return textos.SelectMany(static t => NaoGravavel(t.Campo, t.Texto)).DistinctBy(static e => e.Field);
    }

    private static IEnumerable<(string Campo, string? Texto)> TextosDaRestricao(string campo, RestricaoValor restricao) => restricao switch
    {
        OpcoesPermitidas opcoes => opcoes.Entradas.SelectMany((e, k) => e.Valores.Select(v => ($"{campo}.entradas[{k}].valores", (string?)v))
            .Concat(TextosDoPredicado($"{campo}.entradas[{k}].quando", e.Quando))),
        OpcoesDasRespostas respostas => respostas.Fatos.Select(f => ($"{campo}.fatos", (string?)f)),
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

    private static IEnumerable<FieldError> NaoGravavel(string campo, string? texto)
    {
        if (texto is not null && (texto.Contains('\0', StringComparison.Ordinal) || !TextoNormalizavel.TentarNormalizar(texto, out _)))
        {
            yield return new(campo, new DomainError(
                ModeloFormularioErrorCodes.TextoNaoGravavel, "O texto contém o caractere nulo (U+0000) ou não é Unicode válido."));
        }
    }
}
