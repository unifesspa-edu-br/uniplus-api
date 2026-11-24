namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Uma etapa do formulário é uma seção de itens ou um bloco que o sistema monta.</summary>
[DescricaoDoContrato("O que a etapa do formulário é: uma seção com itens de resposta ou um bloco que o sistema monta.")]
public enum TipoEtapaFormulario
{
    [CodigoDeContrato(
        "SECAO",
        "Seção",
        Descricao = "Etapa com os itens que o candidato responde.")]
    Secao = 1,

    [CodigoDeContrato(
        "BLOCO",
        "Bloco do sistema",
        Descricao = "Etapa que o sistema monta a partir da configuração do processo; o bloco diz qual.")]
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
        Descricao = "A conferência das respostas e o aceite dos termos, sempre a última etapa do formulário.")]
    RevisaoEAceite = 3,
}

/// <summary>Uma etapa, na forma que a estrutura do formulário confere.</summary>
public sealed record EtapaEstrutura(string Codigo, int Ordem, TipoEtapaFormulario Tipo, BlocoSistema? Bloco);

/// <summary>Um item, na forma que a estrutura do formulário confere: o fato, a ordem e a seção.</summary>
public sealed record ItemEstrutura(string FatoCodigo, int Ordem, string? EtapaCodigo);

/// <summary>
/// As regras de forma de um formulário, as mesmas no processo e no modelo (ADR-0137): quais blocos
/// a finalidade admite e exige, a revisão e aceite por último, e os itens em seções, na ordem delas.
/// </summary>
public static class EstruturaFormulario
{
    /// <summary>Nome da propriedade das etapas nos pedidos que recebem o formulário; muda junto com o nome dela no pedido.</summary>
    public const string CaminhoDasEtapas = "etapas";

    /// <summary>Os blocos que a finalidade admite: as modalidades calculadas só fazem sentido na inscrição.</summary>
    public static IReadOnlySet<BlocoSistema> BlocosAdmitidos(FinalidadeFormulario finalidade) => finalidade switch
    {
        FinalidadeFormulario.Inscricao =>
            new HashSet<BlocoSistema> { BlocoSistema.ComprovacaoDocumental, BlocoSistema.ModalidadesCalculadas, BlocoSistema.RevisaoEAceite },
        FinalidadeFormulario.IsencaoTaxa or FinalidadeFormulario.Habilitacao =>
            new HashSet<BlocoSistema> { BlocoSistema.ComprovacaoDocumental, BlocoSistema.RevisaoEAceite },
        _ => [],
    };

    /// <summary>Os blocos que todo formulário exige, de qualquer finalidade: ele termina na revisão e aceite.</summary>
    public static IReadOnlySet<BlocoSistema> BlocosExigidos() =>
        new HashSet<BlocoSistema> { BlocoSistema.RevisaoEAceite };

    /// <summary>
    /// Confere as etapas: código e ordem únicos, seção sem bloco, bloco admitido pela finalidade e
    /// não repetido, os blocos exigidos presentes e a revisão e aceite como última etapa.
    /// </summary>
    public static IReadOnlyList<FieldError> ValidarEtapas(FinalidadeFormulario finalidade, IReadOnlyList<EtapaEstrutura> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        IReadOnlySet<BlocoSistema> admitidos = BlocosAdmitidos(finalidade);
        List<FieldError> erros = [.. etapas.SelectMany((_, i) => ConferirEtapa(etapas, i, admitidos))];
        erros.AddRange(ConferirBlocosDoFormulario(etapas, admitidos));

        return erros;
    }

    private static IEnumerable<FieldError> ConferirEtapa(IReadOnlyList<EtapaEstrutura> etapas, int posicao, IReadOnlySet<BlocoSistema> admitidos)
    {
        EtapaEstrutura etapa = etapas[posicao];
        string campo = $"{CaminhoDasEtapas}[{posicao}]";
        IEnumerable<EtapaEstrutura> anteriores = etapas.Take(posicao);
        FieldError?[] recusas =
        [
            anteriores.Any(a => string.Equals(a.Codigo, etapa.Codigo, StringComparison.Ordinal))
                ? Recusa($"{campo}.codigo", EstruturaFormularioErrorCodes.EtapaCodigoDuplicado, $"O código de etapa '{etapa.Codigo}' se repete no formulário.")
                : null,
            anteriores.Any(a => a.Ordem == etapa.Ordem)
                ? Recusa($"{campo}.ordem", EstruturaFormularioErrorCodes.EtapaOrdemDuplicada, "Duas etapas do formulário têm a mesma ordem.")
                : null,
            RecusaDoBloco(etapa, $"{campo}.bloco", anteriores, admitidos),
        ];
        return recusas.OfType<FieldError>();
    }

    private static FieldError? RecusaDoBloco(
        EtapaEstrutura etapa, string campo, IEnumerable<EtapaEstrutura> anteriores, IReadOnlySet<BlocoSistema> admitidos) =>
        (etapa.Tipo, etapa.Bloco) switch
        {
            (TipoEtapaFormulario.Secao, not null) =>
                Recusa(campo, EstruturaFormularioErrorCodes.SecaoComBloco, "Uma seção não declara bloco de sistema."),
            (TipoEtapaFormulario.Bloco, { } bloco) when admitidos.Contains(bloco) =>
                anteriores.Any(a => a.Tipo == TipoEtapaFormulario.Bloco && a.Bloco == bloco)
                    ? Recusa(campo, EstruturaFormularioErrorCodes.BlocoRepetido, "O bloco aparece mais de uma vez no formulário.")
                    : null,
            (TipoEtapaFormulario.Bloco, _) =>
                Recusa(campo, EstruturaFormularioErrorCodes.BlocoNaoAdmitido, "O bloco não é admitido pela finalidade do formulário."),
            _ => null,
        };

    private static IEnumerable<FieldError> ConferirBlocosDoFormulario(IReadOnlyList<EtapaEstrutura> etapas, IReadOnlySet<BlocoSistema> admitidos)
    {
        HashSet<BlocoSistema> blocos = [.. etapas.Where(static e => e.Tipo == TipoEtapaFormulario.Bloco).Select(static e => e.Bloco).OfType<BlocoSistema>().Where(admitidos.Contains)];
        foreach (BlocoSistema exigido in BlocosExigidos().Where(b => !blocos.Contains(b)).Order())
        {
            yield return Recusa(CaminhoDasEtapas, EstruturaFormularioErrorCodes.BlocoExigidoAusente, $"O formulário precisa do bloco {exigido.Codigo()}.");
        }

        if (blocos.Contains(BlocoSistema.RevisaoEAceite) && etapas.MaxBy(static e => e.Ordem)?.Bloco != BlocoSistema.RevisaoEAceite)
        {
            yield return Recusa(CaminhoDasEtapas, EstruturaFormularioErrorCodes.RevisaoEAceiteForaDoFim, "A revisão e aceite é sempre a última etapa do formulário.");
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));

    /// <summary>
    /// Confere os itens contra as etapas: todo item está numa seção do formulário, e a ordem dos
    /// itens acompanha a ordem das seções — um item de seção posterior nunca vem antes. No
    /// rascunho (<paramref name="secaoObrigatoria"/> falso), o item ainda sem seção passa, e só a
    /// seção declarada é conferida; a recusa aponta sempre o índice do item na lista recebida.
    /// </summary>
    public static IReadOnlyList<FieldError> ValidarItens(
        IReadOnlyList<EtapaEstrutura> etapas, IReadOnlyList<ItemEstrutura> itens, bool secaoObrigatoria = true)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(itens);

        Dictionary<string, EtapaEstrutura> secoes = etapas
            .Where(static e => e.Tipo == TipoEtapaFormulario.Secao)
            .GroupBy(static e => e.Codigo, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);

        List<FieldError> foraDeSecao = [.. itens.Select((item, i) => RecusaDoItemForaDeSecao(item, i, secoes, secaoObrigatoria)).OfType<FieldError>()];
        return foraDeSecao.Count > 0 ? foraDeSecao : RecusaDaOrdemDosItens(secoes, itens);
    }

    private static FieldError? RecusaDoItemForaDeSecao(
        ItemEstrutura item, int posicao, Dictionary<string, EtapaEstrutura> secoes, bool secaoObrigatoria) =>
        (item.EtapaCodigo is not { } etapa ? secaoObrigatoria : !secoes.ContainsKey(etapa))
            ? Recusa($"itens[{posicao}].etapaCodigo", EstruturaFormularioErrorCodes.ItemForaDeSecao, $"O item '{item.FatoCodigo}' precisa estar numa seção do formulário.")
            : null;

    private static List<FieldError> RecusaDaOrdemDosItens(Dictionary<string, EtapaEstrutura> secoes, IReadOnlyList<ItemEstrutura> itens)
    {
        ItemEstrutura[] emOrdem = [.. itens.Where(static i => i.EtapaCodigo is not null).OrderBy(static i => i.Ordem)];
        for (int i = 1; i < emOrdem.Length; i++)
        {
            if (secoes[emOrdem[i].EtapaCodigo!].Ordem < secoes[emOrdem[i - 1].EtapaCodigo!].Ordem)
            {
                return [Recusa("itens", EstruturaFormularioErrorCodes.ItemForaDaOrdemDasSecoes,
                    $"O item '{emOrdem[i].FatoCodigo}' vem depois de '{emOrdem[i - 1].FatoCodigo}', mas está numa seção anterior à dele.")];
            }
        }

        return [];
    }

    /// <summary>A finalidade do código; código ausente ou desconhecido não é finalidade.</summary>
    public static FinalidadeFormulario? FinalidadeDoCodigo(string? codigo) =>
        CatalogoDe<FinalidadeFormulario>.TentarObter(codigo, out FinalidadeFormulario finalidade) ? finalidade : null;
}

/// <summary>Códigos de erro da estrutura do formulário.</summary>
public static class EstruturaFormularioErrorCodes
{
    public const string EtapaCodigoDuplicado = "EstruturaFormulario.EtapaCodigoDuplicado";
    public const string EtapaOrdemDuplicada = "EstruturaFormulario.EtapaOrdemDuplicada";
    public const string TipoDeEtapaObrigatorio = "EstruturaFormulario.TipoDeEtapaObrigatorio";
    public const string SecaoComBloco = "EstruturaFormulario.SecaoComBloco";
    public const string BlocoNaoAdmitido = "EstruturaFormulario.BlocoNaoAdmitido";
    public const string BlocoRepetido = "EstruturaFormulario.BlocoRepetido";
    public const string BlocoExigidoAusente = "EstruturaFormulario.BlocoExigidoAusente";
    public const string RevisaoEAceiteForaDoFim = "EstruturaFormulario.RevisaoEAceiteForaDoFim";
    public const string ItemForaDeSecao = "EstruturaFormulario.ItemForaDeSecao";
    public const string ItemForaDaOrdemDasSecoes = "EstruturaFormulario.ItemForaDaOrdemDasSecoes";
    public const string EtapaCodigoInvalido = "EstruturaFormulario.EtapaCodigoInvalido";
    public const string EtapaOrdemInvalida = "EstruturaFormulario.EtapaOrdemInvalida";
    public const string EtapaTituloInvalido = "EstruturaFormulario.EtapaTituloInvalido";
    public const string EtapaTextoTamanho = "EstruturaFormulario.EtapaTextoTamanho";
    public const string ExibicaoForaDeSecao = "EstruturaFormulario.ExibicaoForaDeSecao";
    public const string FinalidadeInvalida = "EstruturaFormulario.FinalidadeInvalida";
    public const string TituloTamanho = "EstruturaFormulario.TituloTamanho";
    public const string SecaoReservada = "EstruturaFormulario.SecaoReservada";
    public const string SecaoReservadaAlterada = "EstruturaFormulario.SecaoReservadaAlterada";
    public const string DadoBasicoAlterado = "EstruturaFormulario.DadoBasicoAlterado";
}
