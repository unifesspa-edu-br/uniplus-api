namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Uma página do formulário é uma seção de itens ou um bloco que o sistema monta.</summary>
[DescricaoDoContrato("O que a página do formulário é: uma seção com itens de resposta ou um bloco que o sistema monta.")]
public enum TipoPaginaFormulario
{
    [CodigoDeContrato(
        "SECAO",
        "Seção",
        Descricao = "Página com os itens que o candidato responde.")]
    Secao = 1,

    [CodigoDeContrato(
        "BLOCO",
        "Bloco do sistema",
        Descricao = "Página que o sistema monta a partir da configuração do processo; o bloco diz qual.")]
    Bloco = 2,
}

/// <summary>Os blocos que o sistema monta a partir da configuração do processo.</summary>
[DescricaoDoContrato("Os blocos que o sistema monta no formulário a partir da configuração do processo seletivo.")]
public enum BlocoSistema
{
    [CodigoDeContrato(
        "COMPROVACAO_DOCUMENTAL",
        "Comprovação documental",
        Descricao = "Os documentos que o candidato envia, conforme a exigência de cada situação dele.")]
    ComprovacaoDocumental = 1,

    [CodigoDeContrato(
        "MODALIDADES_CALCULADAS",
        "Modalidades calculadas",
        Descricao = "As modalidades de concorrência em que o candidato se enquadra, calculadas pelas respostas dele.")]
    ModalidadesCalculadas = 2,

    [CodigoDeContrato(
        "REVISAO_E_ACEITE",
        "Revisão e aceite",
        Descricao = "A conferência das respostas e o aceite dos termos, sempre a última página do formulário.")]
    RevisaoEAceite = 3,
}

/// <summary>Uma página, na forma que a estrutura do formulário confere.</summary>
public sealed record PaginaEstrutura(string Codigo, int? Ordem, TipoPaginaFormulario Tipo, BlocoSistema? Bloco);

/// <summary>Um item, na forma que a estrutura do formulário confere: o fato, a ordem e a seção.</summary>
public sealed record ItemEstrutura(string FatoCodigo, int Ordem, string? PaginaCodigo);

/// <summary>
/// As regras de forma de um formulário, as mesmas no processo e no modelo (ADR-0137): quais blocos
/// a declaração admite e exige, a revisão e aceite por último, e os itens em seções, na ordem delas.
/// </summary>
public static class EstruturaFormulario
{
    /// <summary>Nome da propriedade das páginas nos pedidos que recebem o formulário; muda junto com o nome dela no pedido.</summary>
    public const string CaminhoDasPaginas = "paginas";

    /// <summary>Os blocos que a declaração admite: as modalidades calculadas só fazem sentido no formulário que serve à fase de inscrição.</summary>
    public static IReadOnlySet<BlocoSistema> BlocosAdmitidos(DeclaracaoDoFormulario declaracao) => declaracao.ServeAFaseDeInscricao
        ? new HashSet<BlocoSistema> { BlocoSistema.ComprovacaoDocumental, BlocoSistema.ModalidadesCalculadas, BlocoSistema.RevisaoEAceite }
        : new HashSet<BlocoSistema> { BlocoSistema.ComprovacaoDocumental, BlocoSistema.RevisaoEAceite };

    /// <summary>Os blocos que todo formulário exige, de qualquer declaração: ele termina na revisão e aceite.</summary>
    public static IReadOnlySet<BlocoSistema> BlocosExigidos() =>
        new HashSet<BlocoSistema> { BlocoSistema.RevisaoEAceite };

    /// <summary>
    /// Confere as páginas: código e ordem únicos, seção sem bloco, bloco admitido pela declaração e
    /// não repetido, os blocos exigidos presentes e a revisão e aceite como última página.
    /// </summary>
    public static IReadOnlyList<FieldError> ValidarPaginas(DeclaracaoDoFormulario declaracao, IReadOnlyList<PaginaEstrutura> paginas)
    {
        ArgumentNullException.ThrowIfNull(paginas);

        return [.. ValidarCadaPagina(declaracao, paginas), .. ConferirBlocosDoFormulario(paginas, BlocosAdmitidos(declaracao))];
    }

    /// <summary>
    /// Confere cada página contra as outras: código e ordem únicos, seção sem bloco, bloco admitido e não
    /// repetido. A lista vem na posição do pedido; a posição nula é a página recusada, que não entra na
    /// comparação e não desloca a posição das demais. Não confere o conjunto (blocos exigidos, revisão e
    /// aceite no fim), que só se conclui com todas as páginas.
    /// </summary>
    public static IReadOnlyList<FieldError> ValidarCadaPagina(DeclaracaoDoFormulario declaracao, IReadOnlyList<PaginaEstrutura?> paginas)
    {
        ArgumentNullException.ThrowIfNull(paginas);

        IReadOnlySet<BlocoSistema> admitidos = BlocosAdmitidos(declaracao);
        return [.. paginas.SelectMany((pagina, i) => pagina is null ? [] : ConferirPagina(paginas, i, pagina, admitidos))];
    }

    private static IEnumerable<FieldError> ConferirPagina(
        IReadOnlyList<PaginaEstrutura?> paginas, int posicao, PaginaEstrutura pagina, IReadOnlySet<BlocoSistema> admitidos)
    {
        string campo = $"{CaminhoDasPaginas}[{posicao}]";
        PaginaEstrutura[] anteriores = [.. paginas.Take(posicao).OfType<PaginaEstrutura>()];
        FieldError?[] recusas =
        [
            anteriores.Any(a => string.Equals(a.Codigo, pagina.Codigo, StringComparison.Ordinal))
                ? Recusa($"{campo}.codigo", EstruturaFormularioErrorCodes.PaginaCodigoDuplicado, "Existe outra página com o mesmo código no formulário.")
                : null,
            anteriores.Any(a => a.Ordem == pagina.Ordem)
                ? Recusa($"{campo}.ordem", EstruturaFormularioErrorCodes.PaginaOrdemDuplicada, "Duas páginas do formulário têm a mesma ordem.")
                : null,
            RecusaDoBloco(pagina, $"{campo}.bloco", anteriores, admitidos),
        ];
        return recusas.OfType<FieldError>();
    }

    private static FieldError? RecusaDoBloco(
        PaginaEstrutura pagina, string campo, IEnumerable<PaginaEstrutura> anteriores, IReadOnlySet<BlocoSistema> admitidos) =>
        (pagina.Tipo, pagina.Bloco) switch
        {
            (TipoPaginaFormulario.Secao, not null) =>
                Recusa(campo, EstruturaFormularioErrorCodes.SecaoComBloco, "Uma seção não declara bloco de sistema."),
            (TipoPaginaFormulario.Bloco, { } bloco) when admitidos.Contains(bloco) =>
                anteriores.Any(a => a.Tipo == TipoPaginaFormulario.Bloco && a.Bloco == bloco)
                    ? Recusa(campo, EstruturaFormularioErrorCodes.BlocoRepetido, "O bloco aparece mais de uma vez no formulário.")
                    : null,
            (TipoPaginaFormulario.Bloco, _) =>
                Recusa(campo, EstruturaFormularioErrorCodes.BlocoNaoAdmitido, "O bloco não é admitido pelo formulário."),
            _ => null,
        };

    private static IEnumerable<FieldError> ConferirBlocosDoFormulario(IReadOnlyList<PaginaEstrutura> paginas, IReadOnlySet<BlocoSistema> admitidos)
    {
        HashSet<BlocoSistema> blocos = [.. paginas.Where(static e => e.Tipo == TipoPaginaFormulario.Bloco).Select(static e => e.Bloco).OfType<BlocoSistema>().Where(admitidos.Contains)];
        foreach (BlocoSistema exigido in BlocosExigidos().Where(b => !blocos.Contains(b)).Order())
        {
            yield return Recusa(CaminhoDasPaginas, EstruturaFormularioErrorCodes.BlocoExigidoAusente, $"O formulário precisa do bloco {exigido.Codigo()}.");
        }

        if (blocos.Contains(BlocoSistema.RevisaoEAceite) && paginas.MaxBy(static e => e.Ordem)?.Bloco != BlocoSistema.RevisaoEAceite)
        {
            yield return Recusa(CaminhoDasPaginas, EstruturaFormularioErrorCodes.RevisaoEAceiteForaDoFim, "A revisão e aceite é sempre a última página do formulário.");
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));

    /// <summary>
    /// Confere os itens contra as páginas: todo item está numa seção do formulário, e a ordem dos
    /// itens acompanha a ordem das seções — um item de seção posterior nunca vem antes. No
    /// rascunho (<paramref name="secaoObrigatoria"/> falso), o item ainda sem seção passa, e só a
    /// seção declarada é conferida; a recusa aponta sempre o índice do item na lista recebida.
    /// </summary>
    public static IReadOnlyList<FieldError> ValidarItens(
        IReadOnlyList<PaginaEstrutura> paginas, IReadOnlyList<ItemEstrutura> itens, bool secaoObrigatoria = true)
    {
        ArgumentNullException.ThrowIfNull(paginas);
        ArgumentNullException.ThrowIfNull(itens);

        Dictionary<string, PaginaEstrutura> secoes = paginas
            .Where(static e => e.Tipo == TipoPaginaFormulario.Secao)
            .GroupBy(static e => e.Codigo, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);

        List<FieldError> foraDeSecao = [.. itens.Select((item, i) => RecusaDoItemForaDeSecao(item, i, secoes, secaoObrigatoria)).OfType<FieldError>()];
        return foraDeSecao.Count > 0 ? foraDeSecao : RecusaDaOrdemDosItens(secoes, itens);
    }

    private static FieldError? RecusaDoItemForaDeSecao(
        ItemEstrutura item, int posicao, Dictionary<string, PaginaEstrutura> secoes, bool secaoObrigatoria) =>
        (item.PaginaCodigo is not { } pagina ? secaoObrigatoria : !secoes.ContainsKey(pagina))
            ? Recusa($"itens[{posicao}].paginaCodigo", EstruturaFormularioErrorCodes.ItemForaDeSecao, $"O item '{item.FatoCodigo}' precisa estar numa seção do formulário.")
            : null;

    private static List<FieldError> RecusaDaOrdemDosItens(Dictionary<string, PaginaEstrutura> secoes, IReadOnlyList<ItemEstrutura> itens)
    {
        ItemEstrutura[] emOrdem = [.. itens.Where(static i => i.PaginaCodigo is not null).OrderBy(static i => i.Ordem)];
        for (int i = 1; i < emOrdem.Length; i++)
        {
            if (secoes[emOrdem[i].PaginaCodigo!].Ordem < secoes[emOrdem[i - 1].PaginaCodigo!].Ordem)
            {
                return [Recusa("itens", EstruturaFormularioErrorCodes.ItemForaDaOrdemDasSecoes,
                    $"O item '{emOrdem[i].FatoCodigo}' vem depois de '{emOrdem[i - 1].FatoCodigo}', mas está numa seção anterior à dele.")];
            }
        }

        return [];
    }
}

/// <summary>Códigos de erro da estrutura do formulário.</summary>
public static class EstruturaFormularioErrorCodes
{
    public const string PaginaCodigoDuplicado = "EstruturaFormulario.PaginaCodigoDuplicado";
    public const string PaginaOrdemDuplicada = "EstruturaFormulario.PaginaOrdemDuplicada";
    public const string TipoDePaginaObrigatorio = "EstruturaFormulario.TipoDePaginaObrigatorio";
    public const string SecaoComBloco = "EstruturaFormulario.SecaoComBloco";
    public const string BlocoNaoAdmitido = "EstruturaFormulario.BlocoNaoAdmitido";
    public const string BlocoRepetido = "EstruturaFormulario.BlocoRepetido";
    public const string BlocoExigidoAusente = "EstruturaFormulario.BlocoExigidoAusente";
    public const string RevisaoEAceiteForaDoFim = "EstruturaFormulario.RevisaoEAceiteForaDoFim";
    public const string ItemForaDeSecao = "EstruturaFormulario.ItemForaDeSecao";
    public const string ItemForaDaOrdemDasSecoes = "EstruturaFormulario.ItemForaDaOrdemDasSecoes";
    public const string PaginaCodigoInvalido = "EstruturaFormulario.PaginaCodigoInvalido";
    public const string PaginaOrdemInvalida = "EstruturaFormulario.PaginaOrdemInvalida";
    public const string PaginaTituloInvalido = "EstruturaFormulario.PaginaTituloInvalido";
    public const string PaginaTextoTamanho = "EstruturaFormulario.PaginaTextoTamanho";
    public const string ExibicaoForaDeSecao = "EstruturaFormulario.ExibicaoForaDeSecao";
    public const string TituloTamanho = "EstruturaFormulario.TituloTamanho";
    public const string SecaoReservada = "EstruturaFormulario.SecaoReservada";
    public const string SecaoReservadaAlterada = "EstruturaFormulario.SecaoReservadaAlterada";
    public const string DadoBasicoAlterado = "EstruturaFormulario.DadoBasicoAlterado";
}
