namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O conteúdo do modelo lido da escrita, com a posição de cada parte na entrada: as recusas da
/// leitura e as da conferência contra o catálogo apontam o índice que veio. A parte que não se leu
/// fica de fora, com a recusa dela.
/// </summary>
internal sealed record ConteudoLido(
    string? Titulo,
    IReadOnlyList<(int Indice, EtapaDoModelo Etapa)> Etapas,
    IReadOnlyList<(int Indice, ItemDoModelo Item)> Itens,
    IReadOnlyList<(int Indice, TermoDoModelo Termo)> Termos,
    IReadOnlyList<string> Pressupostos,
    IReadOnlyList<(int Indice, GrupoDoModelo Grupo)> Grupos,
    IReadOnlyList<(int Indice, DocumentoExigidoDoModelo Documento)> Documentos,
    IReadOnlyList<FieldError> Erros)
{
    public ConteudoDoModelo ParaConteudo() => new(
        Titulo, [.. Etapas.Select(static e => e.Etapa)], [.. Itens.Select(static i => i.Item)], [.. Termos.Select(static t => t.Termo)], Pressupostos,
        [.. Grupos.Select(static g => g.Grupo)], [.. Documentos.Select(static d => d.Documento)]);

    /// <summary>
    /// Lê a entrada pela forma das regras do formulário (<see cref="EntradaDeRegras"/>): token
    /// desconhecido vira o sentinela, que a forma recusa depois; predicado, obrigatoriedade e
    /// restrição malformados são recusados aqui. O formato do campo de texto é o do fato no
    /// catálogo.
    /// </summary>
    public static ConteudoLido Ler(ConteudoDoModeloInput? entrada, IReadOnlyDictionary<string, string> formatos)
    {
        ArgumentNullException.ThrowIfNull(formatos);
        List<FieldError> erros = [];
        return new(
            entrada?.Titulo,
            LerLista(entrada?.Etapas, "etapas", (etapa, campo) => LerEtapa(etapa, campo, erros)),
            LerLista(entrada?.Itens, "itens", (item, campo) => LerItem(item, campo, formatos, erros)),
            LerLista(entrada?.Termos, "termos", (termo, campo) => LerTermo(termo, campo, erros)),
            [.. (entrada?.Pressupostos ?? []).Select(static p => p ?? string.Empty)],
            LerLista(entrada?.Grupos, "grupos", (grupo, campo) => LerGrupo(grupo, campo, formatos, erros)),
            LerLista(entrada?.DocumentosExigidos, "documentosExigidos", (documento, campo) => LerDocumento(documento, campo, erros)),
            erros);
    }

    /// <summary>
    /// Lê cada parte da lista de entrada e guarda a posição que ela tem na entrada: a parte que não
    /// se leu fica de fora, e as recusas da conferência seguinte ainda apontam a posição original.
    /// </summary>
    private static List<(int Indice, TLido Lido)> LerLista<TEntrada, TLido>(
        IReadOnlyList<TEntrada?>? entradas, string prefixo, Func<TEntrada?, string, TLido?> ler)
        where TLido : class
    {
        List<(int, TLido)> lidos = [];
        IReadOnlyList<TEntrada?> lista = entradas ?? [];
        for (int i = 0; i < lista.Count; i++)
        {
            if (ler(lista[i], $"{prefixo}[{i}]") is { } lido)
            {
                lidos.Add((i, lido));
            }
        }

        return lidos;
    }

    /// <summary>
    /// Lê o documento do conjunto padrão pela forma: os códigos de fase e de tipo presentes e as
    /// condições bem formadas. Que a fase exista no cronograma e o tipo no catálogo, e que os fatos
    /// citados sejam do processo, quem confere é a aplicação do modelo, que tem o processo.
    /// </summary>
    private static DocumentoExigidoDoModelo? LerDocumento(DocumentoExigidoDoModeloInput? entrada, string campo, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(Nula(campo));
            return null;
        }

        int recusasAntes = erros.Count;
        PredicadoDnf? quando = LerPredicado(entrada.Quando, $"{campo}.quando", erros, out _);
        if ((entrada.BasesLegais ?? []).Any(static b => b is null))
        {
            erros.Add(Nula($"{campo}.basesLegais"));
        }
        foreach ((string valor, string nome) in new[] { (entrada.FaseCodigo, "faseCodigo"), (entrada.TipoDocumentoCodigo, "tipoDocumentoCodigo"), (entrada.ConsequenciaIndeferimento, "consequenciaIndeferimento") })
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                erros.Add(new($"{campo}.{nome}", new DomainError(
                    ModeloFormularioErrorCodes.EntradaMalformada, $"O documento exigido do modelo precisa informar '{nome}'.")));
            }
        }

        if (entrada.PorOpcao is { } porOpcao)
        {
            if (string.IsNullOrWhiteSpace(porOpcao.FatoCodigo))
            {
                erros.Add(new($"{campo}.porOpcao.fatoCodigo", new DomainError(
                    ModeloFormularioErrorCodes.EntradaMalformada, "A exigência por opção precisa informar o fato cujas opções a multiplicam.")));
            }

            if ((porOpcao.Exceto ?? []).Any(string.IsNullOrWhiteSpace))
            {
                erros.Add(new($"{campo}.porOpcao.exceto", new DomainError(
                    ModeloFormularioErrorCodes.EntradaMalformada, "A opção excetuada precisa ser um código.")));
            }
        }

        return erros.Count > recusasAntes
            ? null
            : new DocumentoExigidoDoModelo(
                entrada.FaseCodigo, entrada.TipoDocumentoCodigo, entrada.Obrigatorio, quando, entrada.ConsequenciaIndeferimento,
                [.. (entrada.BasesLegais ?? []).OfType<BaseLegalDoModeloInput>().Select(static b => new BaseLegalDoDocumentoDoModelo(b.Referencia, b.Abrangencia, b.Status, b.Observacao))],
                entrada.PorOpcao is { } opcoes ? new ExigenciaPorOpcao(opcoes.FatoCodigo, [.. (opcoes.Exceto ?? []).OfType<string>()]) : null);
    }

    private static GrupoDoModelo? LerGrupo(
        GrupoColetadoInput? entrada, string campo, IReadOnlyDictionary<string, string> formatos, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(Nula(campo));
            return null;
        }

        int recusasAntes = erros.Count;
        PredicadoDnf? exibicao = LerPredicado(entrada.Exibicao, $"{campo}.exibicao", erros, out _);
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(entrada.Obrigatoriedade, entrada.PredicadoObrigatoriedade, campo, erros);
        List<ItemDoModelo> subitens = [];
        IReadOnlyList<FatoColetadoInput?> subitensDeEntrada = entrada.Subitens ?? [];
        for (int j = 0; j < subitensDeEntrada.Count; j++)
        {
            if (LerItem(subitensDeEntrada[j], $"{campo}.subitens[{j}]", formatos, erros) is { } subitem)
            {
                subitens.Add(subitem);
            }
        }

        return erros.Count > recusasAntes
            ? null
            : new GrupoDoModelo(
                entrada.Codigo, entrada.Ordem, entrada.EtapaCodigo, entrada.Rotulo, entrada.Minimo, entrada.Maximo, exibicao, obrigatoriedade!, subitens,
                entrada.IncluiCandidato);
    }

    private static EtapaDoModelo? LerEtapa(EtapaFormularioInput? entrada, string campo, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(Nula(campo));
            return null;
        }

        PredicadoDnf? exibicao = LerPredicado(entrada.Exibicao, $"{campo}.exibicao", erros, out bool lido);
        return lido
            ? new EtapaDoModelo(
                entrada.Codigo, entrada.Ordem, EstruturaFormulario.TipoDoToken(entrada.Tipo), EstruturaFormulario.BlocoDoToken(entrada.Bloco),
                entrada.Titulo, entrada.Descricao, entrada.Aviso, exibicao)
            : null;
    }

    private static ItemDoModelo? LerItem(
        FatoColetadoInput? entrada, string campo, IReadOnlyDictionary<string, string> formatos, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(Nula(campo));
            return null;
        }

        int recusasAntes = erros.Count;
        PredicadoDnf? exibicao = LerPredicado(entrada.Precondicao, $"{campo}.precondicao", erros, out _);
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(
            entrada.Obrigatoriedade, entrada.PredicadoObrigatoriedade, campo, erros);
        List<RestricaoValor> restricoes = [];
        IReadOnlyList<RestricaoValorInput?> restricoesDeEntrada = entrada.Restricoes ?? [];
        for (int j = 0; j < restricoesDeEntrada.Count; j++)
        {
            Result<RestricaoValor> restricao = restricoesDeEntrada[j] is { } r
                ? EntradaDeRegras.Restricao(r)
                : Result<RestricaoValor>.Failure(new DomainError(RestricaoValorErrorCodes.TipoDesconhecido, "A restrição de valor é nula."));
            if (restricao.IsSuccess)
            {
                restricoes.Add(restricao.Value!);
            }
            else
            {
                erros.Add(new($"{campo}.restricoes[{j}]", restricao.Error!));
            }
        }

        Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(entrada.Impedimento);
        if (impedimento.IsFailure)
        {
            erros.Add(new($"{campo}.impedimento.quando", impedimento.Error!));
        }

        if (erros.Count > recusasAntes)
        {
            return null;
        }

        string? formato = entrada.FatoCodigo is { } codigo && formatos.TryGetValue(ModeloFormulario.CodigoNaFormaGravada(codigo), out string? doCatalogo) ? doCatalogo : null;
        return new ItemDoModelo(
            entrada.FatoCodigo, entrada.Ordem, entrada.EtapaCodigo, entrada.Rotulo, TipoRenderizacaoCodigo.FromCodigo(entrada.TipoRenderizacao),
            formato, entrada.Ajuda, obrigatoriedade!, exibicao, restricoes, entrada.PedirConfirmacao, impedimento.Value);
    }

    private static TermoDoModelo? LerTermo(TermoExigidoInput? entrada, string campo, List<FieldError> erros)
    {
        if (entrada is null)
        {
            erros.Add(Nula(campo));
            return null;
        }

        int recusasAntes = erros.Count;
        PredicadoDnf? exibicao = LerPredicado(entrada.Exibicao, $"{campo}.exibicao", erros, out _);
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(entrada.Obrigatoriedade, entrada.PredicadoObrigatoriedade, campo, erros);
        return erros.Count > recusasAntes
            ? null
            : new TermoDoModelo(entrada.Codigo, entrada.Ordem, entrada.TermoId, entrada.VersaoId, exibicao, obrigatoriedade!);
    }

    private static PredicadoDnf? LerPredicado(
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? entrada, string campo, List<FieldError> erros, out bool lido)
    {
        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(entrada);
        lido = predicado.IsSuccess;
        if (!lido)
        {
            erros.Add(new(campo, predicado.Error!));
        }

        return predicado.Value;
    }

    private static Obrigatoriedade? LerObrigatoriedade(
        string? token, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicadoDeEntrada, string campo, List<FieldError> erros)
    {
        PredicadoDnf? predicado = LerPredicado(predicadoDeEntrada, $"{campo}.predicadoObrigatoriedade", erros, out bool lido);
        if (!lido)
        {
            return null;
        }

        if (EntradaDeRegras.Obrigatoriedade(token, predicado) is { } obrigatoriedade)
        {
            return obrigatoriedade;
        }

        erros.Add(new($"{campo}.obrigatoriedade", new DomainError(
            ModeloFormularioErrorCodes.ObrigatoriedadeInvalida,
            "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.")));
        return null;
    }

    private static FieldError Nula(string campo) => new(campo, new DomainError(
        ModeloFormularioErrorCodes.EntradaMalformada, "A etapa, o item, o grupo ou o termo veio nulo."));
}
