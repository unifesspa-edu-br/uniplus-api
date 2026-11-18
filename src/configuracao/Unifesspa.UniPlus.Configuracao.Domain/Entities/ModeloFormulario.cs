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

/// <summary>Uma base legal que sustenta o documento exigido do modelo; copiada como está para a exigência do processo.</summary>
public sealed record BaseLegalDoDocumentoDoModelo(string Referencia, string Abrangencia, string Status, string? Observacao);

/// <summary>
/// O documento exigido uma vez por opção que o processo oferece para o fato, salvo as de
/// <paramref name="Exceto"/>. As opções só existem no processo: o modelo não as enumera, e a opção
/// que o processo passar a oferecer entra na próxima aplicação sem mudar o modelo.
/// </summary>
public sealed record ExigenciaPorOpcao(string FatoCodigo, IReadOnlyList<string> Exceto);

/// <summary>
/// Um documento do conjunto padrão que o modelo exige do processo (UNI-REQ-0026): o tipo do catálogo e
/// a fase canônica pelos códigos, que o processo resolve ao aplicar o modelo, as condições sobre fatos
/// que o tornam exigido (ausentes, vale para todo candidato) e a consequência de o candidato não o
/// apresentar. É sempre do formulário da finalidade do próprio modelo. Com <paramref name="PorOpcao"/>,
/// o processo recebe uma exigência por opção ofertada, cada uma condicionada à declaração dela.
/// </summary>
public sealed record DocumentoExigidoDoModelo(
    string FaseCodigo,
    string TipoDocumentoCodigo,
    bool Obrigatorio,
    PredicadoDnf? Quando,
    string ConsequenciaIndeferimento,
    IReadOnlyList<BaseLegalDoDocumentoDoModelo> BasesLegais,
    ExigenciaPorOpcao? PorOpcao = null)
{
    /// <summary>Os fatos que as condições do documento citam, inclusive o das opções que o multiplicam.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        PorOpcao is null
            ? Quando?.FatosCitados ?? []
            : [.. (Quando?.FatosCitados ?? []).Append(PorOpcao.FatoCodigo).Distinct(StringComparer.Ordinal)];
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
    IReadOnlyList<GrupoDoModelo> Grupos,
    IReadOnlyList<DocumentoExigidoDoModelo>? DocumentosExigidos = null)
{
    /// <summary>O conjunto padrão de documentos exigidos que a aplicação do modelo copia para o processo; ausente é nenhum.</summary>
    public IReadOnlyList<DocumentoExigidoDoModelo> Documentos => DocumentosExigidos ?? [];

    /// <summary>
    /// O conteúdo como o avaliador de formulário o lê: cada seção, na ordem, com os seus itens e grupos
    /// repetíveis — os blocos que o sistema monta não têm regra, como na definição do processo —; os
    /// termos; as derivações por regra do catálogo, resolvidas com as respostas simuladas; e os agregados
    /// do catálogo sobre os grupos.
    /// </summary>
    public DefinicaoFormulario ParaAvaliacao(IReadOnlyList<DerivacaoDoFato> derivacoes, IReadOnlyList<DefinicaoAgregado> agregados)
    {
        ArgumentNullException.ThrowIfNull(derivacoes);
        ArgumentNullException.ThrowIfNull(agregados);
        ILookup<string?, ItemDoModelo> itensPorEtapa = Itens.ToLookup(static i => i.EtapaCodigo, StringComparer.Ordinal);
        ILookup<string?, GrupoDoModelo> gruposPorEtapa = Grupos.ToLookup(static g => g.EtapaCodigo, StringComparer.Ordinal);
        return new DefinicaoFormulario(
            [.. Etapas.Where(static e => e.Tipo == TipoEtapaFormulario.Secao).OrderBy(static e => e.Ordem).Select(e => new DefinicaoEtapa(
                e.Codigo,
                e.Exibicao,
                [.. itensPorEtapa[e.Codigo].OrderBy(static i => i.Ordem).Select(Item)],
                [.. gruposPorEtapa[e.Codigo].OrderBy(static g => g.Ordem).Select(static g => new DefinicaoGrupo(
                    g.Codigo, g.Exibicao, g.Obrigatoriedade, g.Minimo, g.Maximo, [.. g.Subitens.OrderBy(static s => s.Ordem).Select(Item)], g.IncluiCandidato))]))],
            [.. Termos.OrderBy(static t => t.Ordem).Select(static t => new DefinicaoTermo(t.Codigo, t.Exibicao, t.Obrigatoriedade))],
            derivacoes,
            agregados);
    }

    private static DefinicaoItem Item(ItemDoModelo item) =>
        new(item.FatoCodigo, item.Exibicao, item.Obrigatoriedade, item.Restricoes, item.Impedimento, item.Formato);
}

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

        ConteudoDoModelo normalizado = FormaGravadaDoModelo.Normalizar(conteudo);
        List<FieldError> erros = ConferirCadastro(codigo, nome, descricao, tipoProcessoCodigo, finalidade);
        erros.AddRange(NoConteudo(ConferenciaDoConteudoDoModelo.ConferirNormalizado(finalidade, normalizado, derivacoes)));
        if (erros.Count > 0)
        {
            return Result<ModeloFormulario>.ValidationFailure(erros);
        }

        ModeloFormulario modelo = new()
        {
            Codigo = FormaGravadaDoModelo.Normalizar(codigo!),
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

        ConteudoDoModelo normalizado = FormaGravadaDoModelo.Normalizar(conteudo);
        List<FieldError> erros = ConferirDescritivo(nome, descricao, tipoProcessoCodigo);
        erros.AddRange(NoConteudo(ConferenciaDoConteudoDoModelo.ConferirNormalizado(Finalidade, normalizado, derivacoes)));
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
        return FormaGravadaDoModelo.Normalizar(conteudo);
    }

    /// <summary>O código na forma em que é gravado: aparado e em NFC.</summary>
    public static string CodigoNaFormaGravada(string? codigo) => FormaGravadaDoModelo.Normalizar(codigo);

    /// <summary>As recusas do conteúdo apontam o campo dentro dele, no caminho da escrita.</summary>
    public static IEnumerable<FieldError> NoConteudo(IEnumerable<FieldError> erros) =>
        erros.Select(static e => e.NoCaminhoDe("conteudo"));

    private static List<FieldError> ValidarCodigo(string? codigo)
    {
        List<FieldError> erros = [.. TextosGravaveisDoModelo.NaoGravavel("codigo", codigo)];
        if (string.IsNullOrWhiteSpace(codigo))
        {
            erros.Add(new("codigo", new DomainError(ModeloFormularioErrorCodes.CodigoObrigatorio, "O código do modelo é obrigatório.")));
        }
        else if (FormaGravadaDoModelo.Normalizar(codigo).Length > CodigoMaxLength)
        {
            erros.Add(new("codigo", new DomainError(
                ModeloFormularioErrorCodes.CodigoTamanho, $"O código do modelo deve ter no máximo {CodigoMaxLength} caracteres.")));
        }

        return erros;
    }

    private static List<FieldError> ValidarDescritivo(string? nome, string? descricao, string? tipoProcessoCodigo)
    {
        List<FieldError> erros = [.. TextosGravaveisDoModelo.NaoGravavel("nome", nome), .. TextosGravaveisDoModelo.NaoGravavel("descricao", descricao), .. TextosGravaveisDoModelo.NaoGravavel("tipoProcessoCodigo", tipoProcessoCodigo)];
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
        TipoProcessoCodigo = FormaDoItem.TextoOpcional(tipoProcessoCodigo) is { } tipo ? TextoNormalizavel.AparadoEmNfc(tipo) : null;
        Conteudo = conteudo with
        {
            Etapas = [.. conteudo.Etapas.OrderBy(static e => e.Ordem)],
            Itens = [.. conteudo.Itens.OrderBy(static i => i.Ordem).Select(ComRestricoesOrdenadas)],
            Termos = [.. conteudo.Termos.OrderBy(static t => t.Ordem)],
            Grupos = [.. conteudo.Grupos.OrderBy(static g => g.Ordem)
                .Select(static g => g with { Subitens = [.. g.Subitens.OrderBy(static s => s.Ordem).Select(ComRestricoesOrdenadas)] })],
            DocumentosExigidos = conteudo.Documentos,
        };
    }

    private static ItemDoModelo ComRestricoesOrdenadas(ItemDoModelo campo) =>
        campo with { Restricoes = [.. campo.Restricoes.OrderBy(static r => r.Tipo)] };

    /// <summary>
    /// O texto que o banco grava: sem caractere nulo e Unicode válido. Quem consulta o banco com um
    /// texto recebido confere isto antes, para a recusa ser a do modelo e não um erro do banco.
    /// </summary>
    public static bool EhGravavel(string texto) => TextosGravaveisDoModelo.EhGravavel(texto);
}
